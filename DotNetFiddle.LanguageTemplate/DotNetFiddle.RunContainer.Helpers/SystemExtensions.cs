using DotNetFiddle.RunContainer.Helpers;

namespace System
{
	public static class SystemExtensions
	{
		public static void Dump(this object obj)
		{
			Dumper dumper = new Dumper(10, Console.Out);
			dumper.Write(obj);
		}
	}
}