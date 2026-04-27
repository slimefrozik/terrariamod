using System;
using MacroMod.Common.Macros;
using Microsoft.Xna.Framework;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;

namespace MacroMod.Common.UI
{
	/// <summary>
	/// Single row inside the visual macro editor.  Lays out (left → right):
	/// indentation gutter, command badge (click to change), arguments
	/// input (click to edit, picker icon for items/buffs), conditions
	/// badge (click to open the condition builder), then per-row controls
	/// — move up, move down, duplicate, delete, insert below.
	/// </summary>
	public class LineRow : UIPanel
	{
		public const float RowHeight = 36f;

		public VisualLine Line;
		public int IndentLevel;
		public Action<int, string> RequestEditCommand;     // (rowIndex, currentKeyword)
		public Action<int> RequestEditConditions;
		public Action<int, bool> RequestPickContent;       // (rowIndex, isBuff)
		public Action<int> RequestMoveUp;
		public Action<int> RequestMoveDown;
		public Action<int> RequestDelete;
		public Action<int> RequestDuplicate;
		public Action<int> RequestInsertAfter;
		public Action OnDirty;

		public int Index;

		private UITextPanel<string> _cmdBtn;
		private UITextPanel<string> _condBtn;
		private TextInput _argsInput;
		private UITextPanel<string> _pickItemBtn;
		private UITextPanel<string> _pickBuffBtn;

		public LineRow(VisualLine line, int index)
		{
			Line = line;
			Index = index;
			Width.Set(0f, 1f);
			Height.Set(RowHeight, 0f);
			SetPadding(2f);
			BackgroundColor = ColorForLine(line);

			Build();
		}

		public void RefreshFromModel(int newIndex, int indent)
		{
			Index = newIndex;
			IndentLevel = indent;
			BackgroundColor = ColorForLine(Line);
			_cmdBtn?.SetText(string.IsNullOrEmpty(Line.Keyword) ? "(text)" : Line.Keyword);
			if (_condBtn != null) {
				_condBtn.SetText(string.IsNullOrEmpty(Line.Conditions) ? "[..]" : Line.Conditions);
				_condBtn.BackgroundColor = string.IsNullOrEmpty(Line.Conditions) ? new Color(60, 70, 130) : new Color(160, 110, 60);
			}
			if (_argsInput != null) {
				if (_argsInput.Text != Line.Args) _argsInput.Text = Line.Args;
				var entry = MacroCommandPalette.Find(Line.Keyword);
				_argsInput.SetHint(entry?.ArgsHint ?? string.Empty);
			}
			UpdatePickerVisibility();
			Recalculate();
		}

		private void UpdatePickerVisibility()
		{
			var entry = MacroCommandPalette.Find(Line.Keyword);
			if (_pickItemBtn != null) _pickItemBtn.IgnoresMouseInteraction = !(entry?.IsItem ?? false);
			if (_pickBuffBtn != null) _pickBuffBtn.IgnoresMouseInteraction = !(entry?.IsBuff ?? false);
		}

		private void Build()
		{
			float x = 0f;
			float indentPx = IndentLevel * 14f;
			x += indentPx;

			// Conditions badge
			_condBtn = new UITextPanel<string>(string.IsNullOrEmpty(Line.Conditions) ? "[..]" : Line.Conditions, 0.7f, true);
			_condBtn.Width.Set(80f, 0f);
			_condBtn.Height.Set(28f, 0f);
			_condBtn.Left.Set(x, 0f);
			_condBtn.BackgroundColor = string.IsNullOrEmpty(Line.Conditions) ? new Color(60, 70, 130) : new Color(160, 110, 60);
			_condBtn.OnLeftClick += (_, __) => RequestEditConditions?.Invoke(Index);
			Append(_condBtn);
			x += 84f;

			// Command picker badge
			_cmdBtn = new UITextPanel<string>(string.IsNullOrEmpty(Line.Keyword) ? "(text)" : Line.Keyword, 0.75f, true);
			_cmdBtn.Width.Set(96f, 0f);
			_cmdBtn.Height.Set(28f, 0f);
			_cmdBtn.Left.Set(x, 0f);
			_cmdBtn.BackgroundColor = new Color(70, 100, 160);
			_cmdBtn.OnLeftClick += (_, __) => RequestEditCommand?.Invoke(Index, Line.Keyword);
			Append(_cmdBtn);
			x += 100f;

			// Args text input (background panel for visibility)
			var argBg = new UIPanel { BackgroundColor = new Color(20, 25, 50) };
			argBg.Left.Set(x, 0f);
			argBg.Width.Set(-(x + 156f), 1f);
			argBg.Height.Set(28f, 0f);
			argBg.SetPadding(2f);
			Append(argBg);

			_argsInput = new TextInput(MacroCommandPalette.Find(Line.Keyword)?.ArgsHint ?? "");
			_argsInput.Width.Set(0f, 1f);
			_argsInput.Height.Set(0f, 1f);
			_argsInput.Text = Line.Args;
			_argsInput.OnTextChange = nv => { Line.Args = nv; OnDirty?.Invoke(); };
			argBg.Append(_argsInput);

			// pick-item / pick-buff
			_pickItemBtn = new UITextPanel<string>("📎", 0.75f, true);
			_pickItemBtn.Width.Set(28f, 0f);
			_pickItemBtn.Height.Set(28f, 0f);
			_pickItemBtn.HAlign = 1f;
			_pickItemBtn.Left.Set(-124f, 1f);
			_pickItemBtn.BackgroundColor = new Color(80, 130, 80);
			_pickItemBtn.OnLeftClick += (_, __) => RequestPickContent?.Invoke(Index, false);
			Append(_pickItemBtn);

			_pickBuffBtn = new UITextPanel<string>("✨", 0.75f, true);
			_pickBuffBtn.Width.Set(28f, 0f);
			_pickBuffBtn.Height.Set(28f, 0f);
			_pickBuffBtn.HAlign = 1f;
			_pickBuffBtn.Left.Set(-124f, 1f);
			_pickBuffBtn.BackgroundColor = new Color(130, 90, 150);
			_pickBuffBtn.OnLeftClick += (_, __) => RequestPickContent?.Invoke(Index, true);
			Append(_pickBuffBtn);
			UpdatePickerVisibility();

			// move/delete/dup/insert
			AppendIcon("▲", -92f, () => RequestMoveUp?.Invoke(Index));
			AppendIcon("▼", -68f, () => RequestMoveDown?.Invoke(Index));
			AppendIcon("⎘", -44f, () => RequestDuplicate?.Invoke(Index));
			AppendIcon("✕", -20f, () => RequestDelete?.Invoke(Index));
		}

		private void AppendIcon(string label, float leftOffsetFromRight, Action act)
		{
			var btn = new UITextPanel<string>(label, 0.7f, true);
			btn.Width.Set(22f, 0f);
			btn.Height.Set(28f, 0f);
			btn.HAlign = 1f;
			btn.Left.Set(leftOffsetFromRight, 1f);
			btn.OnLeftClick += (_, __) => act();
			Append(btn);
		}

		private static Color ColorForLine(VisualLine line)
		{
			if (line.RawOverride != null && string.IsNullOrWhiteSpace(line.RawOverride)) return new Color(35, 40, 70);
			if (line.Keyword == "#") return new Color(50, 75, 50);
			switch (line.Keyword) {
				case "/if":
				case "/elseif":
				case "/else":
				case "/endif":
				case "/while":
				case "/endwhile":
				case "/loop":
				case "/stop":
					return new Color(110, 80, 40);
				case "":
					return new Color(40, 50, 70);
				default:
					return new Color(40, 60, 100);
			}
		}
	}
}
