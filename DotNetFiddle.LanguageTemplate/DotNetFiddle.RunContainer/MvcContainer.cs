using System;
using System.IO;
using System.Web;
using System.Web.Hosting;

using DotNetFiddle.Infrastructure;
using DotNetFiddle.Infrastructure.AspNet;
using DotNetFiddle.Infrastructure.AspNet.Host;
using DotNetFiddle.Infrastructure.Worker;

namespace DotNetFiddle.RunContainer
{
	/// <summary>
	/// This class shouldn't ever used in AppDomain of existing WebApplication, as it will crush it
	/// </summary>
	public class MvcContainer : Container
	{
		private static AspNetHost _aspNetHost;
		private static object _lockObj = new object();

		protected override void Initialize()
		{
			if (_aspNetHost == null)
			{
				lock (_lockObj)
				{
					if (_aspNetHost == null)
					{
						SandboxHelper.ExecuteInFullTrust(
							() =>
							{
								var aspNetRootFolder = WorkerConfiguration.Current.AspNetRootFolder;
								if (!aspNetRootFolder.EndsWith("\\"))
									aspNetRootFolder += "\\";

								CopySiteRoot(aspNetRootFolder);

								string virtualFolder = "/MvcPageAction/" + WorkerConfiguration.Current.ID + "/";

								_aspNetHost = new AspNetHost(aspNetRootFolder, virtualFolder);

								try
								{
									_aspNetHost.Start();
								}
								catch (Exception ex)
								{
									ex.ToString();
									throw;
								}
								HostingEnvironment.RegisterVirtualPathProvider(new FakeVirtualPathProvider());

								// we just do empty request to not existing file, so Asp.Net would initialize Http Application in full trust, and next request can be processed in sandbox
								StringWriter writer = new StringWriter();
								SimpleWorkerRequest worker = new SimpleWorkerRequest("static/Warmup.aspx", null, writer);
								HttpRuntime.ProcessRequest(worker);
								writer.Flush();

								string html = writer.ToString();
								html.ToString();
							});
					}
				}
			}
			base.Initialize();
		}

		protected override bool CheckCodeBlock(RunOptsBase opts, ref RunResult runResult)
		{
			if (opts.CodeBlock == null)
				opts.CodeBlock = new MvcCodeBlock
					{
						Model = string.Empty,
						View = string.Empty,
						Controller = string.Empty
					};

			var codeBlock = (MvcCodeBlock) opts.CodeBlock;

			return CheckCodeSizeLimit(codeBlock.Model, ref runResult) &&
					CheckCodeSizeLimit(codeBlock.Controller, ref runResult);
		}

		public static void CopySiteRoot(string aspNetRootFolder)
		{
			var assembly = typeof(MvcCodeHelper).Assembly;

			Action<string, string> copyFile = (path, resourceName) =>
			{
				if (File.Exists(path))
				{
					return;
				}
				using (var fileStream = File.Create(path))
				{
					using (var resource = assembly.GetManifestResourceStream(resourceName))
					{
						resource.CopyTo(fileStream);
					}
				}
			};

			var filePath = Path.Combine(aspNetRootFolder, "Global.asax");
			copyFile(filePath, "DotNetFiddle.Infrastructure.AspNet.SiteRoot.Global.asax");

			filePath = Path.Combine(aspNetRootFolder, "web.config");
			copyFile(filePath, "DotNetFiddle.Infrastructure.AspNet.SiteRoot.web.config");

			aspNetRootFolder = Path.Combine(aspNetRootFolder, "Views");
			if (!Directory.Exists(aspNetRootFolder))
				Directory.CreateDirectory(aspNetRootFolder);

			filePath = Path.Combine(aspNetRootFolder, "web.config");
			copyFile(filePath, "DotNetFiddle.Infrastructure.AspNet.SiteRoot.Views.Web.config");
		}
	}
}