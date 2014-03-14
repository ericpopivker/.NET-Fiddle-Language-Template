using System;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace DotNetFiddle.Infrastructure
{
	[Serializable]
	[DataContract]
	[DebuggerDisplay("{ExceptionType} : {Message}")]
	public class ExceptionInfo
	{
		[DataMember]
		public string Message { get; set; }

		[DataMember]
		public string StackTrace { get; set; }
		
		[DataMember]
		public string ExceptionType { get; set; }

		[DataMember]
		public ExceptionInfo InnerException { get; set; }

		public ExceptionInfo()
		{			
		}

		public ExceptionInfo(Exception ex)
		{
			this.Message = ex.Message;
			this.StackTrace = ex.StackTrace;
			this.ExceptionType = ex.GetType().FullName;
			if (ex.InnerException != null)
			{
				this.InnerException = new ExceptionInfo(ex.InnerException);
			}
		}

		public string GetFormatted()
		{
			return string.Format(
				"{0}. {1}. StackTrace {2}. Inner {3}",
				ExceptionType,
				Message,
				StackTrace,
				InnerException.GetFormatted());
		}
	}
}