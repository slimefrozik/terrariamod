using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria.GameContent.UI.Elements;
using Terraria.Localization;
using Terraria.UI;

namespace MacroMod.Common.UI
{
	/// <summary>
	/// Popup that lists ready-made macro scenarios so the user can drop a
	/// fully working multi-line example into a fresh macro instead of
	/// authoring it line-by-line.
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

		public TemplatePopup() : base(720f, 520f)
		{
			var title = new UIText(Language.GetText("Mods.MacroMod.UI.PickTemplate"), 1.0f, true);
			title.HAlign = 0.5f;
			Append(title);

			var help = new UIText(Language.GetText("Mods.MacroMod.UI.TemplateHelp"), 0.78f);
			help.Top.Set(28f, 0f);
			help.Width.Set(0f, 1f);
			Append(help);

			var listPanel = new UIPanel { BackgroundColor = new Color(20, 25, 50) };
			listPanel.Width.Set(0f, 1f);
			listPanel.Height.Set(-72f, 1f);
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
				var entry = new UIPanel { BackgroundColor = new Color(40, 50, 100) };
				entry.Width.Set(0f, 1f);
				entry.Height.Set(60f, 0f);
				entry.SetPadding(6f);

				var name = new UIText(tpl.Name, 0.9f, true);
				entry.Append(name);

				var desc = new UIText(tpl.Description, 0.7f) { TextColor = new Color(200, 210, 230) };
				desc.Top.Set(22f, 0f);
				entry.Append(desc);

				Template captured = tpl;
				entry.OnLeftClick += (_, __) => {
					OnPicked?.Invoke(captured.Name, captured.Source);
					Close();
				};

				list.Add(entry);
			}
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
