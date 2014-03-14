using DotNetFiddle.Infrastructure;
using Roslyn.Compilers;
using Roslyn.Compilers.Common;
using Roslyn.Compilers.CSharp;

namespace DotNetFiddle.CSharpConsole
{
	public class CSharpConsoleCodeHelper : CSharpCodeHelper
	{
		public override ProjectType ProjectType
		{
			get { return ProjectType.Console; }
		}


		// http://stackoverflow.com/questions/13601412/compilation-errors-when-dealing-with-c-sharp-script-using-roslyn
		public override CommonSyntaxTree ParseSyntaxTree(string code)
		{
			var options = new ParseOptions(CompatibilityMode.None, LanguageVersion.CSharp6, true,SourceCodeKind.Regular, null);
			var tree = ParseSyntaxTree(code, options);
			return tree;
		}


		private const string _codeSample = @"using System;
					
public class Program
{
	public static void Main()
	{
		Console.WriteLine(""Hello World"");
	}
}";

		public override string GetSampleStorageId()
		{
			return "CsCons";
		}

		public override CodeBlock GetSampleCodeBlock()
		{
			return new ConsoleOrScriptCodeBlock
			{
				CodeBlock = _codeSample
			};
		}

		public override ValidateCodeResult ValidateCode(string code)
		{
			// for code with 'await' we use usual compilation as Roslyn doesn't support async\await
			if (code.Contains("await ") || code.Contains("dynamic "))
				return ValidateCodeWithCompilation(code);

			return base.ValidateCode(code);
		}
	}
		
}
