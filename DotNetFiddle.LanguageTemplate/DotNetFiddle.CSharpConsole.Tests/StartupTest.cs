using System;
using System.IO;

using DotNetFiddle.Infrastructure;
using DotNetFiddle.Infrastructure.Worker;

using NUnit.Framework;

namespace DotNetFiddle.CSharpConsole.Tests
{
	[SetUpFixture]
	public class StartupTest
	{

		[SetUp]
		public void SetupContainer()
		{
			var currentPath = Path.GetDirectoryName(SandboxHelper.GetAssemblyLocation(typeof(StartupTest).Assembly));
			currentPath = Path.Combine(currentPath, "Sandbox");
			WorkerConfiguration.SetConfiguration(currentPath, Guid.NewGuid());
		}
	}
}