using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DotNetFiddle.Infrastructure.Extensions
{
	public class StackTraceLine
	{
		public string Method { get; set; }
		public string FilePath { get; set; }
		public int Line { get; set; }
	}
	    

	public static class ExceptionExtensions
	{
		public static List<StackTraceLine> ParseStackTrace(this Exception exception)
		{
			return ParseStackTrace(exception.StackTrace);
	    }

		public static List<StackTraceLine> ParseStackTrace(this ExceptionInfo exception)
		{
			return ParseStackTrace(exception.StackTrace);
		}

		public static List<StackTraceLine> ParseStackTrace(string stackTrace)
		{
			if (string.IsNullOrWhiteSpace(stackTrace))
				return new List<StackTraceLine>();

			Regex regex = new Regex("at (.*) in (.*):line (\\d*)");
			var matches = regex.Matches(stackTrace);

			var lines = new List<StackTraceLine>();
			foreach (Match match in matches)
			{
				var line = new StackTraceLine
				{
					Method = match.Groups[1].Captures[0].Value,
					FilePath = match.Groups[2].Captures[0].Value,
					Line = Convert.ToInt32(match.Groups[3].Captures[0].Value)
				};

				lines.Add(line);
			}

			return lines;
		}
	}
}
