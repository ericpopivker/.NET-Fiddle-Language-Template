using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DotNetFiddle.Infrastructure
{
	public class ConsoleStringReader : TextReader
	{
		public event EventHandler RequestedConsoleInput;

		private List<string> _input;
		private int _idx;

		public List<string> InputLines
		{
			set
			{
				_input = value;
				_idx = 0;
			}
		}

		public override int Read()
		{
			throw new NotImplementedException("Console.Read is not supported.");
		}

		public override string ReadLine()
		{
			if (_input == null || _idx >= _input.Count)
				RequestedConsoleInput(this, null);

			Console.WriteLine(CodeHelper.ConsoleInputLineStart + (_idx) + CodeHelper.ConsoleInputLineEnd);
			return _input[_idx++];
		}
	}
}
