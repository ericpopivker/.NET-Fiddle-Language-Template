using System;
using System.Threading;
using DotNetFiddle.Infrastructure;

namespace DotNetFiddle.CSharpConsole.Tests
{
	public static class CodeHelperTestUtils
	{
		public static RunResult RunInThread(CodeHelper codeHelper, RunOptsBase runOpts)
		{
			RunResult result = null;
			try
			{
				codeHelper.RequestedConsoleInput += CodeHelper_RequestedConsoleInput;
				var newThread = new Thread(() => { result = codeHelper.Run(runOpts); });
				newThread.Start();
				newThread.Join(TimeSpan.FromSeconds(10));
			}
			finally
			{
				codeHelper.RequestedConsoleInput -= CodeHelper_RequestedConsoleInput;
			}
			return result;
		}

		public static void CodeHelper_RequestedConsoleInput(object sender, EventArgs eventArgs)
		{
			Thread.CurrentThread.Abort(new ConsoleInputRequest());
		}

	}
}
