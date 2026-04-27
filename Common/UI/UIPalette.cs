using Microsoft.Xna.Framework;

namespace MacroMod.Common.UI
{
	/// <summary>
	/// Centralised colour palette for the Macro Master UI.  All panels and
	/// rows pull tints from here so the look stays consistent and a single
	/// edit re-themes the whole mod.
	/// </summary>
	internal static class UIPalette
	{
		// Backdrops --------------------------------------------------------
		public static readonly Color RootBg          = new(33, 43, 79);
		public static readonly Color SectionBg       = new(50, 60, 110);
		public static readonly Color SunkenBg        = new(20, 25, 50);
		public static readonly Color CardIdle        = new(45, 55, 95);
		public static readonly Color CardSelected    = new(110, 95, 195);
		public static readonly Color CardHoverTint   = new(70, 85, 140);
		public static readonly Color CardError       = new(140, 70, 70);

		// Pills / badges ---------------------------------------------------
		public static readonly Color PillNeutral     = new(60, 70, 130);
		public static readonly Color PillBound       = new(80, 130, 200);
		public static readonly Color PillRunning     = new(80, 160, 90);
		public static readonly Color PillError       = new(180, 80, 80);
		public static readonly Color PillCondActive  = new(170, 120, 60);

		// Editor command categories ---------------------------------------
		public static readonly Color CmdItem         = new(50, 90, 150);   // /use, /cast, /swap, /drop
		public static readonly Color CmdBuff         = new(120, 90, 160);  // /buff, /debuff
		public static readonly Color CmdFlow         = new(160, 110, 50);  // /if, /while, /loop, /stop
		public static readonly Color CmdTime         = new(80, 120, 110);  // /wait
		public static readonly Color CmdQuick        = new(70, 130, 90);   // /quickheal, /quickbuff, /quickmana
		public static readonly Color CmdAttack       = new(180, 80, 80);   // /attack, /altattack, /release
		public static readonly Color CmdIO           = new(60, 100, 140);  // /say, /print
		public static readonly Color CmdSet          = new(110, 110, 60);  // /set
		public static readonly Color CmdComment      = new(60, 95, 60);    // # comment
		public static readonly Color CmdRaw          = new(50, 60, 90);

		// Indent guide ----------------------------------------------------
		public static readonly Color IndentGuide     = new(120, 140, 200);
	}
}
