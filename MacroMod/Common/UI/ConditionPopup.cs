using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using Terraria.GameContent.UI.Elements;
using Terraria.Localization;
using Terraria.UI;

namespace MacroMod.Common.UI
{
	/// <summary>
	/// Popup that builds a WoW-style <c>[..]</c> condition prefix.  We
	/// render one row per AND-clause inside an OR-group and let the user
	/// add new groups, toggle preset modifiers (mod:shift, boss, …) and
	/// type freeform clauses.  The resulting bracket prefix is reported via
	/// <see cref="OnApply"/>.
	/// </summary>
	public class ConditionPopup : Popup
	{
		public Action<string> OnApply;

		private readonly List<List<string>> _groups = new();
		private UIList _container;

		public ConditionPopup(string existing) : base(620f, 540f)
		{
			ParseExisting(existing);
			if (_groups.Count == 0) _groups.Add(new List<string>());

			var title = new UIText(Language.GetText("Mods.MacroMod.UI.Conditions"), 1.0f, true);
			Append(title);

			var help = new UIText(Language.GetText("Mods.MacroMod.UI.ConditionsHelp"), 0.72f) {
				TextColor = new Color(200, 215, 255),
			};
			help.Top.Set(28f, 0f);
			Append(help);

			var presetsPanel = new UIPanel { BackgroundColor = new Color(40, 50, 95) };
			presetsPanel.Width.Set(0f, 1f);
			presetsPanel.Height.Set(72f, 0f);
			presetsPanel.Top.Set(54f, 0f);
			presetsPanel.SetPadding(4f);
			Append(presetsPanel);
			BuildPresets(presetsPanel);

			var listPanel = new UIPanel { BackgroundColor = new Color(20, 25, 50) };
			listPanel.Width.Set(0f, 1f);
			listPanel.Height.Set(-180f, 1f);
			listPanel.Top.Set(132f, 0f);
			listPanel.SetPadding(4f);
			Append(listPanel);

			_container = new UIList { ListPadding = 6f };
			_container.Width.Set(-26f, 1f);
			_container.Height.Set(0f, 1f);
			listPanel.Append(_container);

			var scroll = new UIScrollbar();
			scroll.Height.Set(0f, 1f);
			scroll.HAlign = 1f;
			listPanel.Append(scroll);
			_container.SetScrollbar(scroll);

			AddButton("Mods.MacroMod.UI.AddGroup", 0f, () => { _groups.Add(new List<string>()); Rebuild(); });
			AddButton("Mods.MacroMod.UI.Apply", 1f, () => { OnApply?.Invoke(Serialize()); Close(); });
			AddButton("Mods.MacroMod.UI.Clear", 2f, () => { _groups.Clear(); _groups.Add(new List<string>()); Rebuild(); });

			Rebuild();
		}

		private void BuildPresets(UIElement host)
		{
			var presets = new[] {
				("mod:shift",     "Shift"),
				("mod:ctrl",      "Ctrl"),
				("mod:alt",       "Alt"),
				("boss",          "Boss"),
				("hostile",       "Hostile"),
				("mounted",       "Mounted"),
				("water",         "Water"),
				("lava",          "Lava"),
				("honey",         "Honey"),
				("day",           "Day"),
				("night",         "Night"),
				("hardmode",      "Hardmode"),
				("expert",        "Expert"),
				("hp<50",         "HP<50%"),
				("hp<25",         "HP<25%"),
				("mp<50",         "MP<50%"),
			};
			for (int i = 0; i < presets.Length; i++) {
				int row = i / 8;
				int col = i % 8;
				var (token, label) = presets[i];
				var btn = new UITextPanel<string>(label, 0.7f, true);
				btn.Width.Set(70f, 0f);
				btn.Height.Set(28f, 0f);
				btn.Left.Set(col * 72f, 0f);
				btn.Top.Set(row * 32f, 0f);
				btn.OnLeftClick += (_, __) => {
					if (_groups.Count == 0) _groups.Add(new List<string>());
					_groups[^1].Add(token);
					Rebuild();
				};
				host.Append(btn);
			}
		}

		private void AddButton(string langKey, float index, Action act)
		{
			var btn = new UITextPanel<string>(Language.GetTextValue(langKey), 0.85f, true);
			btn.Width.Set(0f, 0.31f);
			btn.Height.Set(32f, 0f);
			btn.HAlign = index / 2f;
			btn.Top.Set(-36f, 1f);
			btn.OnLeftClick += (_, __) => act();
			Append(btn);
		}

		// ---- model <-> UI -------------------------------------------------

		private void ParseExisting(string s)
		{
			if (string.IsNullOrWhiteSpace(s)) return;
			s = s.Trim();
			int i = 0;
			while (i < s.Length) {
				while (i < s.Length && (char.IsWhiteSpace(s[i]) || s[i] == ',')) i++;
				if (i >= s.Length || s[i] != '[') break;
				int end = s.IndexOf(']', i + 1);
				if (end < 0) break;
				string body = s.Substring(i + 1, end - i - 1);
				var grp = new List<string>();
				foreach (string raw in body.Split(',')) {
					string tok = raw.Trim();
					if (tok.Length > 0) grp.Add(tok);
				}
				_groups.Add(grp);
				i = end + 1;
			}
		}

		public string Serialize()
		{
			var sb = new StringBuilder();
			foreach (var grp in _groups) {
				if (grp.Count == 0) continue;
				sb.Append('[');
				for (int i = 0; i < grp.Count; i++) {
					if (i > 0) sb.Append(',');
					sb.Append(grp[i]);
				}
				sb.Append(']');
			}
			return sb.ToString();
		}

		private void Rebuild()
		{
			_container.Clear();
			for (int gi = 0; gi < _groups.Count; gi++) {
				int gIdx = gi;
				var grpPanel = new UIPanel { BackgroundColor = new Color(40, 60, 120) };
				grpPanel.Width.Set(0f, 1f);
				grpPanel.Height.Set(120f, 0f);
				grpPanel.SetPadding(4f);

				var label = new UIText(string.Format(Language.GetTextValue("Mods.MacroMod.UI.OrGroup"), gi + 1), 0.85f);
				grpPanel.Append(label);

				var del = new UITextPanel<string>("✕", 0.7f, true);
				del.Width.Set(28f, 0f);
				del.Height.Set(24f, 0f);
				del.HAlign = 1f;
				del.OnLeftClick += (_, __) => { if (gIdx < _groups.Count) { _groups.RemoveAt(gIdx); if (_groups.Count == 0) _groups.Add(new List<string>()); Rebuild(); } };
				grpPanel.Append(del);

				int rowY = 24;
				var grp = _groups[gi];
				for (int ci = 0; ci < grp.Count; ci++) {
					int cIdx = ci;
					var cPanel = new UIPanel { BackgroundColor = new Color(20, 30, 70) };
					cPanel.Width.Set(0f, 1f);
					cPanel.Height.Set(28f, 0f);
					cPanel.Top.Set(rowY, 0f);
					cPanel.SetPadding(2f);

					var input = new TextInput("condition") { };
					input.Width.Set(-40f, 1f);
					input.Height.Set(0f, 1f);
					input.Text = grp[ci];
					input.OnTextChange = nv => { if (cIdx < grp.Count) grp[cIdx] = nv; };
					cPanel.Append(input);

					var rm = new UITextPanel<string>("✕", 0.7f, true);
					rm.Width.Set(28f, 0f);
					rm.Height.Set(24f, 0f);
					rm.HAlign = 1f;
					rm.OnLeftClick += (_, __) => { if (cIdx < grp.Count) { grp.RemoveAt(cIdx); Rebuild(); } };
					cPanel.Append(rm);

					grpPanel.Append(cPanel);
					rowY += 32;
				}

				var addRow = new UITextPanel<string>(Language.GetTextValue("Mods.MacroMod.UI.AddCondition"), 0.7f, true);
				addRow.Width.Set(0f, 1f);
				addRow.Height.Set(24f, 0f);
				addRow.Top.Set(rowY, 0f);
				addRow.OnLeftClick += (_, __) => { grp.Add(string.Empty); Rebuild(); };
				grpPanel.Append(addRow);

				grpPanel.Height.Set(rowY + 32, 0f);
				_container.Add(grpPanel);
			}
		}
	}
}
