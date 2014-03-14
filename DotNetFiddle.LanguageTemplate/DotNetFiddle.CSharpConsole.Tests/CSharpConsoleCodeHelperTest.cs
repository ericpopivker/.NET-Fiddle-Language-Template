using System.Collections.Generic;
using System.Linq;
using DotNetFiddle.Infrastructure;
using DotNetFiddle.Infrastructure.Extensions;
using NUnit.Framework;

namespace DotNetFiddle.CSharpConsole.Tests
{
	[TestFixture]
	public class CSharpConsoleCodeHelperTest
	{
		private CodeHelper _codeHelper;

		[SetUp]
		public void SetUp()
		{
			_codeHelper = new CSharpConsoleCodeHelper();
		} 

		[Test]
		public void Run_Works()
		{
			string code = @"
						using System;
					
						public class Program
						{
                            public void Main()
                            {
								int i=0; i++;
								Console.WriteLine(i);
                            }
						}";

			var result = _codeHelper.Run(code);

			Assert.IsTrue(result.IsSuccess);
			Assert.AreEqual("1", result.ConsoleOutput);
		}



		[Test]
		public void Run_WhenRunTimeException_ThenExceptionReturnedInResult()
		{
			string code = @"
using System;

public class Program
{
    public void Main()
    {
		throw new InvalidOperationException(""Invalid Operation occurred"");
    }
}";

			var result = _codeHelper.Run(code);

			Assert.IsFalse(result.IsSuccess);

			Assert.AreEqual("Invalid Operation occurred", result.RunTimeException.Message);

			var lines = result.RunTimeException.ParseStackTrace();
			var line1 = lines[0];

			Assert.IsNotEmpty(line1.FilePath);
			Assert.IsNotEmpty(line1.Line.ToString());
		}


		[Test]
		public void GetAutoCompleteItems_WhenSb_Works()
		{
			string codeInFile = @"using System; using System.Text;
				class Program
				{
					public void Main()
					{
						var sb= new StringBuilder();
						sb.";

			var autoCompleteItems = _codeHelper.GetAutoCompleteItems(codeInFile);
			Assert.IsTrue(autoCompleteItems.Any());
		}



		[Test]
		public void GetAutoCompleteItems_WhenNestedClass_Works()
		{
			string code = @"
using System;
		
public class FooClass
{
    public string Property1 {get; set}
	public string Method1(int arg1, string arg2)
	{
		return ""Hello World"";
    }
}		
	
class Program
{
	public void Main()
    {
		FooClass fooClass = new FooClass();
        fooClass.";

			var autoCompleteItems = _codeHelper.GetAutoCompleteItems(code);
			Assert.IsTrue(autoCompleteItems.Any());
			Assert.IsTrue(autoCompleteItems.Any(i => i.Name == "Property1"));
			Assert.IsTrue(autoCompleteItems.Any(i => i.Name == "Method1"));
		}


		[Test]
		public void ValidateCode_Missing_Semicolon_Returns_Error()
		{
			string code = @"using System";

			var result = _codeHelper.ValidateCode(code);

			Assert.IsTrue(result.Errors.Count > 0);
			Assert.AreEqual(0, result.Errors[0].WarningLevel);
		}


		[Test]
		public void ValidateCode_Dupe_Properties_Returns_Error()
		{
			string code = @"using System;
public class Test
{
  public string Prop1 {get; set;}
  public string Prop1 {get; set;}
  public string Prop2 {get; set;}
  public string Prop2 {get; set;}
}";

			var result = _codeHelper.ValidateCode(code);
		}



		[Test]
		public void ValidateCode_NewOverride_Returns_Warning()
		{
			string code = @"using System;
	
public class Parent1
{
	public void Test()
    {
		Console.WriteLine(""Hello World"");
    }
}

public class Child1 : Parent1
{
	public void Test()
    {
		Console.WriteLine(""Hello World"");
    }
}";

			var result = _codeHelper.ValidateCode(code);
		}

		[Test]
		public void Run_WhenDumpString_Works()
		{
			string code = @"using System;
public class Program
{
	public void Main()
    {
		string test1 = ""test string"";
		test1.Dump();
    }

}";


			var result = _codeHelper.Run(code);
			string expectedResult = @"Dumping object(String)
 test string";

			Assert.AreEqual(expectedResult, result.ConsoleOutput);
		}

		[Test]
		public void Run_WhenDump_ListOfStrings_Works()
		{
			string code = @"using System;
using System.Collections.Generic;

public class Program
{
	public void Main()
    {
		List<string> test2 = new List<string>(){""test list"", ""test list 2""};
		test2.Dump();
    }

}";


			var result = _codeHelper.Run(code);
			string expectedResult = @"Dumping object(System.Collections.Generic.List`1[String])
[
   test list
   ,
   test list 2
]";

			Assert.AreEqual(expectedResult, result.ConsoleOutput);
		}

		[Test]
		public void Run_WhenDump_ComplexObject_Works()
		{
			string code = @"using System;

public class Program
{
	public void Main()
    {
		Test obj = new Test();
		obj.Dump();
    }

	public class Test
	{
		public string Test1 {get;set;}

		public Test()
		{
			Test1 = ""test string"";
		}

	}

}";


			var result = _codeHelper.Run(code);
			string expectedResult = @"Dumping object(Test)
 Test1  : test string";

			Assert.AreEqual(expectedResult, result.ConsoleOutput);
		}

		// FB 3434
		[Test]
		public void Run_When_UsingSystemNumeric_Works()
		{
			string code = @"using System;
using System.Numerics;

public class Program
{
	public void Main()
    {
BigInteger val = new BigInteger(125);
Console.WriteLine(val);
    }
}";

			var result = _codeHelper.Run(code);
			string expectedResult = @"125";

			Assert.AreEqual(expectedResult, result.ConsoleOutput);
		}

		[Test]
		public void Run_WhenPermissionSet_Error()
		{
			string code = @"using System;
					
class Program
{
	public void Main()
    {
		new System.Security.PermissionSet(System.Security.Permissions.PermissionState.Unrestricted).Assert();
		Console.WriteLine(Environment.StackTrace);
    }
}";

			var result = _codeHelper.Run(code);
			string expectedResult = @"Using PermissionSet is not allowed due to security reasons";

			Assert.IsFalse(result.IsSuccess);
			Assert.AreEqual(expectedResult, result.FatalErrorMessage);

		}

		[Test]
		public void Run_WhenPermissionSetInReflection_Error()
		{
			string code = @"using System;
using System.Reflection;
using System.Security;

public class Program
{
	public void Main()
    {		
		string typeName = ""System.Security.Permissio"";
		typeName += ""nSet, mscorlib, Version=4.0.0.0, Culture=neutral, PublicKeyToken=b77a5c561934e089"";
		var type = Type.GetType(typeName);
		
		var param = System.Security.Permissions.PermissionState.Unrestricted;		
		var obj = Activator.CreateInstance(type, new object[]{param});		
		var method = type.GetMethod(""Assert"", BindingFlags.Instance | BindingFlags.Public);
		
		method.Invoke(obj, null);		
    }
}";

			var result = _codeHelper.Run(code);
			string expectedResult = @"Error in the application.";

			Assert.IsFalse(result.IsSuccess);
			Assert.AreEqual(expectedResult, result.RunTimeException.Message);
		}

		[Test]
		public void Run_WhenAwait_WithoutError()
		{
			string code = @"using System;
using System.Threading.Tasks;
					
public class Program
{
	public async void Main()
    {
		var content = await Test();
		Console.WriteLine(content);
    }
	
	public async Task<string> Test()
	{
		return await Task.FromResult(""Test string"");
	}
}";

			var result = _codeHelper.Run(code);
			string expectedResult = @"Test string";

			Assert.IsTrue(result.IsSuccess);
			Assert.AreEqual(expectedResult, result.ConsoleOutput);
			Assert.IsNull(result.CompilerErrors);
		}

		[Test]
		public void Run_WhenDynamic_WithoutError()
		{
			string code = @"using System;
					
public class Program
{
	public void Main()
    {
		dynamic d = 1;
        var testSum = d + 3;
        
        Console.WriteLine(testSum);
    }
}";

			var result = _codeHelper.Run(code);
			string expectedResult = @"4";

			Assert.IsTrue(result.IsSuccess);
			Assert.AreEqual(expectedResult, result.ConsoleOutput);
			Assert.IsNull(result.CompilerErrors);
		}

		[Test]
		public void Run_WhenConsoleReadLine_ConsoleInputIsRequested()
		{
			const string code = @"using System;
					
public class Program
{
	public static void Main()
    {
		Console.WriteLine(""Who are you?"");
		string name = Console.ReadLine();
    }
}";

			var runOpts = new ConsoleOrScriptRunOpts()
			{
				CodeBlock = new ConsoleOrScriptCodeBlock { CodeBlock = code },
				Language = Language.CSharp,
				ProjectType = ProjectType.Console,
			};

			RunResult result = CodeHelperTestUtils.RunInThread(_codeHelper, runOpts);

			Assert.IsTrue(result.IsSuccess);
			Assert.IsTrue(result.IsConsoleInputRequested);
			Assert.IsNull(result.CompilerErrors);
		}

		[Test]
		public void Run_WhenConsoleReadLine_InputTagIsInConsoleOutput()
		{
			const string code = @"using System;
					
public class Program
{
	public static void Main()
    {
		Console.WriteLine(""Who are you?"");
		string name = Console.ReadLine();
    }
}";
			var input = new List<string> { "Mike" };

			string expectedResult = string.Format(@"Who are you?
{0}0{1}", CodeHelper.ConsoleInputLineStart, CodeHelper.ConsoleInputLineEnd);

			var runOpts = new ConsoleOrScriptRunOpts()
			{
				CodeBlock = new ConsoleOrScriptCodeBlock { CodeBlock = code },
				Language = Language.CSharp,
				ProjectType = ProjectType.Console,
				ConsoleInputLines = input
			};

			var result = _codeHelper.Run(runOpts);

			Assert.IsTrue(result.IsSuccess);
			Assert.AreEqual(expectedResult, result.ConsoleOutput);
		}

		[Test]
		public void Run_WhenConsoleReadLine_InputTagAreIncrementedInConsoleOutput()
		{
			//============== first run

			const string code = @"using System;
					
public class Program
{
	public static void Main()
    {
		Console.WriteLine(""Who are you?"");
		string name = Console.ReadLine();
		Console.WriteLine(""Hello {0} , how old are You?"", name);
		string age = Console.ReadLine();
		Console.WriteLine(""Ok Mike , Mike is enough!"");
    }
}";
			List<string> input = null;
			var runOpts = new ConsoleOrScriptRunOpts()
			{
				CodeBlock = new ConsoleOrScriptCodeBlock { CodeBlock = code },
				Language = Language.CSharp,
				ProjectType = ProjectType.Console,
				ConsoleInputLines = input
			};
			string expectedResult = "Who are you?";

			RunResult result = CodeHelperTestUtils.RunInThread(_codeHelper, runOpts);

			Assert.IsTrue(result.IsSuccess);
			Assert.IsTrue(result.IsConsoleInputRequested);
			Assert.AreEqual(expectedResult, result.ConsoleOutput);
			Assert.IsNull(result.CompilerErrors);

			//================================== second run

			input = new List<string> { "Mike" };
			runOpts = new ConsoleOrScriptRunOpts()
			{
				CodeBlock = new ConsoleOrScriptCodeBlock { CodeBlock = code },
				Language = Language.CSharp,
				ProjectType = ProjectType.Console,
				ConsoleInputLines = input
			};
			expectedResult = string.Format(@"Who are you?
{0}0{1}
Hello Mike , how old are You?", CodeHelper.ConsoleInputLineStart, CodeHelper.ConsoleInputLineEnd);

			result = CodeHelperTestUtils.RunInThread(new CSharpConsoleCodeHelper(), runOpts);

			Assert.IsTrue(result.IsSuccess);
			Assert.IsTrue(result.IsConsoleInputRequested);
			Assert.AreEqual(expectedResult, result.ConsoleOutput);
			Assert.IsNull(result.CompilerErrors);

			//================================== third run

			input = new List<string> { "Mike", "27" };
			runOpts = new ConsoleOrScriptRunOpts()
			{
				CodeBlock = new ConsoleOrScriptCodeBlock { CodeBlock = code },
				Language = Language.CSharp,
				ProjectType = ProjectType.Console,
				ConsoleInputLines = input
			};
			expectedResult = string.Format(@"Who are you?
{0}0{1}
Hello Mike , how old are You?
{0}1{1}
Ok Mike , Mike is enough!", CodeHelper.ConsoleInputLineStart, CodeHelper.ConsoleInputLineEnd);

			result = CodeHelperTestUtils.RunInThread(new CSharpConsoleCodeHelper(), runOpts);

			Assert.IsTrue(result.IsSuccess);
			Assert.IsFalse(result.IsConsoleInputRequested);
			Assert.AreEqual(expectedResult, result.ConsoleOutput);
			Assert.IsNull(result.CompilerErrors);
		}

		[Test]
		public void Run_WhenConsoleRead_Fails()
		{
			const string code = @"using System;
					
public class Program
{
	public static void Main()
    {
		Console.WriteLine(""Who are you?"");
		int ch = Console.Read();
    }
}";
			var input = new List<string> { "Test input" };

			var runOpts = new ConsoleOrScriptRunOpts()
			{
				CodeBlock = new ConsoleOrScriptCodeBlock { CodeBlock = code },
				Language = Language.CSharp,
				ProjectType = ProjectType.Console,
				ConsoleInputLines = input
			};

			var result = _codeHelper.Run(runOpts);

			Assert.IsFalse(result.IsSuccess);
		}
	}
}
