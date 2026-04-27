using MacroMod.Common.Macros;
using Terraria;
using Terraria.ModLoader;

namespace MacroMod.Common.Systems
{
	/// <summary>
	/// Polls every macro's triggers each tick and starts the macro on a
	/// rising edge (false → true).  Edge detection prevents continuous
	/// re-launching while the condition stays true.
	/// </summary>
	public class MacroTriggerSystem : ModSystem
	{
		public override void PostUpdatePlayers()
		{
			if (Main.dedServ) return;
			var player = Main.LocalPlayer;
			if (player == null || !player.active || player.dead) return;

			foreach (var macro in MacroLibrary.All) {
				if (macro == null || !macro.TriggersEnabled) continue;

				bool fire = EvaluateAll(macro, player);
				if (!macro.TriggerInitialized) {
					macro.TriggerInitialized = true;
					macro.LastTriggerValue = fire;
					continue; // never fire on the very first sample
				}
				if (fire && !macro.LastTriggerValue) {
					// Rising edge — kick off the macro unless it's already running.
					if (!MacroSystem.IsRunning(macro.Name)) {
						MacroSystem.StartMacro(macro.Name);
					}
				}
				macro.LastTriggerValue = fire;
			}
		}

		private static bool EvaluateAll(Macro macro, Player player)
		{
			if (macro.Triggers == null || macro.Triggers.Count == 0) return false;
			if (macro.TriggerMode == TriggerMatchMode.All) {
				foreach (var t in macro.Triggers) {
					if (!t.Evaluate(player)) return false;
				}
				return true;
			}
			foreach (var t in macro.Triggers) {
				if (t.Evaluate(player)) return true;
			}
			return false;
		}
	}
}
