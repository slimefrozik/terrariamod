using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria.GameContent.UI.Elements;
using Terraria.Localization;
using Terraria.UI;

namespace MacroMod.Common.UI
{
	/// <summary>
	/// Popup that lists ready-made macro scenarios with a script preview on
	/// the right pane.  Selecting an item highlights it and shows its full
	/// source; "Create" copies it into a new macro.
	/// </summary>
	public class TemplatePopup : Popup
	{
		public Action<string, string> OnPicked; // (macroName, source)

		private static readonly List<Template> Templates = new() {
			new Template("Boss farm loop",
				"Switch to a weapon, attack the nearest boss until it dies, recall when inventory is full.",
				"# Boss farm: keep attacking the nearest boss, panic-recall when inv is full\n"
				+ "/select Terra Blade\n"
				+ "/while [boss] free_slots() > 2\n"
				+ "    /attack hold\n"
				+ "    /wait 1t\n"
				+ "/endwhile\n"
				+ "/attack release\n"
				+ "/if free_slots() <= 2\n"
				+ "    /print Inventory full, recalling\n"
				+ "    /recall\n"
				+ "/endif\n"),

			new Template("Fish until full or low HP",
				"Cast the rod, wait for a bite, recall once the inventory fills up or HP drops below 20%.",
				"# Fishing run: keeps fishing until inv is full or HP is low, then recalls\n"
				+ "/select Fishing Pole\n"
				+ "/while [hp>20] free_slots() > 1\n"
				+ "    /attack once\n"
				+ "    /wait 5\n"
				+ "/endwhile\n"
				+ "/print Done fishing, recalling\n"
				+ "/recall\n"),

			new Template("Panic recall on low HP",
				"Quick-heals first; if HP is still under 25%, swaps to recall and uses it.",
				"# Run as a keybind: instant emergency recall\n"
				+ "/quickheal\n"
				+ "/wait 0.2\n"
				+ "/if hp_pct() < 25\n"
				+ "    /recall\n"
				+ "/endif\n"),

			new Template("Buff stack",
				"Quick-buffs every potion in inventory, then announces in chat.",
				"# Pre-fight buff routine\n"
				+ "/quickbuff\n"
				+ "/wait 0.1\n"
				+ "/say Buffed up, ready to fight\n"),

			new Template("Auto-mine column",
				"Holds attack with a pickaxe in front of you for a few seconds.",
				"# Mines whatever your held pickaxe / drill is aimed at\n"
				+ "/attack hold\n"
				+ "/wait 5s\n"
				+ "/attack release\n"),

			new Template("Mana potion when low",
				"If MP is below 20%, drinks a mana potion via QuickMana.",
				"# Use as a periodic keybind during boss fights\n"
				+ "/if mp_pct() < 20\n"
				+ "    /quickmana\n"
				+ "/endif\n"),

			new Template("Smart heal + buff",
				"Quick-heal if HP < 50, quick-buff if no Well Fed buff.",
				"/if hp_pct() < 50\n"
				+ "    /quickheal\n"
				+ "/endif\n"
				+ "/if [nobuff:Well Fed]\n"
				+ "    /quickbuff\n"
				+ "/endif\n"),

			new Template("Drop junk",
				"Drops common trash items so the inventory does not fill up.",
				"# Edit this list to match your trash items\n"
				+ "/drop Wood 999\n"
				+ "/drop Stone Block 999\n"
				+ "/drop Dirt Block 999\n"),
		};

		private Template _selected;
		private readonly List<UIPanel> _entries = new();
		private UIText _previewName;
		private UIText _previewDesc;
		private UIText _previewSource;
		private UITextPanel<LocalizedText> _createBtn;

		public TemplatePopup() : base(900f, 540f)
		{
			var title = new UIText(Language.GetText("Mods.MacroMod.UI.PickTemplate"), 1.0f, true) { HAlign = 0f };
			Append(title);

			var help = new UIText(Language.GetText("Mods.MacroMod.UI.TemplateHelp"), 0.78f) {
				TextColor = new Color(200, 210, 230),
			};
			help.Top.Set(28f, 0f);
			help.Width.Set(0f, 1f);
			Append(help);

			// Left pane — list of templates ----------------------------------
			var listPanel = new UIPanel { BackgroundColor = UIPalette.SunkenBg };
			listPanel.Width.Set(-12f, 0.45f);
			listPanel.Height.Set(-110f, 1f);
			listPanel.Top.Set(56f, 0f);
			listPanel.SetPadding(4f);
			Append(listPanel);

			var list = new UIList { ListPadding = 6f };
			list.Width.Set(-26f, 1f);
			list.Height.Set(0f, 1f);
			listPanel.Append(list);

			var scroll = new UIScrollbar();
			scroll.Height.Set(0f, 1f);
			scroll.HAlign = 1f;
			listPanel.Append(scroll);
			list.SetScrollbar(scroll);

			foreach (var tpl in Templates) {
				Template captured = tpl;
				var entry = new UIPanel { BackgroundColor = UIPalette.CardIdle };
				entry.Width.Set(0f, 1f);
				entry.Height.Set(56f, 0f);
				entry.SetPadding(6f);

				var name = new UIText(tpl.Name, 0.9f, true);
				entry.Append(name);

				var desc = new UIText(TruncateLines(tpl.Description, 60), 0.7f) {
					TextColor = new Color(200, 210, 230),
				};
				desc.Top.Set(22f, 0f);
				entry.Append(desc);

				entry.OnLeftClick += (_, __) => SelectTemplate(captured);
				_entries.Add(entry);
				list.Add(entry);
			}

			// Right pane — preview -------------------------------------------
			var previewPanel = new UIPanel { BackgroundColor = UIPalette.SunkenBg };
			previewPanel.Width.Set(0f, 0.55f);
			previewPanel.Height.Set(-110f, 1f);
			previewPanel.Top.Set(56f, 0f);
			previewPanel.HAlign = 1f;
			previewPanel.SetPadding(8f);
			Append(previewPanel);

			_previewName = new UIText("—", 1.0f, true);
			previewPanel.Append(_previewName);

			_previewDesc = new UIText(string.Empty, 0.78f) {
				TextColor = new Color(200, 210, 230),
			};
			_previewDesc.Top.Set(28f, 0f);
			_previewDesc.Width.Set(0f, 1f);
			previewPanel.Append(_previewDesc);

			var sourceWrap = new UIPanel { BackgroundColor = UIPalette.RootBg };
			sourceWrap.Top.Set(72f, 0f);
			sourceWrap.Width.Set(0f, 1f);
			sourceWrap.Height.Set(-72f, 1f);
			sourceWrap.SetPadding(6f);
			previewPanel.Append(sourceWrap);

			_previewSource = new UIText(string.Empty, 0.8f);
			_previewSource.Width.Set(0f, 1f);
			sourceWrap.Append(_previewSource);

			// Footer — Create / Cancel ---------------------------------------
			_createBtn = new UITextPanel<LocalizedText>(Language.GetText("Mods.MacroMod.UI.NewMacro"), 0.85f, true) {
				BackgroundColor = UIPalette.PillBound,
				BorderColor = Color.Transparent,
			};
			_createBtn.Width.Set(220f, 0f);
			_createBtn.Height.Set(38f, 0f);
			_createBtn.Top.Set(-44f, 1f);
			_createBtn.HAlign = 1f;
			_createBtn.OnLeftClick += (_, __) => {
				if (_selected.Source == null) return;
				OnPicked?.Invoke(_selected.Name, _selected.Source);
				Close();
			};
			Append(_createBtn);

			var cancelBtn = new UITextPanel<LocalizedText>(Language.GetText("Mods.MacroMod.UI.Cancel"), 0.85f, true);
			cancelBtn.Width.Set(120f, 0f);
			cancelBtn.Height.Set(38f, 0f);
			cancelBtn.Top.Set(-44f, 1f);
			cancelBtn.HAlign = 0f;
			cancelBtn.OnLeftClick += (_, __) => Close();
			Append(cancelBtn);

			if (Templates.Count > 0) SelectTemplate(Templates[0]);
		}

		private void SelectTemplate(Template tpl)
		{
			_selected = tpl;
			for (int i = 0; i < _entries.Count; i++) {
				_entries[i].BackgroundColor = ReferenceEquals(Templates[i].Source, tpl.Source)
					? UIPalette.CardSelected
					: UIPalette.CardIdle;
			}
			_previewName?.SetText(tpl.Name);
			_previewDesc?.SetText(tpl.Description);
			_previewSource?.SetText(tpl.Source.Replace("\t", "  "));
		}

		private static string TruncateLines(string s, int max)
		{
			if (string.IsNullOrEmpty(s) || s.Length <= max) return s;
			return s.Substring(0, max - 3) + "...";
		}

		private readonly struct Template
		{
			public readonly string Name;
			public readonly string Description;
			public readonly string Source;

			public Template(string name, string description, string source)
			{
				Name = name;
				Description = description;
				Source = source;
			}
		}
	}
}
