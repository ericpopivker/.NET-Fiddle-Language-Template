using System;
using System.Diagnostics;
using System.IO;
using System.Web;


namespace DotNetFiddle.Infrastructure
{
	public class LocalResourcesUtils
	{
	


		private static string _tempWorkerDir;

		public static string GetSystemTempDir()
		{
			var path = Path.GetTempPath();

			return Path.Combine(path, "Workers");
		}

		public static string GetSystemTempWorkerDir(bool createIfNotExists = true)
		{
			if (!string.IsNullOrWhiteSpace(_tempWorkerDir))
			{
				return _tempWorkerDir;
			}

			var path = Path.GetTempPath();
			var workerId = Process.GetCurrentProcess().Id;

			_tempWorkerDir = Path.Combine(path, "Workers", workerId.ToString());

			if (createIfNotExists && !Directory.Exists(_tempWorkerDir))
				Directory.CreateDirectory(_tempWorkerDir);

			return _tempWorkerDir;
		}
	}
}
