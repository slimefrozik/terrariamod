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

		public Macro(string name)
		{
			Name = name;
		}

		public bool HasError => !string.IsNullOrEmpty(ParseError);
	}
}
