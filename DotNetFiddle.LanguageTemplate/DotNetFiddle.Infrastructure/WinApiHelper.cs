using System.Runtime.InteropServices;

namespace DotNetFiddle.Infrastructure
{
	public class WinApiHelper
	{
		[DllImport("kernel32.dll")]
		public static extern uint GetCurrentThreadId();
	}
}