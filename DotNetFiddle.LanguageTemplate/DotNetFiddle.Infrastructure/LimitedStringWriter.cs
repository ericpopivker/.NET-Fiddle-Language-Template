using System.IO;

namespace DotNetFiddle.Infrastructure
{
	public class LimitedStringWriter : StringWriter
	{
		private readonly int _maxSymbolsSize;

		public LimitedStringWriter(int maxSymbolsSize)
		{
			_maxSymbolsSize = maxSymbolsSize;
		}

		public override void Write(char value)
		{
			base.Write(value);
			ValidateSize();
		}

		public override void Write(char[] buffer, int index, int count)
		{
			base.Write(buffer, index, count);
			ValidateSize();
		}

		public override void Write(string value)
		{
			base.Write(value);
			ValidateSize();
		}

		private void ValidateSize()
		{
			if (GetStringBuilder().Length > _maxSymbolsSize)
			{
				throw new LimitExceededException(LimitType.CodeOutput);
			}
		}
	}
}