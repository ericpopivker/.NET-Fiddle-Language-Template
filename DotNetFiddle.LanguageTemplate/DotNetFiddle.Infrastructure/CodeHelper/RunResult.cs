using System;
using System.Collections.Generic;
using System.Runtime.Serialization;

namespace DotNetFiddle.Infrastructure
{
	public enum RunResultFailureType
	{
		None,
		CompilerErrors,
		RunTimeException,
		FatalError
	};

	[DataContract]
	public enum SandboxUnloadReason
	{
		[EnumMember] None = 0,
		[EnumMember] MemoryLimit,
		[EnumMember] AliveThreads,
		[EnumMember] ClearDirFailed,
		[EnumMember] CachingDisabled
	}

	[Serializable]
	[DataContract]
	public class RunResult
	{
		[DataMember]
		public bool IsSuccess { get; set; }

		[DataMember]
		public RunResultFailureType FailureType { get; set; }

		[DataMember]
		public List<ValidationError> CompilerErrors { get; set; }

		[DataMember]
		public ExceptionInfo RunTimeException { get; set; }

		[DataMember]
		public string FatalErrorMessage { get; set; }

		[DataMember]
		public string ConsoleOutput { get; set; }

		[DataMember]
		public RunStats Stats { get; set; }

		[DataMember]
		public bool IsSandboxUnloaded { get; set; }

		[DataMember]
		public SandboxUnloadReason SandboxUnloadReason { get; set; }

		[DataMember]
		public bool IsConsoleInputRequested { get; set; }

		[DataMember]
		public string WebPageHtmlOutput { get; set; }
	}
}
