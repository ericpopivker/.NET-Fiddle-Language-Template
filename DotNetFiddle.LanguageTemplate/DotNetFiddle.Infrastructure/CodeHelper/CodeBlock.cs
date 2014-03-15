using System;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

namespace DotNetFiddle.Infrastructure
{
	[DataContract, KnownType(typeof(ConsoleOrScriptCodeBlock)), KnownType(typeof(MvcCodeBlock)), KnownType(typeof(WebFormsCodeBlock))]
	[Serializable]
	public abstract class CodeBlock
	{
		public abstract CodeBlock Clone();
	}

	[DataContract]
	[Serializable]
	public class ConsoleOrScriptCodeBlock : CodeBlock
	{
		public ConsoleOrScriptCodeBlock()
		{

		}

		public ConsoleOrScriptCodeBlock(ConsoleOrScriptCodeBlock copy)
		{
			CodeBlock = copy.CodeBlock;
		}

		public override CodeBlock Clone()
		{
			return new ConsoleOrScriptCodeBlock(this);
		}

		[DataMember]
		public string CodeBlock { get; set; }
	}

	[DataContract]
	[Serializable]
	public class MvcCodeBlock : CodeBlock
	{
		[Required]
		[StringLength(1000, ErrorMessage = "The {0} must be between {2} and {1} characters long.")]
		[Display(Name = "Code")]
		[DataMember]
		public string Model { get; set; }

		[Required]
		[StringLength(1000, ErrorMessage = "The {0} must be between {2} and {1} characters long.")]
		[Display(Name = "Code")]
		[DataMember]
		public string View { get; set; }

		[Required]
		[StringLength(1000, ErrorMessage = "The {0} must be between {2} and {1} characters long.")]
		[Display(Name = "Code")]
		[DataMember]
		public string Controller { get; set; }

		public MvcCodeBlock()
		{
		}

		public MvcCodeBlock(MvcCodeBlock copy)
		{
			if (copy == null)
				return;

			Model = copy.Model;
			View = copy.View;
			Controller = copy.Controller;
		}
		public override CodeBlock Clone()
		{
			return new MvcCodeBlock(this);
		}
	}

    [DataContract]
    [Serializable]
    public class NancyCodeBlock : CodeBlock
    {
        [Required]
        [StringLength(1000, ErrorMessage = "The {0} must be between {2} and {1} characters long.")]
        [Display(Name = "Code")]
        [DataMember]
        public string Model { get; set; }

        [Required]
        [StringLength(1000, ErrorMessage = "The {0} must be between {2} and {1} characters long.")]
        [Display(Name = "Code")]
        [DataMember]
        public string View { get; set; }

        [Required]
        [StringLength(1000, ErrorMessage = "The {0} must be between {2} and {1} characters long.")]
        [Display(Name = "Code")]
        [DataMember]
        public string Module { get; set; }

        public NancyCodeBlock()
        {
        }

        public NancyCodeBlock(NancyCodeBlock copy)
        {
            if (copy == null)
                return;

            Model = copy.Model;
            View = copy.View;
            Module = copy.Module;
        }
        public override CodeBlock Clone()
        {
            return new NancyCodeBlock(this);
        }
    }

	[DataContract]
	[Serializable]
	public class WebFormsCodeBlock : CodeBlock
	{
		[Required]
		[StringLength(1000, ErrorMessage = "The {0} must be between {2} and {1} characters long.")]
		[Display(Name = "Code")]
		[DataMember]
		public string Aspx { get; set; }

		[Required]
		[StringLength(1000, ErrorMessage = "The {0} must be between {2} and {1} characters long.")]
		[Display(Name = "Code")]
		[DataMember]
		public string CodeBehind { get; set; }

		public WebFormsCodeBlock()
		{

		}

		public WebFormsCodeBlock(WebFormsCodeBlock copy)
		{
			Aspx = copy.Aspx;
			CodeBehind = copy.CodeBehind;
		}

		public override CodeBlock Clone()
		{
			return new WebFormsCodeBlock(this);
		}
	}

}
