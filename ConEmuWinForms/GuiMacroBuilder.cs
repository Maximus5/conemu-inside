using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using JetBrains.Annotations;

namespace ConEmu.WinForms
{
	/// <summary>
	/// Fluent API for constructing a GUI macro. Start with the running <see cref="ConEmuSession" />, call <see cref="ConEmuSession.BeginGuiMacro" />.
	/// </summary>
	public sealed class GuiMacroBuilder
	{
		[NotNull]
		private readonly ConEmuSession _owner;

		[NotNull]
		private readonly IEnumerable<string> _parameters;

		[NotNull]
		private readonly string _sMacroName;

		internal GuiMacroBuilder([NotNull] ConEmuSession owner, [NotNull] string sMacroName, [NotNull] IEnumerable<string> parameters)
		{
			if(owner == null)
				throw new ArgumentNullException(nameof(owner));
			if(sMacroName == null)
				throw new ArgumentNullException(nameof(sMacroName));
			if(parameters == null)
				throw new ArgumentNullException(nameof(parameters));

			_owner = owner;
			_sMacroName = sMacroName;
			_parameters = parameters;
		}

		/// <summary>
		/// Renders the macro and executes with ConEmu, asynchronously.
		/// </summary>
		[NotNull]
		public Task<GuiMacroResult> ExecuteAsync()
		{
			return _owner.ExecuteGuiMacroTextAsync(ConEmuLogic.RenderMacroCommand(_sMacroName, _parameters));
		}

		/// <summary>
		/// Renders the macro and executes with ConEmu, getting the result synchronously.
		/// </summary>
		public GuiMacroResult ExecuteSync()
		{
			return _owner.ExecuteGuiMacroTextSync(ConEmuLogic.RenderMacroCommand(_sMacroName, _parameters));
		}

		/// <summary>
		/// Adds a parameter.
		/// </summary>
		[NotNull]
		[Pure]
		public GuiMacroBuilder WithParam([NotNull] string value)
		{
			if(value == null)
				throw new ArgumentNullException(nameof(value));
			return new GuiMacroBuilder(_owner, _sMacroName, _parameters.Concat(new[] {value}));
		}

		/// <summary>
		/// Adds a parameter.
		/// </summary>
		[NotNull]
		[Pure]
		public GuiMacroBuilder WithParam(int value)
		{
			return WithParam(value.ToString());
		}
	}
}