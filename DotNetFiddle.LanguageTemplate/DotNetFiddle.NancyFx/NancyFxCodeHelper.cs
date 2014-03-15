namespace DotNetFiddle.NancyFx
{
    using System;
    using System.CodeDom.Compiler;
    using System.Collections.Generic;
    using System.IO;
    using System.Reflection;
    using Infrastructure;
    using Nancy;
    using Nancy.ViewEngines.Razor;

    public class NancyFxCodeHelper : CodeHelper
    {
        private readonly CodeHelper languageCodeHelper;
        private static string[] _assembliesForBuild;

        static NancyFxCodeHelper()
		{
			SandboxHelper.ExecuteInFullTrust(
				() =>
					{
						_assembliesForBuild = new List<string>()
							{
								typeof (NancyModule).Assembly.Location,
								typeof (NancyRazorEngineHost).Assembly.Location
							}.ToArray();
					});

		}

        public NancyFxCodeHelper(CodeHelper languageCodeHelper)
        {
            this.languageCodeHelper = languageCodeHelper;
        }

        public override Language Language
        {
            get { return Language.CSharp; }
        }

        public override ProjectType ProjectType
        {
            get {return ProjectType.Nancy; }
        }

        public override ValidateCodeResult ValidateCode(string code)
        {
            throw new System.NotImplementedException();
        }

        public ValidateCodeResult ValidateCode(NancyCodeBlock codeBlock)
        {
            if (string.IsNullOrWhiteSpace(codeBlock.Module))
            {
                return new ValidateCodeResult()
                {
                    Errors =
                        new List<ValidationError>(new[] {new ValidationError() {NancyFileType = new NancyFileType()}})
                };
            }

            return new ValidateCodeResult();
        }

        public override List<AutoCompleteItem> GetAutoCompleteItems(string code, int? pos = null)
        {
            throw new System.NotImplementedException();
        }

        public List<AutoCompleteItem> GetAutoCompleteItems(NancyCodeBlock codeBlock, NancyViewEngine viewEngine,
                                                           NancyFileType mvcFileType,
                                                           int? pos = null)
        {
            switch (mvcFileType)
            {
                case NancyFileType.Model:
                    return languageCodeHelper.GetAutoCompleteItems(codeBlock.Model, pos);
                case NancyFileType.View:
                    return new List<AutoCompleteItem>();
                case NancyFileType.Module:
                    {
                        var aggregateCode = codeBlock.Module + codeBlock.Model;
                        return languageCodeHelper.GetAutoCompleteItems(aggregateCode, pos);
                    }
                default:
                    throw new ArgumentOutOfRangeException("mvcFileType");
            }
        }

        private const string _viewCsSample = @"@model HelloWorldNancyApp.SampleViewModel
@{
	Layout = null;
}

<!DOCTYPE html>
<!-- template from http://getbootstrap.com/getting-started -->

<html lang=""en"">
	<head>
		<meta charset=""utf-8"">
		<meta http-equiv=""X-UA-Compatible"" content=""IE=edge"">
		<meta name=""viewport"" content=""width=device-width, initial-scale=1"">
		<title>Bootstrap 101 Template</title>

		<!-- CSS Includes -->
		<link rel=""stylesheet"" href=""//netdna.bootstrapcdn.com/bootstrap/3.1.1/css/bootstrap.min.css"">
		
		<style type=""text/css"">

			.field-validation-error {
				color: #ff0000;
			}

		</style>
	</head>
	
	<body>
		<div class=""container"">
			<div class=""col-md-6 col-md-offset-3"">
				<h1>Hello Stranger</h1>
	
				@using (Html.BeginForm())
				{
					<div class=""form-group"">
						@Html.LabelFor(m => m.Question)
						@Html.TextBoxFor(model => model.Question, new {@class=""form-control""}) 
						@Html.ValidationMessageFor(model => model.Question)
					</div>
				
					<button type=""button"" class=""btn btn-success submit"">Ask</button>
				}
	
				<br/><br/>
				<div class=""alert alert-warning fade"">
					<img src=""https://dl.dropboxusercontent.com/s/lq0mgxtxtc4uj1e/morph.jpg?dl=1&amp;token_hash=AAGm5lEcLzicmV_-T4h6Hc_3iBvhKVerZlOjvGP_vjpoJQ"" /><br/><br/>
					<strong><span class=""alert-content""></span></strong>
				</div>
			</div>
		</div>

		<!-- JS includes -->
		<script src=""https://ajax.googleapis.com/ajax/libs/jquery/1.11.0/jquery.min.js""></script>
		<script src=""//netdna.bootstrapcdn.com/bootstrap/3.1.1/js/bootstrap.min.js""></script>
	
		<script src=""http://ajax.aspnetcdn.com/ajax/jquery.validate/1.11.1/jquery.validate.min.js""></script>
		<script src=""http://ajax.aspnetcdn.com/ajax/mvc/4.0/jquery.validate.unobtrusive.min.js""></script>
		
		<script type=""text/javascript"">
		
			function openAlert(txt) {
				$('.alert-content').text(txt);
				$('.alert').addClass('in');
			}
		
			function closeAlert() {
				$('.alert').removeClass('in');
			}
		
			$(function(){
				var answer = '@Model.Answer';
		
				if(answer && answer != '') 
					openAlert(answer);
		
				$('#Question').change(closeAlert);
				$('#Question').keyup(closeAlert);
		
				$('.submit').click(function(){
					if($('form').valid()) {
					
						$.ajax({
							url: '@Url.RouteUrl(new{ action=""GetAnswer"", controller=""Home""})',
							data: {Answer: '', Question: $('#Question').val()},
								type: 'POST',
								dataType: 'json',
								contentType: ""application/json; charset=utf-8"",
								success: function(resp) {
								openAlert(resp);
						}});
					}
					else {
						closeAlert();
					}
				});
			
			});

		</script>
	</body>
</html>
";

        private const string _modelCsSample = @"using System;
using System.ComponentModel.DataAnnotations;

namespace HelloWorldNancyApp
{
	public class SampleViewModel
	{
		[Required]
		[MinLength(10)]
		[MaxLength(100)]
		[Display(Name = ""Ask Magic 8 Ball any question:"")]
		public string Question { get; set; }

		//See here for list of answers
		public string Answer { get; set; }
	}
}";

        private const string _moduleCsSample = @"using System;
using Nancy;
using System.Collections.Generic;

namespace HelloWorldNancyApp
{
	public class HomeModule : NancyModule
	{
        public HomeModule()
        {
            Get[""\""] = _ => View[""Index"",new SampleViewModel()];
            Post[""\""] = _ => 
            {
              int index = _rnd.Next(_db.Count);
              var answer = _db[index];
              return Response.AsJson(answer);
            }
        }

		private static Random _rnd = new Random();
		
		private static List<string> _db = new List<string> { ""Yes"", ""No"", ""Definitely, yes"", ""I don't know"", ""Looks like, yes""} ;
	}
}";

        public override TokenTypeResult GetTokenType(string code, int? pos = null)
        {
            throw new System.NotImplementedException();
        }

        public override string GetSampleStorageId()
        {
            return "CsNancy";
        }

        public override CodeBlock GetSampleCodeBlock()
        {
            return new NancyCodeBlock() {Model = _modelCsSample, Module = _moduleCsSample, View = _viewCsSample};
        }

        public override string GetUsingNamespaceLinePattern()
        {
            return languageCodeHelper.GetUsingNamespaceLinePattern();
        }

        public override string GetSecurityLevel1Attribute()
        {
            return languageCodeHelper.GetSecurityLevel1Attribute();
        }

        public override MethodInfo GetMainMethodAndOwnerInstance(Assembly assembly, out object owner)
        {
            throw new System.NotImplementedException();
        }

        protected override void RunInteractive(RunOptsBase opts, RunResult result)
        {
            throw new System.NotImplementedException();
        }

        protected override void RunNancy(RunOptsBase opts, RunResult result)
        {
            var nancyOpts = opts as NancyRunOpts;
        }

        private Assembly CompileModelAndController(MvcRunOpts opts, RunResult result)
        {
            string modelFullFileName;
            string moduleFullFileName;
            var codeBlock = (NancyCodeBlock)opts.CodeBlock;
            // as we will reference to controller from View building, we need to save it physically
            var compilerResults = CompileCodeModelAndController(codeBlock.Model, codeBlock.Controller,
                                                                out modelFullFileName, out moduleFullFileName, 4, false);
            if (compilerResults.Errors.HasErrors)
            {
                result.IsSuccess = false;
                result.FailureType = RunResultFailureType.CompilerErrors;

                var modelFileName = Path.GetFileName(modelFullFileName);
                var controllerFileName = Path.GetFileName(moduleFullFileName);
                //we cant just check full names, because they contain chars with different cases
                var errors = GetValidationErrorsFromCompilerErrors(compilerResults.Errors);

                foreach (var validationError in errors)
                {
                    if (validationError.FileName.EndsWith(controllerFileName))
                        validationError.MvcFileType = MvcFileType.Controller;
                    else if (validationError.FileName.EndsWith(modelFileName))
                        validationError.MvcFileType = MvcFileType.Model;
                }
                result.CompilerErrors = errors;

                return null;
            }

            return compilerResults.CompiledAssembly;
        }

        private CompilerResults CompileCodeModelAndController(
            string modelCodeBlock,
            string controllerCodeBlock,
            out string modelFileName,
            out string controllerFileName,
            int? warningLevel = null,
            bool loadAssembyToAppDomain = true)
        {
            string languageName = this.Language.ToString();
            if (this.Language == Language.VbNet)
                languageName = "Vb";

            CodeDomProvider codeCompiler = CodeDomProvider.CreateProvider(languageName);

            CompilerParameters compilerParams;
            List<string> codeItems;
            PrepareForCompile(new List<string> { modelCodeBlock, controllerCodeBlock }, warningLevel, loadAssembyToAppDomain,
                       out compilerParams, out codeItems);


            modelFileName = Path.GetTempFileName();
            compilerParams.TempFiles.AddFile(modelFileName, false);
            controllerFileName = Path.GetTempFileName();
            compilerParams.TempFiles.AddFile(controllerFileName, false);
            var attributeFileName = compilerParams.TempFiles.AddExtension(codeCompiler.FileExtension, false);

            var files = new List<string>
				{
					CreateFile(codeItems[0], modelFileName),
					CreateFile(codeItems[1], controllerFileName),
					CreateFile(codeItems[2], attributeFileName)
				};

            CompilerResults cr = codeCompiler.CompileAssemblyFromFile(compilerParams, files.ToArray());
            compilerParams.TempFiles.Delete();

            return cr;
        }

        private string CreateFile(string codeBlock, string fileName)
        {
            using (var outfile = new StreamWriter(fileName))
            {
                outfile.Write(codeBlock);
            }

            return fileName;
        }

    }
}
