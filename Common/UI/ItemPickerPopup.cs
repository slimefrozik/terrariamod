using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ID;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace MacroMod.Common.UI
{
	/// <summary>
	/// Modal that lets the user search through every loaded item or buff
	/// (vanilla + every enabled mod) and pick one.  Selecting an entry
	/// invokes <see cref="OnPicked"/> with the canonical name and closes
	/// the popup.
	/// </summary>
	public class ItemPickerPopup : Popup
	{
		public Action<string> OnPicked;
		private readonly bool _buffMode;
		private TextInput _search;
		private UIList _list;
		private UIScrollbar _scroll;
		private string _lastQuery = "__none__";

		public ItemPickerPopup(bool buffMode = false) : base(560f, 460f)
		{
			_buffMode = buffMode;

			var title = new UIText(Language.GetText(buffMode ? "Mods.MacroMod.UI.PickBuff" : "Mods.MacroMod.UI.PickItem"), 1.0f, true);
			Append(title);

			var searchPanel = new UIPanel { BackgroundColor = new Color(20, 25, 50) };
			searchPanel.Width.Set(0f, 1f);
			searchPanel.Height.Set(32f, 0f);
			searchPanel.Top.Set(32f, 0f);
			searchPanel.SetPadding(2f);
			Append(searchPanel);

			_search = new TextInput(Language.GetText("Mods.MacroMod.UI.Search").Value);
			_search.Width.Set(0f, 1f);
			_search.Height.Set(0f, 1f);
			_search.OnTextChange = _ => Refilter();
			searchPanel.Append(_search);

			var listPanel = new UIPanel { BackgroundColor = new Color(40, 50, 90) };
			listPanel.Width.Set(0f, 1f);
			listPanel.Height.Set(-72f, 1f);
			listPanel.Top.Set(72f, 0f);
			listPanel.SetPadding(4f);
			Append(listPanel);

			_list = new UIList { ListPadding = 2f };
			_list.Width.Set(-26f, 1f);
			_list.Height.Set(0f, 1f);
			listPanel.Append(_list);

			_scroll = new UIScrollbar();
			_scroll.Height.Set(0f, 1f);
			_scroll.HAlign = 1f;
			listPanel.Append(_scroll);
			_list.SetScrollbar(_scroll);
		}

		public override void OnActivate()
		{
			base.OnActivate();
			_search.Focused = true;
			Refilter(force: true);
		}

		private void Refilter(bool force = false)
		{
			string q = (_search.Text ?? string.Empty).Trim();
			if (!force && q == _lastQuery) return;
			_lastQuery = q;
			string ql = q.ToLowerInvariant();
			_list.Clear();
			int count = _buffMode ? BuffLoader.BuffCount : ItemLoader.ItemCount;
			int max = string.IsNullOrEmpty(q) ? Math.Min(80, count) : 250;
			int added = 0;
			for (int id = 1; id < count && added < max; id++) {
				string display, internalName;
				if (_buffMode) {
					display = Lang.GetBuffName(id) ?? string.Empty;
					internalName = id < BuffID.Count ? string.Empty : BuffLoader.GetBuff(id)?.Name ?? string.Empty;
				}
				else {
					display = Lang.GetItemNameValue(id) ?? string.Empty;
					internalName = id < ItemID.Count ? string.Empty : ItemLoader.GetItem(id)?.Name ?? string.Empty;
				}
				if (string.IsNullOrEmpty(display) && string.IsNullOrEmpty(internalName)) continue;
				if (!string.IsNullOrEmpty(ql) &&
					display.ToLowerInvariant().IndexOf(ql, StringComparison.Ordinal) < 0 &&
					internalName.ToLowerInvariant().IndexOf(ql, StringComparison.Ordinal) < 0) continue;
				added++;
				string canonical = string.IsNullOrEmpty(display) ? internalName : display;
				if (canonical.Contains(' ')) canonical = "\"" + canonical + "\"";
				int idCopy = id;
				_list.Add(BuildRow(id, display, internalName, canonical));
			}
			if (added == 0) {
				_list.Add(new UIText(Language.GetText("Mods.MacroMod.UI.NoMatches"), 0.85f));
			}
		}

		private UIElement BuildRow(int id, string display, string internalName, string canonical)
		{
			var row = new UIPanel { BackgroundColor = new Color(50, 60, 110) };
			row.Width.Set(0f, 1f);
			row.Height.Set(28f, 0f);
			row.SetPadding(4f);

			string suffix = !string.IsNullOrEmpty(internalName) ? " (" + internalName + ")" : string.Empty;
			var t = new UIText(id + ". " + (display.Length > 0 ? display : internalName) + suffix, 0.78f);
			row.Append(t);

			row.OnLeftClick += (_, __) => {
				OnPicked?.Invoke(canonical);
				Close();
			};
			return row;
		}
	}
}
