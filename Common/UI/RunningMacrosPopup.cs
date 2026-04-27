using System;
using System.Linq;
using MacroMod.Common.Macros;
using MacroMod.Common.Systems;
using Microsoft.Xna.Framework;
using Terraria.GameContent.UI.Elements;
using Terraria.Localization;
using Terraria.UI;

namespace MacroMod.Common.UI
{
	/// <summary>
	/// Modal popup listing every currently-running macro instance with a
	/// per-row Stop button and a "Stop all" affordance at the bottom.
	/// Refreshes every frame so the list stays in sync with what the
	/// MacroSystem is actually executing.
	/// </summary>
	public class RunningMacrosPopup : Popup
	{
		public Action OnAfterChange;

		private UIList _list;

		public RunningMacrosPopup() : base(420f, 360f)
		{
			var title = new UIText(Language.GetText("Mods.MacroMod.UI.RunningTitle"), 1.0f, true);
			title.HAlign = 0f;
			Append(title);

			var listPanel = new UIPanel { BackgroundColor = UIPalette.SunkenBg };
			listPanel.Width.Set(0f, 1f);
			listPanel.Height.Set(-72f, 1f);
			listPanel.Top.Set(34f, 0f);
			listPanel.SetPadding(4f);
			Append(listPanel);

			_list = new UIList { ListPadding = 4f };
			_list.Width.Set(-26f, 1f);
			_list.Height.Set(0f, 1f);
			listPanel.Append(_list);

			var scroll = new UIScrollbar();
			scroll.Height.Set(0f, 1f);
			scroll.HAlign = 1f;
			listPanel.Append(scroll);
			_list.SetScrollbar(scroll);

			var stopAll = new UITextPanel<LocalizedText>(Language.GetText("Mods.MacroMod.UI.StopAll"), 0.85f, true);
			stopAll.Width.Set(0f, 1f);
			stopAll.Height.Set(32f, 0f);
			stopAll.Top.Set(-32f, 1f);
			stopAll.BackgroundColor = UIPalette.PillError;
			stopAll.OnLeftClick += (_, __) => {
				MacroSystem.StopAll();
				OnAfterChange?.Invoke();
				Refresh();
			};
			Append(stopAll);

			Refresh();
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			Refresh();
		}

		// -1 is impossible for ComputeSignature (which returns 0 on empty,
		// non-zero hash otherwise), so it is safe as a force-rebuild sentinel.
		private int _lastSignature = -1;

		private void Refresh()
		{
			var running = MacroSystem.Instance?.Running;
			int sig = ComputeSignature(running);
			if (sig == _lastSignature) return;
			_lastSignature = sig;
			_list.Clear();
			if (running == null || running.Count == 0) {
				var empty = new UIText(Language.GetText("Mods.MacroMod.UI.RunningEmpty"), 0.85f) {
					TextColor = new Color(180, 190, 220),
				};
				_list.Add(empty);
				return;
			}
			foreach (MacroExecutor ex in running.ToArray()) {
				_list.Add(BuildRow(ex));
			}
		}

		// Signature combines the identity of every executor reference, so a
		// swap (one macro finishes, another starts in the same tick) still
		// invalidates the cached list even when Count happens to match.
		private static int ComputeSignature(System.Collections.Generic.IReadOnlyList<MacroExecutor> running)
		{
			if (running == null || running.Count == 0) return 0;
			int hash = 17;
			for (int i = 0; i < running.Count; i++) {
				hash = unchecked(hash * 31 + System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(running[i]));
			}
			return hash;
		}

		private UIElement BuildRow(MacroExecutor ex)
		{
			var row = new UIPanel { BackgroundColor = UIPalette.CardIdle };
			row.Width.Set(0f, 1f);
			row.Height.Set(34f, 0f);
			row.SetPadding(4f);

			var name = new UIText(ex.Entry?.Name ?? "?", 0.85f, true) { Top = new StyleDimension(4f, 0f) };
			row.Append(name);

			var stopBtn = new UITextPanel<LocalizedText>(Language.GetText("Mods.MacroMod.UI.Stop"), 0.75f, true);
			stopBtn.Width.Set(80f, 0f);
			stopBtn.Height.Set(26f, 0f);
			stopBtn.HAlign = 1f;
			stopBtn.BackgroundColor = UIPalette.PillError;
			stopBtn.OnLeftClick += (_, __) => {
				MacroSystem.StopMacro(ex);
				OnAfterChange?.Invoke();
				_lastSignature = -1; // force rebuild next tick
			};
			row.Append(stopBtn);
			return row;
		}
	}
}
