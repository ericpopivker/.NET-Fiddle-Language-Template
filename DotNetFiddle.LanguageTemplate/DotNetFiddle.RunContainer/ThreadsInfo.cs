using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace DotNetFiddle.RunContainer
{
	public class ThreadsInfo : IComparable<ThreadsInfo>
	{
		public List<ProcessThread> Threads { get; set; }
		public int ThreadPoolWorker { get; set; }
		public int ThreadPoolIO { get; set; }
		
		public static ThreadsInfo Gather()
		{
			int workerThreads, ioThreads;
			ThreadPool.GetAvailableThreads(out workerThreads, out ioThreads);
			var threads = Process.GetCurrentProcess().Threads.OfType<ProcessThread>().ToList();

			return new ThreadsInfo() { ThreadPoolIO = ioThreads, ThreadPoolWorker = workerThreads, Threads = threads };
		}

		public int CompareTo(ThreadsInfo other)
		{
			if (this.Threads.Count < other.Threads.Count) return -1;
			if (this.Threads.Count > other.Threads.Count) return 1;

			if (this.ThreadPoolWorker < other.ThreadPoolWorker) return -1;
			if (this.ThreadPoolWorker > other.ThreadPoolWorker) return 1;

			if (this.ThreadPoolIO < other.ThreadPoolIO) return -1;
			if (this.ThreadPoolIO > other.ThreadPoolIO) return 1;

			return 0;
		}

		public static int CalculateDifference(ThreadsInfo x, ThreadsInfo y)
		{
			var delta = x.Threads.Count - y.Threads.Count + 
				x.ThreadPoolIO - y.ThreadPoolIO + 
				x.ThreadPoolWorker - y.ThreadPoolWorker;

			return delta;
		}
	}
}