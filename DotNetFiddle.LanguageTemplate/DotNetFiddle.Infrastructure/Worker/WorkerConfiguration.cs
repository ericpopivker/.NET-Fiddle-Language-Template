using System;
using System.IO;
using System.Security;
using System.Security.Permissions;

namespace DotNetFiddle.Infrastructure.Worker
{
	public class WorkerConfiguration
	{
		private const int _executionLimitTimeoutMs = 5000;

		private const int _executionMemoryLimitBytes = 1024 * 1024 * 10; // 10mb;

		private const  int _executionCodeMaxSize = 100000;

		private const int _executionOutputMaxSize = 100000;

		private const bool _allowReflectionMemberAccess = true;

		private const bool _allowAppDomainCaching = true;

		private const long _executionFileLimitBytes = 1024 * 1024; // 1MB;

		private Guid _id = Guid.NewGuid();

		private string _sandboxFolder;

		public int ExecutionLimitTimeoutMs
		{
			get
			{
				return _executionLimitTimeoutMs;
			}
		}

		public int ExecutionMemoryLimitBytes
		{
			get
			{
				return _executionMemoryLimitBytes;
			}
		}

		public long ExecutionFileLimitBytes
		{
			get
			{
				return _executionFileLimitBytes;
			}
		}

		public bool AllowAppDomainCaching
		{
			get
			{
				return _allowAppDomainCaching;
			}
		}

		public int ExecutionCodeMaxSize
		{
			get
			{
				return _executionCodeMaxSize;
			}
		}

		public int ExecutionOutputMaxSize
		{
			get
			{
				return _executionOutputMaxSize;
			}
		}

		public bool AllowReflectionMemberAccess
		{
			get
			{
				return _allowReflectionMemberAccess;
			}
		}

		public Guid ID
		{
			get
			{
				return this._id;
			}
		}

		public string SandboxFolder
		{
			get
			{
				return this._sandboxFolder;
			}
		}


		public static WorkerConfiguration Current { get; private set; }

		public static void SetConfiguration(string sandboxFolder, Guid? id = null)
		{
			if (!id.HasValue)
				id = Guid.NewGuid();

			Current = new WorkerConfiguration();
			Current._id = id.Value;
			Current._sandboxFolder = sandboxFolder;
		}
	}
}