using System.Collections.Generic;
using MacroMod.Common.Macros;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;

namespace MacroMod.Common.Systems
{
	/// <summary>
	/// Hosts every running <see cref="MacroExecutor"/>.  Each tick we step
	/// every active executor, then drain its pending action latches onto
	/// the local player.  Exposes a static <see cref="StartMacro"/> entry
	/// point used by keybinds, the chat command and the UI's "Test" button.
	/// </summary>
	public class MacroSystem : ModSystem
	{
		public static MacroSystem Instance { get; private set; }

		private readonly List<MacroExecutor> _running = new();
		private int _fileWatchTimer;

		public IReadOnlyList<MacroExecutor> Running => _running;

		public override void Load()
		{
			Instance = this;
		}

		public override void Unload()
		{
			Instance = null;
			_running.Clear();
		}

		public override void OnWorldLoad()
		{
			MacroLibrary.EnsureDirectory();
			MacroLibrary.ReloadAll();
			ItemResolver.ResetCache();
			BuffResolver.ResetCache();
		}

		public override void OnWorldUnload()
		{
			_running.Clear();
		}

		// ---- API ----------------------------------------------------------

		/// <summary>Start running the named macro for the local player.  Errors are surfaced in chat.</summary>
		public static void StartMacro(string name)
		{
			Macro macro = MacroLibrary.FindMacro(name);
			if (macro == null) {
				Main.NewText(string.Format(Language.GetTextValue("Mods.MacroMod.NoSuchMacro"), name), Color.IndianRed);
				return;
			}
			StartMacro(macro);
		}

		public static void StartMacro(Macro macro)
		{
			if (Instance == null || macro == null) return;
			if (macro.HasError) {
				Main.NewText(string.Format(Language.GetTextValue("Mods.MacroMod.ParseError"), macro.Name, macro.ParseError), Color.IndianRed);
				return;
			}
			if (Main.LocalPlayer == null) return;
			Instance._running.Add(new MacroExecutor(macro, Main.LocalPlayer));
		}

		/// <summary>Cancel every running macro instance.</summary>
		public static void StopAll()
		{
			Instance?._running.Clear();
		}

		// ---- per-frame ----------------------------------------------------

		public override void PreUpdatePlayers()
		{
			// Lightweight hot-reload poll: every ~30 ticks check for edits to .macro files on disk.
			if (++_fileWatchTimer >= 30) {
				_fileWatchTimer = 0;
				MacroLibrary.ReloadIfChanged();
			}

			if (_running.Count == 0) return;

			Player player = Main.LocalPlayer;
			if (player == null || !player.active || player.dead) return;

			for (int i = _running.Count - 1; i >= 0; i--) {
				MacroExecutor ex = _running[i];
				ex.Tick();
				DrainPending(ex, player);
				if (ex.IsFinished) _running.RemoveAt(i);
			}
		}

		private static void DrainPending(MacroExecutor ex, Player player)
		{
			if (ex.PendingHotbarSlot >= 0 && ex.PendingHotbarSlot < 10) {
				player.selectedItem = ex.PendingHotbarSlot;
				ex.PendingHotbarSlot = -1;
			}
			if (ex.PendingUseItem || ex.HoldUseItem) {
				player.controlUseItem = true;
				ex.PendingUseItem = false;
			}
			if (ex.PendingUseAlt || ex.HoldUseAlt) {
				player.controlUseTile = true;
				ex.PendingUseAlt = false;
			}
			if (!string.IsNullOrEmpty(ex.PendingChat)) {
				if (Main.netMode == NetmodeID.SinglePlayer) {
					Main.NewText(ex.PendingChat);
				}
				else {
					Terraria.Chat.ChatHelper.SendChatMessageFromClient(new Terraria.Chat.ChatMessage(ex.PendingChat));
				}
				ex.PendingChat = null;
			}
			if (ex.PendingPlayerAction != null) {
				try { ex.PendingPlayerAction(player); } catch { /* swallow */ }
				ex.PendingPlayerAction = null;
			}
		}

	}
}
