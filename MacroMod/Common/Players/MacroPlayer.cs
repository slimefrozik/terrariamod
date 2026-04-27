using System.Collections.Generic;
using MacroMod.Common.Macros;
using MacroMod.Common.Systems;
using Terraria.GameInput;
using Terraria.ModLoader;
using Terraria.ModLoader.IO;

namespace MacroMod.Common.Players
{
	/// <summary>
	/// Per-character storage for keybind→macro slot bindings, plus the
	/// glue that fires the corresponding macro when its key is pressed.
	/// </summary>
	public class MacroPlayer : ModPlayer
	{
		// Slot index → macro name.  Empty string means "unbound".
		public string[] SlotMacro = new string[MacroKeybindSystem.SlotCount];

		public override void Initialize()
		{
			for (int i = 0; i < SlotMacro.Length; i++) SlotMacro[i] = string.Empty;
		}

		public override void SaveData(TagCompound tag)
		{
			var list = new List<string>(SlotMacro);
			tag["MacroBindings"] = list;
		}

		public override void LoadData(TagCompound tag)
		{
			if (tag.TryGet("MacroBindings", out List<string> list)) {
				for (int i = 0; i < SlotMacro.Length; i++) {
					SlotMacro[i] = i < list.Count ? (list[i] ?? string.Empty) : string.Empty;
				}
			}
		}

		public override void ProcessTriggers(TriggersSet triggersSet)
		{
			if (Player != Terraria.Main.LocalPlayer) return;

			if (MacroKeybindSystem.ToggleUIKeybind?.JustPressed == true) {
				global::MacroMod.Common.UI.MacroUISystem.Instance?.Toggle();
			}
			if (MacroKeybindSystem.StopAllKeybind?.JustPressed == true) {
				MacroSystem.StopAll();
			}

			var keys = MacroKeybindSystem.MacroSlotKeybinds;
			if (keys == null) return;
			for (int i = 0; i < keys.Length; i++) {
				if (keys[i] == null) continue;
				if (!keys[i].JustPressed) continue;
				string name = SlotMacro[i];
				if (string.IsNullOrEmpty(name)) continue;
				MacroSystem.StartMacro(name);
			}
		}

		public string GetSlot(int i) => i >= 0 && i < SlotMacro.Length ? SlotMacro[i] : string.Empty;

		public void SetSlot(int i, string macroName)
		{
			if (i < 0 || i >= SlotMacro.Length) return;
			SlotMacro[i] = macroName ?? string.Empty;
		}
	}
}
