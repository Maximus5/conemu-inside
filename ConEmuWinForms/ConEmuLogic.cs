using System;
using System.Collections.Generic;
using System.Text;

using JetBrains.Annotations;

namespace ConEmu.WinForms
{
	public static class ConEmuLogic
	{
		public static bool IsAlphanumeric([NotNull] string s)
		{
			if(s == null)
				throw new ArgumentNullException(nameof(s));
			foreach(char ch in s)
			{
				if((!Char.IsLetterOrDigit(ch)) && (ch != '_'))
					return false;
			}
			return true;
		}

		[Pure]
		[NotNull]
		public static string RenderMacroCommand([NotNull] string sMacroName, [NotNull] IEnumerable<string> parameters)
		{
			if(sMacroName == null)
				throw new ArgumentNullException(nameof(sMacroName));
			if(parameters == null)
				throw new ArgumentNullException(nameof(parameters));

			var sb = new StringBuilder();
			if(!IsAlphanumeric(sMacroName))
				throw new InvalidOperationException("The macro name must be alphanumeric.");
			sb.Append(sMacroName);

			foreach(string parameter in parameters)
			{
				sb.Append(' ');

				if(IsAlphanumeric(parameter))
					sb.Append(parameter);
				else
					sb.Append('@').Append('"').Append(parameter.Replace("\"", "\"\"")).Append('"');
			}
			return sb.ToString();
		}
	}
}