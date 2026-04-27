using System;
using MacroMod.Common.Macros;
using Microsoft.Xna.Framework;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;

namespace MacroMod.Common.UI
{
	/// <summary>
	/// Single row inside the visual macro editor.  Lays out (left → right):
	/// indentation gutter (vertical guide bars), command badge tinted by
	/// category, condition badge, arguments input with optional item / buff
	/// picker icons, then per-row action icons — move up, move down,
	/// duplicate, insert below, delete.
	/// </summary>
	public class LineRow : UIPanel
	{
		public const float RowHeight = 38f;
		private const float IndentStep = 14f;
		private const float CmdBadgeWidth = 110f;
		private const float CondBadgeWidth = 80f;

		public VisualLine Line;
		public int IndentLevel;
		public Action<int, string> RequestEditCommand;
		public Action<int> RequestEditConditions;
		public Action<int, bool> RequestPickContent;
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
			BackgroundColor = RowBackground(line);
			BorderColor = Color.Transparent;

			Build();
		}

		public void RefreshFromModel(int newIndex, int indent)
		{
			Index = newIndex;
			IndentLevel = indent;
			BackgroundColor = RowBackground(Line);
			if (_cmdBtn != null) {
				_cmdBtn.SetText(string.IsNullOrEmpty(Line.Keyword) ? "(text)" : Line.Keyword);
				_cmdBtn.BackgroundColor = CategoryColor(Line.Keyword);
			}
			if (_condBtn != null) {
				_condBtn.SetText(string.IsNullOrEmpty(Line.Conditions) ? "[..]" : Line.Conditions);
				_condBtn.BackgroundColor = string.IsNullOrEmpty(Line.Conditions)
					? UIPalette.PillNeutral
					: UIPalette.PillCondActive;
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
			float gutter = 4f;

			// Indentation guide bars.  One thin vertical strip per nesting
			// level so it's obvious which /if or /while a line belongs to.
			for (int d = 0; d < IndentLevel; d++) {
				var guide = new UIPanel {
					BackgroundColor = UIPalette.IndentGuide * 0.45f,
					BorderColor = Color.Transparent,
				};
				guide.Width.Set(2f, 0f);
				guide.Height.Set(0f, 1f);
				guide.Left.Set(gutter + d * IndentStep, 0f);
				guide.IgnoresMouseInteraction = true;
				Append(guide);
			}

			float x = gutter + IndentLevel * IndentStep + 4f;

			_condBtn = new UITextPanel<string>(string.IsNullOrEmpty(Line.Conditions) ? "[..]" : Line.Conditions, 0.7f, true) {
				BackgroundColor = string.IsNullOrEmpty(Line.Conditions) ? UIPalette.PillNeutral : UIPalette.PillCondActive,
				BorderColor = Color.Transparent,
			};
			_condBtn.Width.Set(CondBadgeWidth, 0f);
			_condBtn.Height.Set(28f, 0f);
			_condBtn.Left.Set(x, 0f);
			_condBtn.OnLeftClick += (_, __) => RequestEditConditions?.Invoke(Index);
			Append(_condBtn);
			x += CondBadgeWidth + 4f;

			_cmdBtn = new UITextPanel<string>(string.IsNullOrEmpty(Line.Keyword) ? "(text)" : Line.Keyword, 0.78f, true) {
				BackgroundColor = CategoryColor(Line.Keyword),
				BorderColor = Color.Transparent,
			};
			_cmdBtn.Width.Set(CmdBadgeWidth, 0f);
			_cmdBtn.Height.Set(28f, 0f);
			_cmdBtn.Left.Set(x, 0f);
			_cmdBtn.OnLeftClick += (_, __) => RequestEditCommand?.Invoke(Index, Line.Keyword);
			Append(_cmdBtn);
			x += CmdBadgeWidth + 4f;

			float rightReserve = 154f; // pickers + 5 action icons
			var argBg = new UIPanel { BackgroundColor = UIPalette.SunkenBg };
			argBg.Left.Set(x, 0f);
			argBg.Width.Set(-(x + rightReserve), 1f);
			argBg.Height.Set(28f, 0f);
			argBg.SetPadding(2f);
			Append(argBg);

			_argsInput = new TextInput(MacroCommandPalette.Find(Line.Keyword)?.ArgsHint ?? "");
			_argsInput.Width.Set(0f, 1f);
			_argsInput.Height.Set(0f, 1f);
			_argsInput.Text = Line.Args;
			_argsInput.OnTextChange = nv => { Line.Args = nv; OnDirty?.Invoke(); };
			argBg.Append(_argsInput);

			_pickItemBtn = new UITextPanel<string>("📎", 0.75f, true) {
				BackgroundColor = new Color(80, 130, 80),
				BorderColor = Color.Transparent,
			};
			_pickItemBtn.Width.Set(28f, 0f);
			_pickItemBtn.Height.Set(28f, 0f);
			_pickItemBtn.HAlign = 1f;
			_pickItemBtn.Left.Set(-150f, 1f);
			_pickItemBtn.OnLeftClick += (_, __) => RequestPickContent?.Invoke(Index, false);
			Append(_pickItemBtn);

			_pickBuffBtn = new UITextPanel<string>("✨", 0.75f, true) {
				BackgroundColor = new Color(130, 90, 150),
				BorderColor = Color.Transparent,
			};
			_pickBuffBtn.Width.Set(28f, 0f);
			_pickBuffBtn.Height.Set(28f, 0f);
			_pickBuffBtn.HAlign = 1f;
			_pickBuffBtn.Left.Set(-150f, 1f);
			_pickBuffBtn.OnLeftClick += (_, __) => RequestPickContent?.Invoke(Index, true);
			Append(_pickBuffBtn);
			UpdatePickerVisibility();

			// Right-aligned action icons: ↑ ↓ + ⎘ ✕
			AppendIcon("▲", -118f, () => RequestMoveUp?.Invoke(Index), new Color(60, 80, 130));
			AppendIcon("▼", -94f, () => RequestMoveDown?.Invoke(Index), new Color(60, 80, 130));
			AppendIcon("+", -70f, () => RequestInsertAfter?.Invoke(Index), new Color(70, 130, 90));
			AppendIcon("⎘", -46f, () => RequestDuplicate?.Invoke(Index), new Color(80, 100, 150));
			AppendIcon("✕", -22f, () => RequestDelete?.Invoke(Index), new Color(150, 80, 80));
		}

		private void AppendIcon(string label, float leftOffsetFromRight, Action act, Color bg)
		{
			var btn = new UITextPanel<string>(label, 0.7f, true) {
				BackgroundColor = bg,
				BorderColor = Color.Transparent,
			};
			btn.Width.Set(22f, 0f);
			btn.Height.Set(28f, 0f);
			btn.HAlign = 1f;
			btn.Left.Set(leftOffsetFromRight, 1f);
			btn.OnLeftClick += (_, __) => act();
			Append(btn);
		}

		private static Color RowBackground(VisualLine line)
		{
			if (line.RawOverride != null && string.IsNullOrWhiteSpace(line.RawOverride)) return new Color(35, 40, 70);
			if (line.Keyword == "#") return new Color(40, 60, 40);
			switch (line.Keyword) {
				case "/if":
				case "/elseif":
				case "/else":
				case "/endif":
				case "/while":
				case "/endwhile":
				case "/loop":
				case "/stop":
					return new Color(70, 55, 30);
				case "":
					return new Color(40, 50, 70);
				default:
					return new Color(40, 55, 90);
			}
		}

		private static Color CategoryColor(string keyword)
		{
			switch (keyword) {
				case "/use":
				case "/cast":
				case "/swap":
				case "/select":
				case "/drop":
				case "/recall":
				case "/mirror":
					return UIPalette.CmdItem;
				case "/buff":
				case "/debuff":
				case "/removebuff":
					return UIPalette.CmdBuff;
				case "/if":
				case "/elseif":
				case "/else":
				case "/endif":
				case "/while":
				case "/endwhile":
				case "/loop":
				case "/stop":
					return UIPalette.CmdFlow;
				case "/wait":
				case "/sleep":
					return UIPalette.CmdTime;
				case "/quickheal":
				case "/quickmana":
				case "/quickbuff":
				case "/mount":
					return UIPalette.CmdQuick;
				case "/attack":
				case "/altattack":
				case "/release":
				case "/useitem":
				case "/rightclick":
					return UIPalette.CmdAttack;
				case "/say":
				case "/print":
				case "/run":
				case "/log":
				case "/chat":
				case "/call":
					return UIPalette.CmdIO;
				case "/set":
					return UIPalette.CmdSet;
				case "#":
					return UIPalette.CmdComment;
				case "":
					return UIPalette.CmdRaw;
				default:
					return UIPalette.PillNeutral;
			}
		}
	}
}
