using System;
using Microsoft.Xna.Framework;
using Terraria.GameContent.UI.Elements;
using Terraria.Localization;
using Terraria.UI;

namespace MacroMod.Common.UI
{
	/// <summary>Popup listing every command from <see cref="MacroCommandPalette"/>.</summary>
	public class CommandPickerPopup : Popup
	{
		public Action<string> OnPicked;

		public CommandPickerPopup() : base(420f, 480f)
		{
			var title = new UIText(Language.GetText("Mods.MacroMod.UI.PickCommand"), 1.0f, true);
			Append(title);

			var listPanel = new UIPanel { BackgroundColor = new Color(40, 50, 90) };
			listPanel.Width.Set(0f, 1f);
			listPanel.Height.Set(-40f, 1f);
			listPanel.Top.Set(40f, 0f);
			listPanel.SetPadding(4f);
			Append(listPanel);

			var list = new UIList { ListPadding = 2f };
			list.Width.Set(-26f, 1f);
			list.Height.Set(0f, 1f);
			listPanel.Append(list);

			var scroll = new UIScrollbar();
			scroll.Height.Set(0f, 1f);
			scroll.HAlign = 1f;
			listPanel.Append(scroll);
			list.SetScrollbar(scroll);

			foreach (var entry in MacroCommandPalette.Entries) {
				var row = new UIPanel { BackgroundColor = new Color(55, 65, 120) };
				row.Width.Set(0f, 1f);
				row.Height.Set(46f, 0f);
				row.SetPadding(4f);

				string keywordLabel = string.IsNullOrEmpty(entry.Keyword) ? "(raw)" : entry.Keyword;
				var name = new UIText(keywordLabel + "  —  " + entry.Label, 0.85f);
				row.Append(name);

				var desc = new UIText(entry.Description, 0.7f) { TextColor = new Color(200, 220, 255) };
				desc.Top.Set(20f, 0f);
				row.Append(desc);

				string keyword = entry.Keyword;
				row.OnLeftClick += (_, __) => {
					OnPicked?.Invoke(keyword);
					Close();
				};
				list.Add(row);
			}
		}
	}
}
