namespace DotNetFiddle.NancyFx
{
    using System.Collections.Generic;
    using System.Reflection;
    using Infrastructure;

    public class NancyFxCodeHelper : CodeHelper
    {
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

        public List<AutoCompleteItem> GetAutoCompleteItems(NancyCodeBlock codeBlock, NancyFileType fileType, int? pos = null)
        {
            return new List<AutoCompleteItem>();
        }

        public override TokenTypeResult GetTokenType(string code, int? pos = null)
        {
            throw new System.NotImplementedException();
        }

        public override string GetSampleStorageId()
        {
            throw new System.NotImplementedException();
        }

        public override CodeBlock GetSampleCodeBlock()
        {
            return new NancyCodeBlock();
        }

        public override string GetUsingNamespaceLinePattern()
        {
            throw new System.NotImplementedException();
        }

        public override string GetSecurityLevel1Attribute()
        {
            throw new System.NotImplementedException();
        }

        public override MethodInfo GetMainMethodAndOwnerInstance(Assembly assembly, out object owner)
        {
            throw new System.NotImplementedException();
        }

        protected override void RunInteractive(RunOptsBase opts, RunResult result)
        {
            throw new System.NotImplementedException();
        }
    }
}
