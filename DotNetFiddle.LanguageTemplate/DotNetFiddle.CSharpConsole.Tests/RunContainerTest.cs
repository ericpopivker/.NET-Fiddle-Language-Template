using DotNetFiddle.Infrastructure;
using NUnit.Framework;

namespace DotNetFiddle.CSharpConsole.Tests
{
	[TestFixture]
	public class RunContainerTest
	{

		/// <summary>
		/// Execute code block for CSharp Console code helper
		/// </summary>
		private RunResult ExecuteCSharpConsole(string codeBlock)
		{
			var opts = new ConsoleOrScriptRunOpts()
			{
				CodeBlock = new ConsoleOrScriptCodeBlock { CodeBlock = codeBlock },
				Language = Language.CSharp,
				ProjectType = ProjectType.Console
			};

			return ContainerUtils.ExecuteCode(opts, typeof(CSharpConsoleCodeHelper));
		}

		private const string HelloWorldCode = @"
using System;
					
public class Program
{
    public void Main()
    {
		Console.WriteLine(""Hello World"");
    }
}";

		[Test]
		public void ExecuteCode_WhenCSharpConsole_ThenWorks()
		{
			var result = ExecuteCSharpConsole(HelloWorldCode);

			Assert.IsTrue(result.IsSuccess);
			Assert.AreEqual("Hello World", result.ConsoleOutput);
		}

		private const string ExecutionTimeCode = @"
using System;

public class Program
{
	public void Main()
    {
		Console.WriteLine(""Hello World"");
		System.Threading.Thread.Sleep(10000);
    }
}";

		[Test]
		public void ExecuteCode_WhenOverTimeLimit_ThenReturnsExecutionTimeLimitExceeded()
		{
			var result = ExecuteCSharpConsole(ExecutionTimeCode);

			Assert.IsFalse(result.IsSuccess);
			Assert.AreEqual(RunResultFailureType.FatalError, result.FailureType);

			Assert.AreEqual("Execution time limit was exceeded", result.FatalErrorMessage);
		}

		private const string MemoryTimeCode = @"
using System;
					
public class Program
{
	public void Main()
    {
		Console.WriteLine(""Hello World"");

		string str = """";
		for (int i = 0; i < 10000; i++)
		{
			str += ""1234567890"";
		}		
		Console.WriteLine(str.Length);
    }
}";


		[Test]
		public void ExecuteCode_WhenOverMemoryLimit_ThenReturnsMemoryLimitExceeded()
		{
			var result = ExecuteCSharpConsole(MemoryTimeCode);

			Assert.IsFalse(result.IsSuccess);
			Assert.AreEqual(RunResultFailureType.FatalError, result.FailureType);

			Assert.AreEqual("Memory usage limit was exceeded", result.FatalErrorMessage);
		}

		string FileSystemCode = @"using System;
using System.IO;

public class Program
{
	public void Main()
    {
		File.WriteAllText(""test.txt"", ""Test value"");
		Console.WriteLine(File.ReadAllText(""test.txt""));
    }
}";

		[Test]
		public void ExecuteCode_WhenFileSystem_Works()
		{
			var result = ExecuteCSharpConsole(FileSystemCode);
			var expectedResult = "Test value";

			Assert.IsTrue(result.IsSuccess);
			Assert.AreEqual(expectedResult, result.ConsoleOutput);
		}

	}
}