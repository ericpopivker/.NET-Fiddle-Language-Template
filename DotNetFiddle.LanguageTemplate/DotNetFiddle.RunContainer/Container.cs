using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Security;
using System.Security.Permissions;
using System.Threading;
using System.Threading.Tasks;

using DotNetFiddle.Infrastructure;
using DotNetFiddle.Infrastructure.Worker;

using StackExchange.Profiling;

namespace DotNetFiddle.RunContainer
{
	[Serializable]
	public class Container : MarshalByRefObject
	{
		private Thread _executingThread;

		private uint _executingThreadID;

		private readonly CancellationTokenSource _tokenSource;

		private readonly ManualResetEvent _compilationCompleted = new ManualResetEvent(false);

		private ThreadsInfo _threadsBefore;

		private long _memoryUsedAfterCompilation = -1;
		private long _memoryUsedForExecution = -1;

		private DateTime _runAt;

		private TimeSpan _compileTime;

		private TimeSpan _executeTime;

		private TimeSpan _cpuTime;

		private string _sandboxFolder;
		public bool AreThreadsAlive { get; private set; }

		public bool IsUnhandledExceptionOccured { get; private set; }

		private Type _codeHelperType { get; set; }
		public Container()
		{
			_tokenSource = new CancellationTokenSource();
			AreThreadsAlive = false;
		}

		/// <summary>
		/// Init worker configuration in the new AppDomain
		/// </summary>
		public void InitWorkerSettings(Guid? workerId, string sandboxFolder, Type codeHelperType)
		{
			try
			{
				_sandboxFolder = sandboxFolder;
				SandboxHelper.ExecuteInFullTrust(
					() =>
					{
						WorkerConfiguration.SetConfiguration(sandboxFolder, workerId);
						AppDomain.CurrentDomain.UnhandledException += CurrentDomain_UnhandledException;
					});

				_codeHelperType = codeHelperType;
				ServicePointManager.DefaultConnectionLimit = 1;
				HttpWebRequest.DefaultMaximumResponseHeadersLength = 0;
				Initialize();
			}
			catch
			{
				throw;
			}
		}

		protected virtual void Initialize()
		{

		}

		protected virtual bool CheckCodeBlock(RunOptsBase opts, ref RunResult runResult)
		{
			if (opts.CodeBlock == null)
				opts.CodeBlock = new ConsoleOrScriptCodeBlock { CodeBlock = string.Empty };
			else if (((ConsoleOrScriptCodeBlock)opts.CodeBlock).CodeBlock == null)
				((ConsoleOrScriptCodeBlock)opts.CodeBlock).CodeBlock = string.Empty;

			return CheckCodeSizeLimit(((ConsoleOrScriptCodeBlock)opts.CodeBlock).CodeBlock, ref runResult);
		}

		protected bool CheckCodeSizeLimit(string code, ref RunResult runResult)
		{
			if (code.Length > WorkerConfiguration.Current.ExecutionCodeMaxSize)
			{
				runResult = new RunResult
				{
					IsSuccess = false,
					FailureType = RunResultFailureType.FatalError,
					FatalErrorMessage = LimitExceededException.FormatMessage(LimitType.CodeSize)
				};
				return false;
			}

			return true;
		}

		public RunResult Run(RunOptsBase opts)
		{
			RunResult result = null;
			if (!CheckCodeBlock(opts, ref result))
				return result;

			// we should copy it before running, becuase it can be changed during execution
			var sandboxFolder = _sandboxFolder;

			var codeHelper = SandboxHelper.ExecuteInFullTrust(() =>(CodeHelper)Activator.CreateInstance(_codeHelperType));
			codeHelper.NuGetDllReferences = opts.NuGetDllReferences;

			codeHelper.StartingExecution += this.CodeHelper_StartingExecution;
			codeHelper.FinishedExecution += this.CodeHelper_FinishedExecution;
			codeHelper.RequestedConsoleInput += this.CodeHelper_RequestedConsoleInput;

			_executingThread = new Thread(
				() =>
				{
					try
					{
						_runAt = DateTime.Now;
						result = this.ExecuteCodeBlock(opts, codeHelper);
					}
					finally
					{
						// in theory in can be null at this point if something bad happened....
						if (result == null)
						{
							result = new RunResult() { FailureType = RunResultFailureType.FatalError, IsSuccess = false };
						}

						result.Stats = SandboxHelper.ExecuteInFullTrust(() => GatherStatistics());
						_compilationCompleted.Set();
					}
				});

			_executingThread.Start();

			var monitoringTask = Task.Factory.StartNew(MonitorHealth, _tokenSource.Token);

			// wait for compilation. Just to be sure we have 15 seconds timeout
			_compilationCompleted.WaitOne(TimeSpan.FromSeconds(15));

			// if something happened during compilation, then we fire _compilationCompleted on exit, and result will be filled, so we just need to return it
			if (result != null)
			{
				return result;
			}

			// it will use some time for compilation
			// it can hungs for some unmanaged call like Console.ReadKey(), so we wait with timeout
			// we might need to rewrite it to ManualEvent that will be fired when CodeHelper starts execution
			_executingThread.Join(WorkerConfiguration.Current.ExecutionLimitTimeoutMs * 2);

			_tokenSource.Cancel();

			// we can't move it to new method, as it can be executed via reflection
			SandboxHelper.ExecuteInFullTrust(
				() =>
				{
					try
					{
						if (Directory.Exists(sandboxFolder))
						{
							foreach (var file in Directory.EnumerateFiles(sandboxFolder, "*", SearchOption.AllDirectories))
							{
								File.Delete(file);
							}
						}
					}
					catch
					{
						if (result != null)
							result.SandboxUnloadReason = SandboxUnloadReason.ClearDirFailed;
					}
				});

			return result;
		}

		protected virtual RunResult ExecuteCodeBlock(RunOptsBase opts, CodeHelper codeHelper)
		{
			RunResult result;
			result = codeHelper.Run(opts);
			return result;
		}

		private void CodeHelper_RequestedConsoleInput(object sender, EventArgs eventArgs)
		{
			SandboxHelper.ExecuteInFullTrust(() => _executingThread.Abort(new ConsoleInputRequest()));
		}

		private RunStats GatherStatistics()
		{
			_executeTime = DateTime.Now - _runAt - _compileTime;

			var threadsInfo = ThreadsInfo.Gather();
			// save CPU just per execution thread, not for whole process
			var procThread = threadsInfo.Threads.FirstOrDefault(t => t.Id == _executingThreadID);
			if (procThread != null)
				_cpuTime = procThread.TotalProcessorTime;

			// if compilation was unsuccessfull, then we need to return 0, as we don't know how many memory was used
			if (_memoryUsedForExecution < 0 && _memoryUsedAfterCompilation > 0)
			{
				_memoryUsedForExecution = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - _memoryUsedAfterCompilation;
			}

			return new RunStats()
				{
					RunAt = _runAt,
					CompileTime = _compileTime,
					ExecuteTime = _executeTime,
					MemoryUsage = _memoryUsedForExecution < 0 ? 0 : _memoryUsedForExecution,  // in case if there are compiler errors, than we don't measure memory
					CpuUsage = _cpuTime
				};
		}

		private void CodeHelper_FinishedExecution(object sender, EventArgs e)
		{
			new PermissionSet(PermissionState.Unrestricted).Assert();
			try
			{
				_memoryUsedForExecution = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - _memoryUsedAfterCompilation;
				Debug.WriteLine("Finished execution");

				// we just disable memory checking, as this thread checking is very memory expensive, and it will make false alarms
				_memoryUsedAfterCompilation = -1;
				ThreadsInfo threadInfo = ThreadsInfo.Gather();
				var delta = Math.Abs(ThreadsInfo.CalculateDifference(_threadsBefore, threadInfo));
				this.AreThreadsAlive = delta != 0;
			}
			catch (ThreadAbortException)
			{
				// it means that execution timeout was exceeded.
				// we might need to iterate over existing threads to Kill them somehow......
				throw;
			}
			finally
			{
				PermissionSet.RevertAssert();
			}
		}

		private void CodeHelper_StartingExecution(object sender, EventArgs e)
		{
			new PermissionSet(PermissionState.Unrestricted).Assert();

			// we start here monitoring thread
			_compilationCompleted.Set();
			_threadsBefore = ThreadsInfo.Gather();

			Debug.WriteLine("Initial thread info gathered");

			_executingThreadID = WinApiHelper.GetCurrentThreadId();
			_compileTime = DateTime.Now - _runAt;
			_memoryUsedAfterCompilation = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize;
			PermissionSet.RevertAssert();

		}


		private void MonitorHealth()
		{
			Debug.WriteLine("MonitorHealth entered");
			_compilationCompleted.WaitOne(20);
			Debug.WriteLine("MonitorHealth started");
			new PermissionSet(PermissionState.Unrestricted).Assert();

			var timestamp = DateTime.Now;
			var isDebugActive = Debugger.IsAttached;

			// we save it before instead of using in while just in case as they can be changed during execution if we have MemberAccess permissions
			var executionTimeout = WorkerConfiguration.Current.ExecutionLimitTimeoutMs;
			var memoryLimit = WorkerConfiguration.Current.ExecutionMemoryLimitBytes;
			var sandboxFolder = _sandboxFolder;
			var sandboxSizeLimit = WorkerConfiguration.Current.ExecutionFileLimitBytes;
			int delayedTime = 0;

			const int SandboxCheckingTime = 200; // ms
			const int Delay = 20; // ms

			while (!_tokenSource.IsCancellationRequested)
			{
				if (!isDebugActive && (DateTime.Now - timestamp).TotalMilliseconds >= executionTimeout)
				{
					_executingThread.Abort(new LimitExceededException(LimitType.ExecutionTime));
					break;
				}

				//Debug.WriteLine(AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize);
				if (_memoryUsedAfterCompilation >= 0)
				{
					_memoryUsedForExecution = AppDomain.CurrentDomain.MonitoringTotalAllocatedMemorySize - _memoryUsedAfterCompilation;
					if (!isDebugActive && _memoryUsedForExecution >= memoryLimit)
					{
						_executingThread.Abort(new LimitExceededException(LimitType.MemoryUsage));
						break;
					}
				}

				// check each 200ms instead of 20ms
				if ((delayedTime % SandboxCheckingTime) == 0 && !string.IsNullOrWhiteSpace(sandboxFolder))
				{
					var dirSize = GetDirectorySize(new DirectoryInfo(sandboxFolder));
					if (dirSize >= sandboxSizeLimit)
					{
						_executingThread.Abort(new LimitExceededException(LimitType.DirSize));
						break;
					}
				}
				// delay
				Thread.Sleep(Delay);
				delayedTime += Delay;
			}
		}

		private long GetDirectorySize(DirectoryInfo dirInfo)
		{
			long size = 0;
			foreach (var directoryInfo in dirInfo.GetDirectories())
			{
				size += GetDirectorySize(directoryInfo);
			}

			foreach (var fileInfo in dirInfo.GetFiles())
			{
				size += fileInfo.Length;
			}

			return size;
		}

		public static void ConfigureEnvironment()
		{
			var procCount = Environment.ProcessorCount;
			ThreadPool.SetMaxThreads(100, 100);
			ThreadPool.SetMinThreads(procCount, procCount);
		}

		private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
		{
			IsUnhandledExceptionOccured = true;
		}

	}
}
