using System;
using System.Collections.Generic;

namespace MacroMod.Common.Macros
{
	/// <summary>
	/// In-memory representation of a single macro: its name, source text and
	/// the parsed program ready for execution by <see cref="MacroExecutor"/>.
	/// </summary>
	public class Macro
	{
		public string Name;
		public string Source = string.Empty;
		public List<MacroLine> Program = new();
		public string ParseError;
		public DateTime LastModifiedUtc = DateTime.MinValue;

		/// <summary>Optional one-line description for the in-game list UI.</summary>
		public string Description = string.Empty;

		/// <summary>
		/// Auto-execution triggers parsed from the file's @triggers header.
		/// When any (or all, depending on <see cref="TriggerMode"/>) of these
		/// transitions from false to true, MacroSystem starts the macro.
		/// </summary>
		public List<MacroTrigger> Triggers = new();

		public TriggerMatchMode TriggerMode = TriggerMatchMode.Any;

		public bool TriggersEnabled => Triggers != null && Triggers.Count > 0;

		[NonSerialized] public bool LastTriggerValue;
		[NonSerialized] public bool TriggerInitialized;

		public Macro(string name)
		{
			Name = name;
		}

		public bool HasError => !string.IsNullOrEmpty(ParseError);
	}
}
