using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Runtime.Remoting;
using System.Runtime.Remoting.Lifetime;
using System.Security;
using System.Security.Permissions;
using System.Security.Policy;
using System.Text.RegularExpressions;
using System.Web;

using DotNetFiddle.Infrastructure.Worker;


namespace DotNetFiddle.Infrastructure
{
	public class SandboxHelper
	{
		public static void ExecuteInFullTrust(Action action)
		{
			try
			{
				new PermissionSet(PermissionState.Unrestricted).Assert();
				action();
			}
			finally
			{
				PermissionSet.RevertAssert();
			}
		}

		public static T ExecuteInFullTrust<T>(Func<T> action)
		{
			try
			{
				new PermissionSet(PermissionState.Unrestricted).Assert();
				return action();
			}
			finally
			{
				PermissionSet.RevertAssert();
			}
		}

		public static AppDomain CreateSandboxDomain(bool useSandboxAsHomeDir, Dictionary<AssemblyName, string> mappings)
		{
			if (!AppDomain.MonitoringIsEnabled)
				AppDomain.MonitoringIsEnabled = true;

			var sandboxFolder = WorkerConfiguration.Current.SandboxFolder;

			if (string.IsNullOrWhiteSpace(sandboxFolder))
				useSandboxAsHomeDir = false;

			AppDomainSetup setup = new AppDomainSetup();
			setup.DisallowCodeDownload = true;
			setup.DisallowBindingRedirects = true;
			setup.DisallowPublisherPolicy = true;
			var basePath = AppDomain.CurrentDomain.BaseDirectory;

			// we need to check for unit tests
			if (AppDomain.CurrentDomain.BaseDirectory.EndsWith(".Web\\"))
				basePath += "\\bin";

			if (useSandboxAsHomeDir)
				setup.ApplicationBase = sandboxFolder;
			else
				setup.ApplicationBase = basePath;

			setup.PartialTrustVisibleAssemblies = GetPartialTrustVisibleAssemblies();

			var permissions = new PermissionSet(PermissionState.None);

			List<StrongName> names = new List<StrongName>();

			List<string> allowedLibraries = new List<string>();
			var apps = AppDomain.CurrentDomain.GetAssemblies();
			foreach (var assembly in apps)
			{
				var path = GetAssemblyLocation(assembly);
				if (!string.IsNullOrWhiteSpace(path))
				{
					allowedLibraries.Add(path);
				}

				var tmpPath = assembly.ManifestModule.FullyQualifiedName;
				if (path != tmpPath && File.Exists(tmpPath))
					allowedLibraries.Add(tmpPath);

				// Roslyn loads System assembles from Microsoft.Net folder, so for them we need to add another path
				if (assembly.GlobalAssemblyCache && assembly.FullName.StartsWith("System"))
				{
					allowedLibraries.Add(GetSystemPaths(assembly));
				}

				var strongName = assembly.Evidence.GetHostEvidence<StrongName>();
				if (strongName != null)
					names.Add(strongName);
			}

			permissions.AddPermission(new FileIOPermission(FileIOPermissionAccess.Read | FileIOPermissionAccess.PathDiscovery, allowedLibraries.ToArray()));

			if (!string.IsNullOrWhiteSpace(sandboxFolder))
				permissions.AddPermission(new FileIOPermission(FileIOPermissionAccess.AllAccess, sandboxFolder));

			permissions.AddPermission(
				new FileIOPermission(FileIOPermissionAccess.AllAccess, LocalResourcesUtils.GetSystemTempWorkerDir()));

			// to allow managed code execution
			permissions.AddPermission(new SecurityPermission(SecurityPermissionFlag.Execution | SecurityPermissionFlag.SerializationFormatter));

			// allow only request(Connet), and don't allow to open own ports (Accept)
			permissions.AddPermission(new WebPermission(NetworkAccess.Connect, new Regex(".*")));

			// we don't need it as both Program and Main should be public
			if (WorkerConfiguration.Current.AllowReflectionMemberAccess)
				permissions.AddPermission(new ReflectionPermission(PermissionState.Unrestricted));

			var domain = AppDomain.CreateDomain("RunContainer.AppDomain_" + Guid.NewGuid(), null, setup, permissions, names.ToArray());


			if (!string.IsNullOrWhiteSpace(sandboxFolder))
			{
				if (Directory.Exists(sandboxFolder))
				{
					Directory.Delete(sandboxFolder, true);
				}

				Directory.CreateDirectory(sandboxFolder);
			}

			if (useSandboxAsHomeDir)
			{
				ObjectHandle handle = Activator.CreateInstanceFrom(
					domain,
					typeof(AssemblyMapper).Assembly.ManifestModule.FullyQualifiedName,
					typeof(AssemblyMapper).FullName);

				// as base directory will be different, we need a way to map load request for DotNetFiddle assmeblies from worker folder
				var mapper = handle.Unwrap() as AssemblyMapper;
				mapper.Init(basePath, sandboxFolder, mappings);
			}

			return domain;

		}

		private static string[] GetPartialTrustVisibleAssemblies()
		{
			return new[]
			       {
				       "System.Web.Extensions, PublicKey=0024000004800000940000000602000000240000525341310004000001000100B5FC90E7027F67871E773A8FDE8938C81DD402BA65B9201D60593E96C492651E889CC13F1415EBB53FAC1131AE0BD333C5EE6021672D9718EA31A8AEBD0DA0072F25D87DBA6FC90FFD598ED4DA35E44C398C454307E8E33B8426143DAEC9F596836F97C8F74750E5975C64E2189F45DEF46B2A2B1247ADC3652BF5C308055DA9",
					   "System.Web, PublicKey=002400000480000094000000060200000024000052534131000400000100010007d1fa57c4aed9f0a32e84aa0faefd0de9e8fd6aec8f87fb03766c834c99921eb23be79ad9d5dcc1dd9ad236132102900b723cf980957fc4e177108fc607774f29e8320e92ea05ece4e821c0a5efe8f1645c4c0c93c1ab99285d622caa652c1dfad63d745d6f2de5f17e5eaf0fc4963d261c8a12436518206dc093344d5ad293"
			       };
		}


		#region AssemblyMapper class

		public class AssemblyMapper : MarshalByRefObject
		{
			private static string _basePath;

			private static Dictionary<AssemblyName, string> _mappings;

			public void Init(string basePath, string sandboxFolder, Dictionary<AssemblyName, string> mappings)
			{
				_basePath = basePath;
				AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
				ExecuteInFullTrust(() => Environment.CurrentDirectory = sandboxFolder);
				_mappings = new Dictionary<AssemblyName, string>(mappings, new AssemblyNameComparer());
			}

			private static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args)
			{
				var aName = new AssemblyName(args.Name);
				var assemblyPath = Path.Combine(_basePath, aName.Name) + ".dll";
				string path = null;
	
				if (_mappings.ContainsKey(aName))
				{
					path = _mappings[aName];
				}

				if (path == null && File.Exists(assemblyPath))
				{
					path = assemblyPath;
				}

				if (path != null)
				{
					var assembly = Assembly.LoadFile(path);
					return assembly;
				}

				return null;
			}
		}

		private class AssemblyNameComparer : IEqualityComparer<AssemblyName>
		{

			public bool Equals(AssemblyName x, AssemblyName y)
			{
				return x.FullName == y.FullName;
			}

			public int GetHashCode(AssemblyName obj)
			{
				return obj.FullName.GetHashCode();
			}
		}

		#endregion

		public static bool IsInstanceInsideRunContainer()
		{
			if (AppDomain.CurrentDomain.FriendlyName.StartsWith("RunContainer.AppDomain_")) 
				return true;

			return false;
		}


		//From http://stackoverflow.com/questions/52797/how-do-i-get-the-path-of-the-assembly-the-code-is-in
		public static string GetAssemblyLocation(Assembly assembly)
		{
			if (assembly.IsDynamic)
			{
				return null;
			}

			string codeBase = assembly.CodeBase;
			UriBuilder uri = new UriBuilder(codeBase);

			string path = Uri.UnescapeDataString(uri.Path);
			path = Path.GetDirectoryName(path);
			path += "\\" + Path.GetFileName(assembly.Location);

			return path;
		}

		// move to some another Assemblies helper
		public static string GetAssemblyPathFromLoadedInDomain(string name)
		{
			var assemblies = AppDomain.CurrentDomain.GetAssemblies();
			var assembly = assemblies.FirstOrDefault(a => a.FullName == name);

			if (assembly == null) return null;

			return GetAssemblyLocation(assembly);
		}

		private static string GetSystemPaths(Assembly assembly)
		{
			string path = string.Format(@"C:\Windows\Microsoft.NET\Framework\{0}\{1}", assembly.ImageRuntimeVersion, assembly.ManifestModule.Name);
			return path;
		}

		public static void UnloadDomain(AppDomain domain)
		{
			// switch it from sandbox folder, as it will lock it
			var assemblyPath = GetAssemblyLocation(typeof(SandboxHelper).Assembly);
			Environment.CurrentDirectory = Path.GetDirectoryName(assemblyPath);
			AppDomain.Unload(domain);
		}
	}
}