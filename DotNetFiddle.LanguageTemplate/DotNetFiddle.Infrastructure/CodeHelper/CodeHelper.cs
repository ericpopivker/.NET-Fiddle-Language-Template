using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Security.Permissions;
using System.Text.RegularExpressions;
using System.Threading;
using DotNetFiddle.Infrastructure.Extensions;
using DotNetFiddle.Infrastructure.Worker;



namespace DotNetFiddle.Infrastructure
{

	public abstract class CodeHelper
	{
		protected LimitedStringWriter _consoleWriter;
		protected ConsoleStringReader _consoleReader;

		public event EventHandler StartingExecution;
		public event EventHandler FinishedExecution;
		public event EventHandler RequestedConsoleInput;


		#region Constraints

		public const long LimitAppDomainMemoryUsage = 1024*1024*100; // 100 mb
		public const long LimitProcessMemoryUsage = (long) (LimitAppDomainMemoryUsage*1.5); // 150 mb

		#endregion

		public const string ConsoleInputLineStart = "[ConsoleInputLine_";
		public const string ConsoleInputLineEnd = "]";

		static CodeHelper()
		{
			SandboxHelper.ExecuteInFullTrust(
				() =>
					{
						// we just cache path generation
						RunContainerHelpersAssemblyPath = typeof (SystemExtensions).Assembly.Location;
					});
		}

		public abstract Language Language { get; }

		public abstract ProjectType ProjectType { get; }

		public List<string> NuGetDllReferences { get; set; }

		public List<string> NuGetXmlFileReferences { get; set; }

		public List<string> ConsoleInputLines { get; set; } 		
		
		public abstract ValidateCodeResult ValidateCode(string code);

		public abstract List<AutoCompleteItem> GetAutoCompleteItems(string code, int? pos = null);

		public abstract TokenTypeResult GetTokenType(string code, int? pos = null);

		public abstract string GetSampleStorageId();

		public abstract CodeBlock GetSampleCodeBlock();

		public abstract string GetUsingNamespaceLinePattern();

		public abstract string GetSecurityLevel1Attribute();

		public abstract MethodInfo GetMainMethodAndOwnerInstance(Assembly assembly, out object owner);

		/// <summary>
		/// Initialization of CodeHelper that should be done before execution, like loading assembly into current AppDomain
		/// </summary>
		protected virtual void Initialize()
		{

		}

		protected CodeHelper()
		{
			NuGetDllReferences = new List<string>();
			NuGetXmlFileReferences = new List<string>();
			ConsoleInputLines = new List<string>();

			_consoleWriter = new LimitedStringWriter(WorkerConfiguration.Current.ExecutionOutputMaxSize);
			_consoleReader = new ConsoleStringReader();
			_consoleReader.RequestedConsoleInput += ConsoleStringReader_RequestedConsoleInput;
		}

		public void ConsoleStringReader_RequestedConsoleInput(object sender, EventArgs e)
		{
			RequestedConsoleInput(sender, e);
		}

	

		public RunOptsBase GetRunOpts(string code)
		{
			var runOpts = new ConsoleOrScriptRunOpts
				{
					CodeBlock = new ConsoleOrScriptCodeBlock {CodeBlock = code},
					Language = Language,
					ProjectType = ProjectType,
					NuGetDllReferences = NuGetDllReferences,
					ConsoleInputLines = ConsoleInputLines
				};

			if (runOpts.NuGetDllReferences == null)
				runOpts.NuGetDllReferences = new List<string>();

			return runOpts;
		}

		public RunResult Run(string code)
		{
			return Run(GetRunOpts(code));
		}

		public RunResult Run(RunOptsBase opts)
		{
			var result = new RunResult();
			result.IsSuccess = true;

			_consoleReader.InputLines = opts.ConsoleInputLines;

			if (!VerifyDeniedCodeBlock(opts, result))
			{
				return result;
			}

			ResolveEventHandler handler = (o, e) => CurrentDomain_AssemblyResolve(opts, e);

			TextWriter defaultConsoleOut = Console.Out;
			TextReader defaultConsoleIn = Console.In;

			SandboxHelper.ExecuteInFullTrust(
				() =>
				{
					Console.SetOut(_consoleWriter);
					Console.SetIn(_consoleReader);
				});

			try
			{
				AppDomain.CurrentDomain.AssemblyResolve += handler;

				switch (ProjectType)
				{
					case ProjectType.Console:
						RunConsole(opts, result);
						break;
					case ProjectType.Script:
						RunInteractive(opts, result);
						break;
					case ProjectType.Mvc:
						RunMvc(opts, result);
						break;
                        case ProjectType.Nancy:
				        RunNancy(opts, result);
                        break;
					default:
						throw new NotImplementedException();
				}
			}
			catch (ThreadAbortException ex)
			{
				var consoleInputRequest = ex.ExceptionState as ConsoleInputRequest;
				if (consoleInputRequest != null)
				{
					result.IsSuccess = true;
					result.IsConsoleInputRequested = true;
				}
				else
				{
					result.IsSuccess = false;

					var limit = ex.ExceptionState as LimitExceededException;
					if (limit != null)
					{
						result.FailureType = RunResultFailureType.FatalError;
						result.FatalErrorMessage = LimitExceededException.FormatMessage(limit.LimitType);
					}
				}

				SandboxHelper.ExecuteInFullTrust(Thread.ResetAbort);
			}
			finally
			{
				//Restore Console Out just in case
				SandboxHelper.ExecuteInFullTrust(
					() =>
					{
						Console.SetOut(defaultConsoleOut);
						Console.SetIn(defaultConsoleIn);
					});

				// unsubscribe
				AppDomain.CurrentDomain.AssemblyResolve -= handler;
			}

			return result;
		}

	    

	    protected bool VerifyDeniedCode(string code, RunResult result)
		{
			Verify.Argument.IsNotNull(code, "code");
			const string DeniedCode = "PermissionSet";

			if (code.IndexOf(DeniedCode, StringComparison.CurrentCultureIgnoreCase) == -1)
			{
				return true;
			}

			result.IsSuccess = false;
			result.FatalErrorMessage = "Using PermissionSet is not allowed due to security reasons";
			result.FailureType = RunResultFailureType.FatalError;

			return false;
		}

		protected virtual bool VerifyDeniedCodeBlock(RunOptsBase opts, RunResult result)
		{
			return VerifyDeniedCode(((ConsoleOrScriptCodeBlock) opts.CodeBlock).CodeBlock, result);
		}

		protected bool IsCompilationSucceed(CompilerResults compilerResults, RunResult result)
		{
			if (compilerResults.Errors.HasErrors)
			{
				result.IsSuccess = false;
				result.FailureType = RunResultFailureType.CompilerErrors;
				result.CompilerErrors = GetValidationErrorsFromCompilerErrors(compilerResults.Errors);
				return false;
			}
			return true;
		}

		private void RunConsole(RunOptsBase opts, RunResult result)
		{
			new PermissionSet(PermissionState.Unrestricted).Assert();

			CompilerResults compilerResults;
			compilerResults = CompileConsole(opts, 4);

			if (!IsCompilationSucceed(compilerResults, result))
			{
				PermissionSet.RevertAssert();
				return;
			}


			MethodInfo mainMethodInfo;
			object ownerInstance;

			mainMethodInfo = GetMainMethodAndOwnerInstance(compilerResults.CompiledAssembly, out ownerInstance);

			if (mainMethodInfo == null || !mainMethodInfo.IsPublic || !mainMethodInfo.DeclaringType.IsPublic)
			{
				result.IsSuccess = false;
				result.FailureType = RunResultFailureType.FatalError;
				result.FatalErrorMessage = "Public Main() method is required in a public class";
				return;
			}


			try
			{
				this.OnStartingExecution();
				PermissionSet.RevertAssert();

				//Add timer so doesn't execute for more then 5 secs
				var paramInfos = mainMethodInfo.GetParameters();
				mainMethodInfo.Invoke(ownerInstance, paramInfos.Select(pi => (object)null).ToArray());

				this.OnFinishedExecution();
			}
			catch (ThreadAbortException)
			{
				throw;
			}
			catch (Exception ex)
			{
				result.IsSuccess = false;
				result.FailureType = RunResultFailureType.RunTimeException;
				result.RunTimeException = new ExceptionInfo(ex.InnerException ?? ex);
			}
			finally
			{
				result.ConsoleOutput = _consoleWriter.ToString().TrimEnd();
			}
		}


		protected virtual List<ValidationError> GetValidationErrorsFromCompilerErrors(CompilerErrorCollection compilerErrors)
		{
			var validationErrors = new List<ValidationError>();
			if (compilerErrors == null)
				return validationErrors;

			for (int i = 0; i < compilerErrors.Count; i++)
			{
				CompilerError compilerError = compilerErrors[i];
				var error = ValidationError.CreateFromCompileError(compilerError);
				validationErrors.Add(error);
			}

			return validationErrors;
		}


		protected abstract void RunInteractive(RunOptsBase opts, RunResult result);

		
		public CompilerResults CompileConsole(RunOptsBase opts, int? warningLevel = null, bool loadAssembyToAppDomain = true)
		{
			return CompileCode(new List<string>() {((ConsoleOrScriptCodeBlock) opts.CodeBlock).CodeBlock}, warningLevel, loadAssembyToAppDomain);
		}

		protected void PrepareForCompile(IEnumerable<string> codeBlocks,
								int? warningLevel,
								bool loadAssembyToAppDomain, out CompilerParameters compilerParams, out List<string> codeItems)
		{
			compilerParams = new CompilerParameters();
			codeItems = new List<string>();

			//compilerParams.OutputAssembly = GetTemporaryAssemblyName();

			var gacDlls = new List<string>();
			foreach (var codeBlock in codeBlocks)
			{
				codeItems.Add(codeBlock);
				gacDlls.AddRange(this.GetGacDlls(codeBlock));
			}

			gacDlls = gacDlls.Distinct().ToList();

			foreach (var referenceDll in gacDlls)
				compilerParams.ReferencedAssemblies.Add(referenceDll);

			var nonGacDlls = GetNonGacDlls();
			foreach (var referencedDll in nonGacDlls)
				compilerParams.ReferencedAssemblies.Add(referencedDll);

			compilerParams.CompilerOptions = "/t:library " + GetConsoleCompilerOptions();
			compilerParams.GenerateInMemory = loadAssembyToAppDomain;

			if (warningLevel.HasValue)
				compilerParams.WarningLevel = warningLevel.Value;

			//http://msdn.microsoft.com/en-us/library/thxezb7y.aspx  
			//Also http://msdn.microsoft.com/en-us/library/13b90fz7(v=vs.110).aspx, but it seems like 4 is already default

			//http://stackoverflow.com/questions/875723/how-to-debug-break-in-codedom-compiled-code
			compilerParams.IncludeDebugInformation = true;

			// FB 3328. We switch Security mechanism for new library back to .Net 2.0, not .Net 4.0. Without it line numbers won't be displayed in stack trace....
			var level1Attribute = GetSecurityLevel1Attribute();
			codeItems.Add(level1Attribute);
		}

		protected CompilerResults CompileCode(
			IEnumerable<string> codeBlocks,
			int? warningLevel = null,
			bool loadAssembyToAppDomain = true)
		{
			string languageName = this.Language.ToString();
			if (this.Language == Language.VbNet)
				languageName = "Vb";

			CodeDomProvider codeCompiler = CodeDomProvider.CreateProvider(languageName);

			CompilerParameters compilerParams;
			List<string> codeItems;
			PrepareForCompile(codeBlocks, warningLevel, loadAssembyToAppDomain, out compilerParams, out codeItems);
			CompilerResults cr = codeCompiler.CompileAssemblyFromSource(compilerParams, codeItems.ToArray());
			return cr;
		}
			
		protected virtual string GetConsoleCompilerOptions()
		{
			return string.Empty;
		}

		public List<string> GetUsedNamespaces(string code)
		{
			string pattern = GetUsingNamespaceLinePattern();

			var regexOpts = RegexOptions.Multiline | RegexOptions.IgnoreCase;
			Regex regex = new Regex(pattern, regexOpts);

			var usingNamespaces = new List<string>();

            if (!String.IsNullOrEmpty(code))
            {
                var matches = regex.Matches(code);
                foreach (Match match in matches)
                {
                    usingNamespaces.Add(match.Groups[1].Value);
                }
            }

			return usingNamespaces;
		}

		protected virtual List<string> GetGacDlls(string code)
		{
			List<string> referencedDlls = NamespaceToDllMap.GetDlls(this.GetUsedNamespaces(code));
			
			// expressions require System.Core assemblies, so we can't understand when to add it, so we will add it for every run
			if (!referencedDlls.Contains("System.Core.dll"))
				referencedDlls.Add("System.Core.dll");

			// used by dynamic keyword
			if (!referencedDlls.Contains("Microsoft.CSharp.dll"))
				referencedDlls.Add("Microsoft.CSharp.dll");

			return referencedDlls;
		}


		private static string RunContainerHelpersAssemblyPath;

		/// <summary>
		/// List of full path for non GAC assemblies that we need to include
		/// </summary>
		protected virtual List<string> GetNonGacDlls()
		{
			var referencedDlls = new List<string>();

			referencedDlls.Add(RunContainerHelpersAssemblyPath);

			if (NuGetDllReferences != null)
				referencedDlls.AddRange(NuGetDllReferences);

			return referencedDlls;
		}

		
		[Serializable]
		public class ValidateCodeResult
		{
			public bool IsSuccess { get; set; }
			public List<ValidationError> Errors { get; set; }
		}



	
		protected void OnStartingExecution()
		{
			if (this.StartingExecution != null)
			{
				this.StartingExecution(this, EventArgs.Empty);
			}
		}

		protected void OnFinishedExecution()
		{
			if (this.FinishedExecution != null)
			{
				this.FinishedExecution(this, EventArgs.Empty);
			}
		}


		protected string GetTemporaryAssemblyName()
		{
			var temp = Path.GetTempPath();
			var fileName = Guid.NewGuid().ToString().Replace("-", string.Empty) + ".dll";
			return Path.Combine(temp, fileName);
		}


		private static Assembly CurrentDomain_AssemblyResolve(RunOptsBase opts, ResolveEventArgs args)
		{
			// when we have different version of the same assembly, we need to map it to that that we already loaded.
			// for example we use System.Core v4, but AutoMapper requiest System.Core v2.0.5, so we need to map it
			var ind = args.Name.IndexOf(",");
			if (ind == -1)
				return null;

			var result = SandboxHelper.ExecuteInFullTrust(
				() =>
				{
					string name = args.Name.Substring(0, ind);

					var assemblies = AppDomain.CurrentDomain.GetAssemblies();

					foreach (var assembly in assemblies)
					{
						var asName = assembly.GetName().Name;
						if (asName.Equals(name, StringComparison.CurrentCultureIgnoreCase))
						{
							return assembly;
						}
					}

					foreach (var reference in opts.NuGetDllReferences)
					{
						var fileName = Path.GetFileNameWithoutExtension(reference);
						if (string.Equals(fileName, name, StringComparison.CurrentCultureIgnoreCase))
						{
							string reference1 = reference;

							var assembly = Assembly.LoadFile(reference1);

							return assembly;
						}
					}

					return null;
				});

			return result;
		}


		protected ValidateCodeResult ValidateCodeWithCompilation(string code)
		{
			var result = new ValidateCodeResult();

			RunOptsBase runOpts = GetRunOpts(code);
			var compilerResults = CompileConsole(runOpts, 4, false);

			if (compilerResults.Errors.HasErrors)
			{
				result.IsSuccess = false;
				result.Errors = GetValidationErrorsFromCompilerErrors(compilerResults.Errors);
			}
			else
			{
				result.IsSuccess = true;
				// clean up compiled assembly
				if (File.Exists(compilerResults.PathToAssembly))
					File.Delete(compilerResults.PathToAssembly);
			}

			return result;
		}

		protected virtual void RunMvc(RunOptsBase opts, RunResult result)
		{
			throw new NotImplementedException();
		}

        private void RunNancy(RunOptsBase opts, RunResult result)
        {
            throw new NotImplementedException();
        }
	}
}