using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Security;
using System.Security.Permissions;
using System.Threading;
using DotNetFiddle.Infrastructure;
using Roslyn.Compilers;
using Roslyn.Compilers.Common;
using Roslyn.Scripting;

namespace DotNetFiddle.CSharpConsole
{

	public abstract class RoslynCodeHelper : CodeHelper
	{

		public abstract Type GetSyntaxTreeType();

		public abstract CommonScriptEngine CreateSciptEngine();

		public abstract CommonSyntaxTree ParseSyntaxTree(string code);

		public abstract CommonCompilation CreateCompilation(string compilatioName, CommonSyntaxTree[] syntaxTrees,
		                                                    List<MetadataReference> matadataReferences);



		//Used for AutoComplete items
		protected abstract int GetNewKeywordCode();

		protected abstract int GetUsingDirectiveCode();

		protected abstract int GetArgumentCode();

		protected abstract int GetArgumentListCode();

		protected abstract int GetIdentifierCode();

		protected abstract int GetQualifiedNameCode();

		protected abstract string GetUsingKeyword();


		private const string ErrorWhenCompileConsoleProjectAsScript = "error BC35000:";

		protected override void RunInteractive(RunOptsBase opts, RunResult result)
		{
			new PermissionSet(PermissionState.Unrestricted).Assert();

			CommonScriptEngine scriptEngine = this.CreateSciptEngine();
			Session session = scriptEngine.CreateSession();
			var codeBlock = (ConsoleOrScriptCodeBlock) opts.CodeBlock;
			var referencedDlls = this.GetGacDlls(codeBlock.CodeBlock);
			foreach (var referenceDll in referencedDlls)
				session.AddReference(referenceDll);

			var libs = GetNonGacDlls();

			foreach (var path in libs)
			{
				session.AddReference(path);
			}

			Submission<object> submission;
			try
			{
				// we compile code there
				submission = session.CompileSubmission<object>(codeBlock.CodeBlock);
			}
			catch (ThreadAbortException)
			{
				throw;
			}
			catch (Exception ex)
			{
				if (!string.IsNullOrEmpty(ex.Message) && ex.Message.Contains(ErrorWhenCompileConsoleProjectAsScript))
				{/*Case 4067: DotNetFiddle throws exception on VbNet Script
				  * https://entech.fogbugz.com/default.asp?4067#31607
				  * This issue occurs, when user is trying to compile VB.Net Console project as VB.Net Script project.
				  * So, there is main entry point 'Module Sub Main' in snippet.
				  * Then Roslyn throws following exception "(3) : error BC35000: Requested operation is not available because the runtime library function 'Microsoft.VisualBasic.CompilerServices.StandardModuleAttribute..ctor' is not defined."
				  * In same case for C#, Roslyn just ignores 'console' code.
				  * So for VB.Net case we just return 'success' and empty string.
				  */
					result.IsSuccess = true;
					result.ConsoleOutput = "";
				}
				else
				{
					ValidateCodeResult validateCodeResult = ValidateCode(codeBlock.CodeBlock);

					result.IsSuccess = false;
					result.FailureType = RunResultFailureType.CompilerErrors;
					result.CompilerErrors = validateCodeResult.Errors;
				}

				if (result.CompilerErrors == null)
				{
					result.CompilerErrors = new List<ValidationError> {ValidationError.CreateFromException(ex)};
				}

				TryCleanRoslynCacheHack();
				PermissionSet.RevertAssert();
				return;
			}


			object execResult = null;
			try
			{
				this.OnStartingExecution();
				
				PermissionSet.RevertAssert();

				execResult = submission.Execute();

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
				result.RunTimeException = new ExceptionInfo(ex);
			}
			finally
			{
				result.ConsoleOutput = _consoleWriter.ToString().TrimEnd();

				if (execResult != null)
				{
					result.ConsoleOutput += Environment.NewLine;
					result.ConsoleOutput += "[Return value]: " + execResult;
				}

				// don't need it as we modified Roslyn assemblies and made fix in them
				// TryCleanRoslynCacheHack();
			}
		}

		/// <summary>
		/// It's a hack to manually Dispose Roslyns cache because GC fails with permissions exception.
		/// </summary>
		/// <remarks>
		/// 	Currently Roslyn's MetadataCache stores AssembliesFromFiles property that stores information about object metadata. 
		///		But it doesn't clean it, so it will be cleaned by GC. but the problem with GC that it will be executed with lower permissions that it needs
		///		AssembliesFromFiles uses WinApi to create MemoryMappedFile
		/// </remarks>
		private static void TryCleanRoslynCacheHack()
		{
			if (!SandboxHelper.IsInstanceInsideRunContainer())
				return;


			// increase to FullTrust
			new PermissionSet(PermissionState.Unrestricted).Assert();

			var type = Type.GetType("Roslyn.Compilers.MetadataCache, Roslyn.Compilers");
			var property = type.GetProperty("AssembliesFromFiles", BindingFlags.Static | BindingFlags.NonPublic);
			var dict = property.GetValue(null) as IDictionary;

			if (dict == null)
			{
				return;
			}

			if (dict.Count != 0)
			{
				var caches = dict.Values;
				FieldInfo weakReferenceField = null;
				PropertyInfo targetProp = null;
				foreach (var cache in caches)
				{
					// init it
					if (weakReferenceField == null)
					{
						weakReferenceField = cache.GetType().GetField("Metadata");
						var tmp = weakReferenceField.GetValue(cache);

						targetProp = tmp.GetType().GetProperty("Target", BindingFlags.Instance | BindingFlags.NonPublic);
					}

					var weakReference = weakReferenceField.GetValue(cache);

					var obj = targetProp.GetValue(weakReference);
					((IDisposable) obj).Dispose();
				}
			}
			dict.Clear();
		}

		

		//From http://www.dotnetexpertguide.com/2011/10/c-sharp-syntax-checker-aspnet-roslyn.html
		public override ValidateCodeResult ValidateCode(string code)
		{
			var result = new ValidateCodeResult();
			result.IsSuccess = true;

			CommonSyntaxTree tree = this.ParseSyntaxTree(code);

			var syntaxDiagnostics = tree.GetDiagnostics().ToList();

			var semanticTree = this.GetSemanticModelFromSyntaxTree(tree, code);
			var semanticDiagnostics = semanticTree.GetDiagnostics().ToList();

			int numLines = code.Split('\n').Length;

			this.AddErrorsFromDiagnostics(syntaxDiagnostics, numLines, result);
			this.AddErrorsFromDiagnostics(semanticDiagnostics, numLines, result, true);

			return result;
		}

		private void AddErrorsFromDiagnostics(List<CommonDiagnostic> diagnostics, int numLines,
		                                      ValidateCodeResult result,
		                                      bool checkForDupes = false)
		{
			if (diagnostics == null)
				return;

			if (diagnostics.Any())
			{
				if (result.Errors == null)
					result.Errors = new List<ValidationError>();

				foreach (CommonDiagnostic diag in diagnostics)
				{
					var error = ValidationError.CreateFromCommonDiagnostic(diag);

					//On last code line for interactive project don't send back 
					if (this.CanSkipValidationError(error, numLines))
						continue;

					if (checkForDupes && this.IsValidationErrorAlreadyInList(error, result.Errors))
						continue;

					result.Errors.Add(error);
				}
			}
		}

		private bool CanSkipValidationError(ValidationError error, int numLines)
		{
			//On last code line for interactive project - if missing ; - not really an error
			if (this.ProjectType == ProjectType.Script
			    && this.Language == Language.CSharp
			    && error.Line == numLines - 1
			    && error.ErrorNumber == "CS1002")
				return true;

			return false;
		}


		private bool IsValidationErrorAlreadyInList(ValidationError error, List<ValidationError> errors)
		{
			var findError =
				errors.FirstOrDefault(
					e => e.Line == error.Line && e.Column == error.Column && e.ErrorNumber == error.ErrorNumber);

			if (findError != null)
				return true;

			return false;

		}
        /// <summary>
        /// 
        /// </summary>
        /// <param name="code"></param>
        /// <param name="pos"></param>
        /// <returns></returns>
		public override List<AutoCompleteItem> GetAutoCompleteItems(string code, int? pos = null)
		{
			var syntaxTree = this.ParseSyntaxTree(code);
			var semanticModel = this.GetSemanticModelFromSyntaxTree(syntaxTree, code, true);

			bool isStaticContext = false;
			bool appendDefaultNamespaces = true;

			var position = GetCursorPosition(code, pos);

			var token = (CommonSyntaxToken) syntaxTree.GetRoot().FindToken(position);

			var synNode = token.Parent;

			var forUsing = GetParentByKind(synNode, GetUsingDirectiveCode());
			var forNew = GetParentByKind(synNode, GetNewKeywordCode());
			var errorType = false;

			if (token.Value.Equals(")") && token.Parent.Kind == GetArgumentListCode())
			{
				synNode = token.Parent.Parent;
				
				//handle inline declaration
				forNew = false;
			}


			ITypeSymbol classType = semanticModel.GetTypeInfo(synNode).Type;

			if (classType != null && classType.TypeKind == CommonTypeKind.Error)
			{
				if (!forNew)
					errorType = true;
				classType = null;
			}
			else if (classType != null)
			{
				var symbol = semanticModel.GetSymbolInfo(synNode).Symbol;
				if (symbol is INamedTypeSymbol)
				{
					isStaticContext = true;
					appendDefaultNamespaces = false;
				}

				if (symbol is ILocalSymbol)
				{
					appendDefaultNamespaces = false;
				}
			}

			var autoCompleteItems = new List<AutoCompleteItem>();

			if (errorType && !forUsing)
				return autoCompleteItems;

			var isNamespace = synNode.Parent.Kind == GetQualifiedNameCode();

			
			//check the namespace 
			var namespaceSymbolInfo = semanticModel.GetSymbolInfo(synNode);

			var namespaces = GetNamespaces(appendDefaultNamespaces, namespaceSymbolInfo);

			if (classType == null && (forUsing || isNamespace))
			{
				var usingNamespace = isNamespace ? synNode.Parent.ToFullString().Split(new[] {'\r', '\n'}).First() : ""; 
				if(namespaces.Count(n => n.StartsWith(usingNamespace)) > 1)
				{
					foreach (var ns in namespaces)
					{
						string innerNameSpace = null;
						if (namespaceSymbolInfo.Symbol != null)
							innerNameSpace =
								ns.Replace(namespaceSymbolInfo.Symbol.ToDisplayString(), "").TrimStart('.').Split('.').First();
						else
							innerNameSpace = ns.Replace(synNode.Parent.ToFullString(), "").TrimStart('.').Split('.').First();

						if (innerNameSpace.Length > 0)
						{
							autoCompleteItems.Add(new AutoCompleteItem()
								{
									IsStatic = false,
									Name = innerNameSpace,
									ItemType = AutoCompleteItemType.Namespace
								});
						}

					}
				}
			}

			if (!forUsing)
			{
				var symbols = semanticModel.LookupSymbols(position, 
														container: classType ?? (INamespaceOrTypeSymbol) namespaceSymbolInfo.Symbol,
														options: forNew ? CommonLookupOptions.NamespacesOrTypesOnly : CommonLookupOptions.IncludeExtensionMethods);
				autoCompleteItems.AddRange(GetAutoCompleteItemsFromSymbols(symbols, forNew, isStaticContext));

				if (!forNew && classType == null && !isNamespace && !isStaticContext)
				{
					//add static items
					autoCompleteItems.AddRange(GetAutoCompleteItemsFromSymbols(symbols, false, true));
				}
			}

			return autoCompleteItems.Distinct(new AutoCompleteItemEqualityComparer()).OrderBy(i => i.Name).ToList();
		}

		public override TokenTypeResult GetTokenType(string code, int? pos = null)
		{
			var result = new TokenTypeResult();

			var syntaxTree = this.ParseSyntaxTree(code);
			var semanticModel = this.GetSemanticModelFromSyntaxTree(syntaxTree, code, true);

			var position = GetCursorPosition(code, pos);

			var token = (CommonSyntaxToken)syntaxTree.GetRoot().FindToken(position);

			var synNode = token.Parent;

			var forUsing = GetParentByKind(synNode, GetUsingDirectiveCode());
			
			if (!forUsing)
			{

				var argumentList = FindArgumentListRecursive(token.Parent);
				if (argumentList != null)
				{
					result.IsInsideArgumentList = true;
					result.PreviousArgumentListTokenTypes = argumentList
						.ChildNodesAndTokens()
						.Where(t => t.Kind == GetArgumentCode())
						.Select(t => semanticModel.GetTypeInfo(syntaxTree.GetRoot().FindToken(t.AsNode().Span.Start).Parent).Type)
						.Select(t => t != null ? t.Name : null)
						.ToArray();
					result.RawArgumentsList = argumentList.ToString();
					result.ParentLine = argumentList.GetLocation().GetLineSpan(true).StartLinePosition.Line;
					result.ParentChar = argumentList.GetLocation().GetLineSpan(true).StartLinePosition.Character;
				}

				ITypeSymbol classType = semanticModel.GetTypeInfo(synNode).Type;

				if (classType != null)
				{
					result.Type = classType.Name;
				}
			}

			return result;
		}

		private CommonSyntaxNode FindArgumentListRecursive(CommonSyntaxNode node)
		{
			if(node == null) 
				return null;

			if (node.Kind == GetArgumentListCode()) return node;

			return FindArgumentListRecursive(node.Parent);
		}


		private bool GetParentByKind(CommonSyntaxNode node, int kind)
		{
			if (node.Kind == kind)
				return true;
			if (node.Parent != null)
				return GetParentByKind(node.Parent, kind);
			else
				return false;
		}

		private List<string> GetNamespaces(bool appendDefaultNamespaces, CommonSymbolInfo namespaceSymbolInfo)
		{
			var namespaces = new List<string>();

			if (appendDefaultNamespaces)
				namespaces.AddRange(NamespaceToDllMap.Map.Keys);

			//NuGet namespaces
			namespaces.AddRange(GetAssemblyNameSpaces(this.NuGetDllReferences));

			if (namespaceSymbolInfo.Symbol != null && namespaceSymbolInfo.Symbol.Kind == CommonSymbolKind.Namespace)
			{
				var symbolNameSpaces = new List<string>();

				symbolNameSpaces.AddRange(
					GetAssemblyNameSpaces(
						((INamespaceSymbol) namespaceSymbolInfo.Symbol).ConstituentNamespaces.Select(
							n => n.ContainingAssembly.Identity.Location)));

				symbolNameSpaces.AddRange(namespaces);

				namespaces =
					symbolNameSpaces.Distinct()
									.Where(n => n != null && n.StartsWith(namespaceSymbolInfo.Symbol.ToDisplayString()))
									.ToList();
			}

			return namespaces;
		}

		private static List<string> GetAssemblyNameSpaces(IEnumerable<string> filePaths)
		{
			List<string> namespaces;

			try
			{
				/*If we will load assemblies one by one and after each load will call .GetTypes,
				 * there can be crash, if some type will rely on other type from another assembly, 
				 * that we have not loaded yet.
				 * As in case https://entech.fogbugz.com/default.asp?3895#31617
				 * where some type within Stacky.dll was rely on other from Newtonsoft.Json
				 */
				var assemblies = filePaths.Select(Assembly.LoadFrom).ToList();

				namespaces = assemblies.SelectMany(a => a.GetTypes())
										.Where(t => t != null) //In some cases returns null
										.Select(t => t.Namespace)
										.Distinct()
										.Where(n => n != null) //Need null check since some classes don't have namespace
										.ToList();
			}
			catch (ReflectionTypeLoadException ex)
			{
				Debug.WriteLine("GetAssemblyNameSpaces : EXCEPTION=" + ex.Message);
				namespaces = new List<string>();
			}


			return namespaces;
		}

		private int GetCursorPosition(string code, int? pos)
		{
			if (pos == null)
				pos = code.Length;

			var codeFragment = pos < code.Length ? code.Substring(0, pos.Value) : code;

			pos = (pos < code.Length ? pos : code.Length) - 1;

			var dotPos = codeFragment.LastIndexOf('.') - 1;
			var spacePos = codeFragment.LastIndexOf(' ') -1;
			var openBracketPos = codeFragment.LastIndexOf('(');
			var closeBracketPos = codeFragment.LastIndexOf(')');

			if (openBracketPos > closeBracketPos && openBracketPos > dotPos && openBracketPos > spacePos)
			{
				return pos.Value;
			}
			
			if (dotPos > spacePos)
				pos = dotPos;
			
			return pos.Value;
		}


		private string _systemXmlFilesDir;
		private MetadataReference CreateMetadataReference(string assemblyDisplayName, bool includeDocumentation)
		{
			string assemblyFullPath = FileResolver.Default.ResolveAssemblyName(assemblyDisplayName);
			string xmlFile = Path.Combine(_systemXmlFilesDir, assemblyDisplayName + ".xml");

			RoslynDocumentationProvider documentationProvider = null;
			if (includeDocumentation)
				documentationProvider = new RoslynDocumentationProvider(xmlFile);

			return new MetadataFileReference(assemblyFullPath, MetadataReferenceProperties.Assembly, documentationProvider);
		}

		private ISemanticModel GetSemanticModelFromSyntaxTree(CommonSyntaxTree syntaxTree, string code, bool includeDocumentation = false)
		{
			_systemXmlFilesDir = GetSystemXmlFilesDir();

			MetadataReference mscorlib = CreateMetadataReference("mscorlib", includeDocumentation);
			var metaDllReferences = new List<MetadataReference> {mscorlib};

			if (this.Language == Language.VbNet)
			{
				//Need to add vb or getting error
				//Requested operation is not available because the runtime library function 'Microsoft.VisualBasic.CompilerServices.StandardModuleAttribute..ctor' is not defined.
				MetadataReference vb = CreateMetadataReference("Microsoft.VisualBasic", includeDocumentation);
				metaDllReferences.Add(vb);
			}


			//Eric: this doesn't seem to work so using mscorlib only for now.

			List<string> gacDlls = GetGacDlls(code);
			foreach (string dllName in gacDlls)
			{
				string dllNameWithoutExtension = dllName.Substring(0, dllName.Length - 4); //remove .dll

				MetadataReference metaRef = CreateMetadataReference(dllNameWithoutExtension, includeDocumentation);
				metaDllReferences.Add(metaRef);

			}

			Dictionary <string, string> nugGetXmlFileNameToPath = new Dictionary<string, string>();

			if (includeDocumentation)
				foreach (var nuGetxmlFilePath in NuGetXmlFileReferences)
				{
					string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(nuGetxmlFilePath);
					nugGetXmlFileNameToPath[fileNameWithoutExtension] = nuGetxmlFilePath;
				}


			foreach (var path in GetNonGacDlls())
			{
				string fileName = Path.GetFileNameWithoutExtension(path);

				RoslynDocumentationProvider documentationProvider = null;
				if (includeDocumentation && nugGetXmlFileNameToPath.ContainsKey(fileName))
					documentationProvider = new RoslynDocumentationProvider(nugGetXmlFileNameToPath[fileName]);

				var reference = new MetadataFileReference(path, MetadataReferenceProperties.Assembly, documentationProvider);
				metaDllReferences.Add(reference);
			}


			
			//http://msdn.microsoft.com/en-us/vstudio/hh500769.aspx#Toc306015688

			CommonCompilation compilation = this.CreateCompilation("Compilation_For_AutoComplete",
			                                                       syntaxTrees: new[] {syntaxTree},
			                                                       matadataReferences: metaDllReferences);

			ISemanticModel semanticModel = compilation.GetSemanticModel(syntaxTree);
			return semanticModel;
		}

		private string GetSystemXmlFilesDir()
		{
			string programFilesDir;
			//From http://stackoverflow.com/questions/194157/c-sharp-how-to-get-program-files-x86-on-windows-vista-64-bit
			if (8 == IntPtr.Size || (!String.IsNullOrEmpty(Environment.GetEnvironmentVariable("PROCESSOR_ARCHITEW6432"))))
			{
				programFilesDir=Environment.GetEnvironmentVariable("ProgramFiles(x86)");
			}
			else
			{
				programFilesDir =Environment.GetEnvironmentVariable("ProgramFiles");
			}

			//Ex C:\Program Files (x86)\Reference Assemblies\Microsoft\Framework\.NETFramework\v4.5

			string dir = Path.Combine(programFilesDir, "Reference Assemblies\\Microsoft\\Framework\\.NETFramework\\v4.5");
			return dir;
		}


		private static IEnumerable<AutoCompleteItem> GetAutoCompleteItemsFromSymbols(ReadOnlyArray<ISymbol> symbols,
		                                                                             bool forNewKeyword, bool isStaticContext)
		{
			var autoCompleteItemsQuery = symbols.ToList().AsParallel();

			//if for new keyowrd - get only named types or namespaces
			//if in staticcontext - get static members only
			if (forNewKeyword)
			{
				autoCompleteItemsQuery =
					autoCompleteItemsQuery.Where(
						s =>
						!s.IsStatic && !s.IsVirtual && !s.IsAbstract &&
						(s.Kind == CommonSymbolKind.NamedType || s.Kind == CommonSymbolKind.Namespace));
			}
			else
			{
				autoCompleteItemsQuery =
					autoCompleteItemsQuery.Where(s => s.IsStatic==isStaticContext);
			}

			var autoCompleteItems = autoCompleteItemsQuery.SelectMany(i => GetAutoCompleteItem(i, !forNewKeyword));

			return autoCompleteItems.Distinct(new AutoCompleteItemEqualityComparer());
		}

		private static IEnumerable<AutoCompleteItem> GetAutoCompleteItem(ISymbol symbol, bool showClassesAsStatic)
		{
			var result = new List<AutoCompleteItem>();

			var item = new AutoCompleteItem {Name = symbol.Name};

			var itemDoc = symbol.GetDocumentationComment(CultureInfo.GetCultureInfo("en-US"));
				
			if (itemDoc != null)
				item.Description = itemDoc.SummaryTextOpt;

			switch (symbol.Kind)
			{
				case CommonSymbolKind.Method:
					item.ItemType = AutoCompleteItemType.Method;
					var methodSymbol = (IMethodSymbol) symbol;
					item.IsExtension = methodSymbol.IsExtensionMethod;
					item.IsStatic = methodSymbol.IsStatic;
					item.Type = methodSymbol.ReturnsVoid ? "void" : methodSymbol.ReturnType.Name;
					//args
					item.Params = GetSymbolParameters(methodSymbol.Parameters, itemDoc);
					item.IsGeneric = methodSymbol.IsGenericMethod;
					break;
				case CommonSymbolKind.Local:
					item.ItemType = AutoCompleteItemType.Variable;
					var localSymbol = (ILocalSymbol) symbol;
					item.Type = localSymbol.Type.Name;
					break;
				case CommonSymbolKind.Field:
					item.ItemType = AutoCompleteItemType.Variable;
					var fieldSymbol = (IFieldSymbol) symbol;
					item.Type = fieldSymbol.Type.Name;
					break;
				case CommonSymbolKind.Property:
					item.ItemType = AutoCompleteItemType.Property;
					var propertySymbol = (IPropertySymbol) symbol;
					item.Type = propertySymbol.Type.Name;
					break;
				case CommonSymbolKind.Namespace:
					item.ItemType = AutoCompleteItemType.Namespace;
					var namespaceSymbol = (INamespaceSymbol) symbol;
					item.Name = namespaceSymbol.Name;
					break;
				case CommonSymbolKind.NamedType:
					item.ItemType = AutoCompleteItemType.Class;
					var classSymbol = (INamedTypeSymbol) symbol;
					item.Name = classSymbol.Name;
					item.IsStatic = showClassesAsStatic || classSymbol.IsStatic;
					item.IsGeneric = classSymbol.IsGenericType;
					
					if (!showClassesAsStatic)
					{
						var constructors = classSymbol.GetConstructors();
						foreach (var constructor in constructors)
						{
							itemDoc = constructor.GetDocumentationComment(CultureInfo.GetCultureInfo("en-US"));

							var consItem = (AutoCompleteItem) item.Clone();
							if (itemDoc != null)
								consItem.Description = itemDoc.SummaryTextOpt;
							consItem.Params = GetSymbolParameters(constructor.Parameters, itemDoc);
							result.Add(consItem);
						}
					}
					break;
			}

			if (result.Count == 0)
				result.Add(item);

			return result;
		}

		private static AutoCompleteItemParameter[] GetSymbolParameters(ReadOnlyArray<IParameterSymbol> paramsArray,
		                                                               DocumentationComment docComment,
		                                                               bool includeThis = false)
		{
			var result = paramsArray.Where(p => !includeThis || !p.IsThis)
			                        .Select(p => new AutoCompleteItemParameter()
				                        {
					                        Name = p.Name,
					                        Type = GetparameterTypeName(p.Type),
					                        Description = docComment != null ? docComment.GetParameterText(p.Name) : null
				                        })
			                        .ToArray();
			return result.Length == 0 ? null : result;
		}


		private static string GetparameterTypeName(ITypeSymbol type)
		{
			var symbol = type as IPointerTypeSymbol;
			if (symbol != null)
				return symbol.PointedAtType.Name + "*";
			var symbol2 = type as IArrayTypeSymbol;
			if (symbol2 != null)
				return symbol2.ElementType.Name + "[]";

			var symbol3 = type as INamedTypeSymbol;
			if (symbol3 != null && symbol3.TypeArguments.Count > 0)
				return symbol3.ConstructedFrom.Name + "<" + String.Join(", ", symbol3.TypeArguments.Select(t => t.Name)) +
				       ">";

			return type.Name;
		}


		protected override void Initialize()
		{
			// we just create it, so it assembly will be loaded into AppDomain
			CreateSciptEngine();
		}

	}
}