using System;
using System.CodeDom.Compiler;
using Roslyn.Compilers.Common;

namespace DotNetFiddle.Infrastructure
{
	[Serializable]
	public enum ValiationErrorSeverity
	{
		Error,
		Warning,
		Informational
	}


	[Serializable]
	public class ValidationError
	{
		public string ErrorMessage { get; set; }
		public string ErrorNumber { get; set; }
		public int WarningLevel { get; set; }
		public ValiationErrorSeverity Severity { get; set; }
		public int Line { get; set; }
		public int Column { get; set; }
		public string FileName { get; set; }
		public MvcFileType? MvcFileType { get; set; }

		public static ValidationError CreateFromCommonDiagnostic(CommonDiagnostic diag)
		{
			var vError = new ValidationError
			{
				ErrorMessage = diag.Info.GetMessage(),
				ErrorNumber = diag.Info.MessageIdentifier,
				Severity = GetValidationErrorSeverity(diag.Info.Severity.ToString()),
				Line = diag.Location.GetLineSpan(true).StartLinePosition.Line,
				Column = diag.Location.GetLineSpan(true).StartLinePosition.Character
			};

			return vError;
		}

		private static ValiationErrorSeverity GetValidationErrorSeverity(string severity)
		{
			return (ValiationErrorSeverity)Enum.Parse(typeof(ValiationErrorSeverity), severity);
		}

		public static ValidationError CreateFromCompileError(CompilerError compilerError)
		{
			var vError = new ValidationError
			{
				ErrorMessage = compilerError.ErrorText,
				ErrorNumber = compilerError.ErrorNumber,
				WarningLevel = compilerError.IsWarning ? 1 : 0,
				Severity =
					compilerError.IsWarning ? ValiationErrorSeverity.Warning : ValiationErrorSeverity.Error,
				Line = compilerError.Line - 1,
				Column = compilerError.Column - 1,
				FileName = compilerError.FileName
			};

			return vError;
		}

		public static ValidationError CreateFromException(Exception ex)
		{
			var vError = new ValidationError
			{
				ErrorMessage = ex.Message,
				ErrorNumber = ex.HResult.ToString(),
				Severity = ValiationErrorSeverity.Error
			};

			return vError;
		}
	}

}
