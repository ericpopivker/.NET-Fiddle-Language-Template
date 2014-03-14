using System;
using System.Runtime.Serialization;

namespace DotNetFiddle.Infrastructure
{
	[Serializable]
	[DataContract]
	public class RunStats
	{
		[DataMember]
		public DateTime RunAt { get; set; }

		[DataMember]
		public TimeSpan CompileTime { get; set; }

		[DataMember]
		public TimeSpan ExecuteTime { get; set; }

		[DataMember]
		public long MemoryUsage { get; set; }

		[DataMember]
		public TimeSpan CpuUsage { get; set; }
	}
}