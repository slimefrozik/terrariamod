using MacroMod.Common.Macros;
using Terraria.ModLoader;

namespace MacroMod.Common.Systems
{
	/// <summary>
	/// Registers a fixed pool of <see cref="ModKeybind"/>s — one for opening
	/// the Macro Master UI and twenty four general-purpose macro slots that
	/// the player binds in the standard tModLoader Controls menu.  Each slot
	/// remembers (per character) which macro it should run.
	/// </summary>
	public class MacroKeybindSystem : ModSystem
	{
		public const int SlotCount = 24;

		public static ModKeybind ToggleUIKeybind { get; private set; }
		public static ModKeybind StopAllKeybind { get; private set; }
		public static ModKeybind[] MacroSlotKeybinds { get; private set; }

		public override void Load()
		{
			ToggleUIKeybind = KeybindLoader.RegisterKeybind(Mod, "ToggleMacroUI", "M");
			StopAllKeybind = KeybindLoader.RegisterKeybind(Mod, "StopAllMacros", "OemPeriod");

			MacroSlotKeybinds = new ModKeybind[SlotCount];
			for (int i = 0; i < SlotCount; i++) {
				MacroSlotKeybinds[i] = KeybindLoader.RegisterKeybind(Mod, "Macro" + (i + 1).ToString("D2"), "None");
			}
		}

		public override void Unload()
		{
			ToggleUIKeybind = null;
			StopAllKeybind = null;
			MacroSlotKeybinds = null;
		}

		public static string SlotKeybindName(int slotIndex) => "Macro" + (slotIndex + 1).ToString("D2");
	}
}
