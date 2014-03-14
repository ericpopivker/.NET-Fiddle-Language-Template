using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Runtime.Serialization;

using DotNetFiddle.Infrastructure.Worker;

namespace DotNetFiddle.Infrastructure
{
	[DataContract, KnownType(typeof(ConsoleOrScriptRunOpts)), KnownType(typeof(MvcRunOpts)), KnownType(typeof(WebFormsRunOpts))]
	[Serializable]
	public class RunOptsBase
	{
		public RunOptsBase()
		{
			NuGetDllReferences = new List<string>();
			ConsoleInputLines = new List<string>();
		}

		[DataMember]
		public Language Language { get; set; }

		[DataMember]
		public ProjectType ProjectType { get; set; }

		[DataMember]
		public CodeBlock CodeBlock { get; set; }

		[DataMember]
		public List<string> NuGetDllReferences { get; set; }

		[DataMember]
		public List<string> ConsoleInputLines { get; set; }
	}

	[DataContract]
	[Serializable]
	public class ConsoleOrScriptRunOpts : RunOptsBase
	{
		public ConsoleOrScriptRunOpts()
		{
			CodeBlock = new ConsoleOrScriptCodeBlock();			
		}
	}

	[DataContract]
	[Serializable]
	public class MvcRunOpts : RunOptsBase
	{
		public MvcRunOpts()
		{
			CodeBlock = new MvcCodeBlock();
			HttpMethod = "GET";
		}

		[DataMember]
		public MvcViewEngine ViewEngine { get; set; }

		[DataMember]
		public string HttpMethod { get; set; }

		[DataMember]
		public string PostBody { get; set; }

		[DataMember]
		public string ContentType { get; set; }

		[DataMember]
		public string Controller { get; set; }

		[DataMember]
		public string Action { get; set; }

		[DataMember]
		public string QueryString { get; set; }

		public MvcPostBackOpts GetPostOpts()
		{
			return new MvcPostBackOpts
			{
				Language = Language,
				MvcViewEngine = ViewEngine,
				NuGetDllReferences = new List<string>(NuGetDllReferences),
				CodeBlock = new MvcCodeBlock((MvcCodeBlock)CodeBlock)
			};			
		}
	}

	[DataContract]
	[Serializable]
	public class WebFormsRunOpts : RunOptsBase
	{
		public WebFormsRunOpts()
		{
			CodeBlock = new WebFormsCodeBlock();
		}
	}

	
	public class MvcPostBackOpts
	{
		public const string SessionDataId = "MVC_POSTBACK_OPTS";

		public Language Language { get; set; }
		public List<string> NuGetDllReferences { get; set; }
		public MvcViewEngine MvcViewEngine { get; set; }
		public MvcCodeBlock CodeBlock { get; set; }
	}

}