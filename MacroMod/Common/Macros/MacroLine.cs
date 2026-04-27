using System.Collections.Generic;

namespace MacroMod.Common.Macros
{
	/// <summary>Kind of statement produced by <see cref="MacroParser"/>.</summary>
	public enum MacroLineKind
	{
		Command,   // /use Wood, /cast Mirror, /buff Regen 30
		Assign,    // /set $foo = expr   (handled as Command "set" too, but kept for clarity)
		If,        // /if expr
		ElseIf,    // /elseif expr
		Else,      // /else
		EndIf,     // /endif
		While,     // /while expr
		EndWhile,  // /endwhile
		Loop,      // /loop  -> jumps back to start
		Stop,      // /stop  -> aborts execution
		Comment,   // # ... or // ...
		Empty
	}

	/// <summary>
	/// Single parsed line of macro source.
	/// <para/>
	/// <see cref="Conditions"/> are the WoW-style modifier groups already split
	/// out from the head of the line, e.g. <c>[hp&lt;50,mod:shift]</c>.  Multiple
	/// bracket groups are stored as separate elements and combined with logical
	/// OR; comma-separated entries inside one group are combined with AND.
	/// </summary>
	public class MacroLine
	{
		public int LineNumber;
		public string RawText;
		public MacroLineKind Kind;

		// Command name without the leading slash, lower-cased (e.g. "use", "cast", "set").
		public string Command;

		// Argument string after the (optional) condition groups.
		public string Args;

		// Each entry corresponds to one [..] group from the head of the line.
		public List<List<string>> Conditions = new();

		// Resolved jump target indexes, filled in by the parser:
		//   - For If: index of the matching ElseIf/Else/EndIf to jump to when false.
		//   - For ElseIf: same as If.
		//   - For Else: index of the matching EndIf.
		//   - For While: index of the matching EndWhile.
		//   - For EndWhile: index of the matching While (jump back).
		public int JumpTarget = -1;

		// Index of the matching EndIf for an If chain (used by Else/ElseIf to jump past the rest of the chain when their branch is taken).
		public int ChainEnd = -1;
	}
}
