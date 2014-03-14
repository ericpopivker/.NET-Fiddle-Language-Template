using System;
using System.IO;
using System.Runtime.Serialization;

namespace DotNetFiddle.Infrastructure
{
	[Serializable]
	public class LimitExceededException : Exception
	{
		public LimitType LimitType { get; private set; }

		public LimitExceededException(LimitType type)
			: base(FormatMessage(type))
		{
			LimitType = type;
		}

		public static string FormatMessage(LimitType type)
		{
			switch (type)
			{
				case LimitType.CodeSize:
					return "Code size limit was exceeded";

				case LimitType.CodeOutput:
					return "Code output limit was exceeded";

				case LimitType.MemoryUsage:
					return "Memory usage limit was exceeded";

				case LimitType.ExecutionTime:
					return "Execution time limit was exceeded";

				case LimitType.DirSize:
					return "Directory size limit was exceeded";

				default:
					throw new ArgumentException("Unknown limit type " + type);
			}
		}

		public LimitExceededException(string message, Exception innerException)
			: base(message, innerException)
		{
		}

		protected LimitExceededException(SerializationInfo info, StreamingContext context)
			: base(info, context)
		{
		}
	}
}