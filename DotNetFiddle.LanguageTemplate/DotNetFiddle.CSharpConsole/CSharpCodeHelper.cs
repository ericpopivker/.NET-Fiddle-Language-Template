using System;
using System.Collections.Generic;
using System.Reflection;
using DotNetFiddle.Infrastructure;

using Roslyn.Compilers;
using Roslyn.Compilers.CSharp;
using Roslyn.Compilers.Common;
using Roslyn.Scripting;
using Roslyn.Scripting.CSharp;

namespace DotNetFiddle.CSharpConsole
{
	public abstract class CSharpCodeHelper : CSharpConsole.RoslynCodeHelper
	{
		public override Language Language
		{
			get { return Language.CSharp; }
		}

		protected override string GetUsingKeyword()
		{
			return "using";
		}

		protected override int GetNewKeywordCode()
		{
			return (int)SyntaxKind.ObjectCreationExpression;
		}

		protected override int GetUsingDirectiveCode()
		{
			return (int)SyntaxKind.UsingDirective;
		}

		protected override int GetArgumentCode()
		{
			return (int)SyntaxKind.Argument;
		}

		protected override int GetArgumentListCode()
		{
			return (int)SyntaxKind.ArgumentList;
		}

		protected override int GetIdentifierCode()
		{
			return (int)SyntaxKind.IdentifierToken;
		}

		protected override int GetQualifiedNameCode()
		{
			return (int)SyntaxKind.QualifiedName;
		}

		public override Type GetSyntaxTreeType()
		{
			return typeof(SyntaxTree);
		}

		public override CommonScriptEngine CreateSciptEngine()
		{
			return new ScriptEngine();
		}

		public override string GetUsingNamespaceLinePattern()
		{
			return "^\\s*using\\s*(\\S*)\\s*;";
		}
		

		public override string GetSecurityLevel1Attribute()
		{
			return "[assembly: System.Security.SecurityRules(System.Security.SecurityRuleSet.Level1)]";
		}

		public override MethodInfo GetMainMethodAndOwnerInstance(Assembly assembly, out object owner)
		{
			foreach (Type type in assembly.GetTypes())
			{
				BindingFlags flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
				MethodInfo methodInfo = type.GetMethod("Main", flags);

				if (methodInfo != null)
				{
					owner = assembly.CreateInstance(type.FullName);
					return methodInfo;
				}
			}

			owner = null;
			return null;
		}



		// http://stackoverflow.com/questions/13601412/compilation-errors-when-dealing-with-c-sharp-script-using-roslyn
		protected CommonSyntaxTree ParseSyntaxTree(string code, ParseOptions parseOptions)
		{
			var tree = SyntaxTree.ParseText(code, "", parseOptions);
			return tree;
		}

		public override CommonCompilation CreateCompilation(string compilatioName, CommonSyntaxTree[] syntaxTrees,
		                                                    List<MetadataReference> metadataReferences)
		{
			var csharpSyntaxTrees = new List<SyntaxTree>();
			foreach (var syntaxTree in syntaxTrees)
				csharpSyntaxTrees.Add((SyntaxTree) syntaxTree);

			Compilation compilation = Compilation.Create(
				compilatioName,
				syntaxTrees: csharpSyntaxTrees.ToArray(),
				references: metadataReferences);

			return compilation;
		}

		
	}
}
