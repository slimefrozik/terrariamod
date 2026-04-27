namespace MacroMod.Common.UI
{
	/// <summary>
	/// Static catalog of every macro command shown in the visual editor's
	/// command picker.  Each entry knows its display label, the actual
	/// keyword written to the .macro file and a short description that's
	/// rendered as tooltip in the popup.
	/// </summary>
	public static class MacroCommandPalette
	{
		public class Entry
		{
			public string Keyword;        // "/use" — written verbatim
			public string Label;          // shown in palette
			public string ArgsHint;       // hint text shown in args input
			public string Description;
			public bool IsItem;           // show "📎 item" picker
			public bool IsBuff;           // show "📎 buff" picker
			public bool Block;            // /if, /while, etc — no args picker
			public bool ClosesBlock;
		}

		public static readonly Entry[] Entries = new[] {
			new Entry { Keyword = "/use",        Label = "Use item",       ArgsHint = "Wooden Sword",   Description = "Switch to and use the named item.",        IsItem = true },
			new Entry { Keyword = "/cast",       Label = "Cast",           ArgsHint = "Magic Missile",  Description = "Alias of /use.",                            IsItem = true },
			new Entry { Keyword = "/swap",       Label = "Swap to",        ArgsHint = "Megashark",      Description = "Switch to the item without using it.",      IsItem = true },
			new Entry { Keyword = "/drop",       Label = "Drop",           ArgsHint = "Wood [count]",   Description = "Drop the named item from inventory.",       IsItem = true },
			new Entry { Keyword = "/buff",       Label = "Apply buff",     ArgsHint = "Regeneration 60",Description = "Add a buff to yourself for N seconds.",     IsBuff = true },
			new Entry { Keyword = "/debuff",     Label = "Remove buff",    ArgsHint = "Regeneration",   Description = "Remove the named buff.",                    IsBuff = true },
			new Entry { Keyword = "/wait",       Label = "Wait",           ArgsHint = "1.5s",           Description = "Pause execution. 30, 1.5s, 200ms, 1m." },
			new Entry { Keyword = "/say",        Label = "Say in chat",    ArgsHint = "hello world",    Description = "Send a chat message." },
			new Entry { Keyword = "/print",      Label = "Print local",    ArgsHint = "HP={hppct()}%",  Description = "Print to your local chat. Supports {expr}." },
			new Entry { Keyword = "/run",        Label = "Run macro",      ArgsHint = "OtherMacro",     Description = "Execute another macro as a function." },
			new Entry { Keyword = "/quickheal",  Label = "Quick heal",     ArgsHint = "",                Description = "Vanilla quick-heal." },
			new Entry { Keyword = "/quickmana",  Label = "Quick mana",     ArgsHint = "",                Description = "Vanilla quick-mana." },
			new Entry { Keyword = "/quickbuff",  Label = "Quick buff",     ArgsHint = "",                Description = "Vanilla quick-buff." },
			new Entry { Keyword = "/mount",      Label = "Mount toggle",   ArgsHint = "",                Description = "Toggle the equipped mount." },
			new Entry { Keyword = "/recall",     Label = "Recall",         ArgsHint = "",                Description = "Use Magic Mirror / Cell Phone / Recall Potion." },
			new Entry { Keyword = "/attack",     Label = "Attack",         ArgsHint = "hold | release | once", Description = "Hold or click left-attack with the held weapon." },
			new Entry { Keyword = "/altattack",  Label = "Right-click",    ArgsHint = "hold | release | once", Description = "Right-click (use ammo / alt fire / open chest)." },
			new Entry { Keyword = "/release",    Label = "Release",        ArgsHint = "",                Description = "Release any held attack from /attack hold." },
			new Entry { Keyword = "/set",        Label = "Set variable",   ArgsHint = "$x = hppct()",   Description = "Assign a macro variable." },
			new Entry { Keyword = "/if",         Label = "If",             ArgsHint = "hppct() < 50",   Description = "Begin a conditional block.", Block = true },
			new Entry { Keyword = "/elseif",     Label = "Else if",        ArgsHint = "hppct() < 25",   Description = "Else-branch with a condition.", Block = true },
			new Entry { Keyword = "/else",       Label = "Else",           ArgsHint = "",                Description = "Else-branch.",                Block = true },
			new Entry { Keyword = "/endif",      Label = "End if",         ArgsHint = "",                Description = "Close /if block.",            Block = true, ClosesBlock = true },
			new Entry { Keyword = "/while",      Label = "While",          ArgsHint = "boss()",         Description = "While-loop block.",           Block = true },
			new Entry { Keyword = "/endwhile",   Label = "End while",      ArgsHint = "",                Description = "Close /while block.",         Block = true, ClosesBlock = true },
			new Entry { Keyword = "/loop",       Label = "Loop to start",  ArgsHint = "",                Description = "Jump to the beginning." },
			new Entry { Keyword = "/stop",       Label = "Stop macro",     ArgsHint = "",                Description = "Stop this macro." },
			new Entry { Keyword = "#",           Label = "Comment",        ArgsHint = "your note",      Description = "A non-executable comment line." },
			new Entry { Keyword = "",            Label = "Raw text",       ArgsHint = "/anything",      Description = "Write the line verbatim — useful for /macro chat aliases." },
		};

		public static Entry Find(string keyword)
		{
			if (keyword == null) keyword = string.Empty;
			foreach (var e in Entries) {
				if (e.Keyword == keyword) return e;
			}
			return null;
		}
	}
}
