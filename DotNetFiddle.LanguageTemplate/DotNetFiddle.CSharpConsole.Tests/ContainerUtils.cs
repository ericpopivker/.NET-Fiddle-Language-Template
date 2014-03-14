using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Remoting;

using DotNetFiddle.Infrastructure;
using DotNetFiddle.Infrastructure.Worker;
using DotNetFiddle.RunContainer;

namespace DotNetFiddle.CSharpConsole.Tests
{
	public static class ContainerUtils
	{

		private static Dictionary<AssemblyName, string> GetMappings()
		{
			var result = new Dictionary<AssemblyName, string>();

			Action<Assembly> addAssembly = (a) => result.Add(a.GetName(), a.ManifestModule.FullyQualifiedName);

			addAssembly(typeof(ContainerUtils).Assembly);
			addAssembly(typeof(RoslynCodeHelper).Assembly);
			addAssembly(typeof(SandboxHelper).Assembly);

			return result;
		}

		public static RunResult ExecuteCode(RunOptsBase opts, Type codeHelperType)
		{
			AppDomain sandboxDomain = SandboxHelper.CreateSandboxDomain(true, GetMappings());

			try
			{
				var containerType = typeof(Container);
				ObjectHandle handle = Activator.CreateInstanceFrom(
					sandboxDomain,
					containerType.Assembly.ManifestModule.FullyQualifiedName,
					containerType.FullName);

				var container = handle.Unwrap() as Container;


				container.InitWorkerSettings(
					WorkerConfiguration.Current.ID,
					WorkerConfiguration.Current.SandboxFolder,
					codeHelperType);

				var runResult = container.Run(opts);

				return runResult;

			}
			finally 
			{
				SandboxHelper.UnloadDomain(sandboxDomain);
			}

		}
	}
}