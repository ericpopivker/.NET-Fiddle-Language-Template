using System;
using System.IO;
using System.Reflection;

namespace DotNetFiddle.Infrastructure
{
	public enum EnvironmentType
	{
		Dev = 1,
		QA = 2,
		Stable = 3,
		Prod = 4
	}

	public static class EnvSettings
	{
		public static EnvironmentType GetEnvType()
		{
			return EnvironmentType.Dev;
		}

		public static string GetExecutingAssemblyPath()
		{
			string codeBase = Assembly.GetExecutingAssembly().CodeBase;
			UriBuilder uri = new UriBuilder(codeBase);
			string path = Uri.UnescapeDataString(uri.Path);
			return Path.GetDirectoryName(path);
		}
	}
}
