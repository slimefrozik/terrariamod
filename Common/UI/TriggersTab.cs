using System;
using System.Collections.Generic;
using MacroMod.Common.Macros;
using Microsoft.Xna.Framework;
using Terraria.GameContent.UI.Elements;
using Terraria.Localization;
using Terraria.UI;

namespace MacroMod.Common.UI
{
	/// <summary>
	/// "Triggers" tab content: lets the user attach auto-run conditions to
	/// the selected macro (HP &lt;= 50, no buff X, inventory full, ...).
	/// On Save, the host panel calls MacroLibrary.UpdateTriggers which
	/// serialises the trigger header at the top of the .macro file.
	/// </summary>
	public class TriggersTab : UIElement
	{
		public Action<List<MacroTrigger>, TriggerMatchMode> OnSave;
		public Func<UIElement, UIElement> OpenPopup;

		private readonly List<MacroTrigger> _triggers = new();
		private TriggerMatchMode _mode = TriggerMatchMode.Any;

		private UIList _list;
		private UITextPanel<LocalizedText> _modeBtn;
		private UIText _emptyHint;

		public TriggersTab()
		{
			Width.Set(0f, 1f);
			Height.Set(0f, 1f);

			var headerBg = new UIPanel { BackgroundColor = UIPalette.SunkenBg };
			headerBg.Width.Set(0f, 1f);
			headerBg.Height.Set(48f, 0f);
			headerBg.SetPadding(6f);
			Append(headerBg);

			var modeLabel = new UIText(Language.GetText("Mods.MacroMod.UI.TriggersMatch"), 0.85f);
			modeLabel.Top.Set(8f, 0f);
			headerBg.Append(modeLabel);

			_modeBtn = new UITextPanel<LocalizedText>(Language.GetText("Mods.MacroMod.UI.TriggersAny"), 0.78f, true) {
				BackgroundColor = UIPalette.PillBound,
				BorderColor = Color.Transparent,
			};
			_modeBtn.Width.Set(110f, 0f);
			_modeBtn.Height.Set(30f, 0f);
			_modeBtn.Left.Set(150f, 0f);
			_modeBtn.OnLeftClick += (_, __) => {
				_mode = _mode == TriggerMatchMode.Any ? TriggerMatchMode.All : TriggerMatchMode.Any;
				RefreshModeBtn();
			};
			headerBg.Append(_modeBtn);

			var addBtn = new UITextPanel<LocalizedText>(Language.GetText("Mods.MacroMod.UI.AddTrigger"), 0.78f, true) {
				BackgroundColor = UIPalette.PillRunning,
				BorderColor = Color.Transparent,
			};
			addBtn.Width.Set(180f, 0f);
			addBtn.Height.Set(30f, 0f);
			addBtn.HAlign = 1f;
			addBtn.OnLeftClick += (_, __) => {
				_triggers.Add(new MacroTrigger { Kind = TriggerKind.HpBelow, Op = TriggerOp.LessOrEqual, Number = 50 });
				Rebuild();
			};
			headerBg.Append(addBtn);

			var saveBtn = new UITextPanel<LocalizedText>(Language.GetText("Mods.MacroMod.UI.SaveTriggers"), 0.78f, true) {
				BackgroundColor = UIPalette.CmdItem,
				BorderColor = Color.Transparent,
			};
			saveBtn.Width.Set(140f, 0f);
			saveBtn.Height.Set(30f, 0f);
			saveBtn.HAlign = 1f;
			saveBtn.Left.Set(-188f, 0f);
			saveBtn.OnLeftClick += (_, __) => OnSave?.Invoke(_triggers, _mode);
			headerBg.Append(saveBtn);

			var listPanel = new UIPanel { BackgroundColor = UIPalette.SunkenBg };
			listPanel.Width.Set(0f, 1f);
			listPanel.Height.Set(-56f, 1f);
			listPanel.Top.Set(56f, 0f);
			listPanel.SetPadding(4f);
			Append(listPanel);

			_list = new UIList { ListPadding = 6f };
			_list.Width.Set(-26f, 1f);
			_list.Height.Set(0f, 1f);
			listPanel.Append(_list);

			var scroll = new UIScrollbar();
			scroll.Height.Set(0f, 1f);
			scroll.HAlign = 1f;
			listPanel.Append(scroll);
			_list.SetScrollbar(scroll);

			_emptyHint = new UIText(Language.GetText("Mods.MacroMod.UI.TriggersEmpty"), 0.85f) {
				TextColor = new Color(180, 190, 220),
			};
		}

		public void Load(IList<MacroTrigger> triggers, TriggerMatchMode mode)
		{
			_triggers.Clear();
			if (triggers != null) {
				foreach (var t in triggers) _triggers.Add(t.Clone());
			}
			_mode = mode;
			RefreshModeBtn();
			Rebuild();
		}

		private void RefreshModeBtn()
		{
			_modeBtn?.SetText(_mode == TriggerMatchMode.All
				? Language.GetText("Mods.MacroMod.UI.TriggersAll")
				: Language.GetText("Mods.MacroMod.UI.TriggersAny"));
		}

		private void Rebuild()
		{
			_list.Clear();
			if (_triggers.Count == 0) {
				_list.Add(_emptyHint);
				return;
			}
			for (int i = 0; i < _triggers.Count; i++) {
				int captured = i;
				_list.Add(new TriggerRow(_triggers[i], captured, OpenPopup,
					() => Rebuild(),
					() => { _triggers.RemoveAt(captured); Rebuild(); }));
			}
		}
	}

	internal class TriggerRow : UIPanel
	{
		private readonly MacroTrigger _trigger;
		private readonly Action _onChange;
		private readonly Action _onDelete;
		private readonly Func<UIElement, UIElement> _openPopup;

		private UITextPanel<string> _kindBtn;
		private UITextPanel<string> _opBtn;
		private TextInput _valueInput;

		private static readonly (TriggerKind Kind, string LangKey, Color Tint)[] KindMeta = {
			(TriggerKind.HpBelow,       "Mods.MacroMod.UI.TrigHp",        new Color(180, 80, 80)),
			(TriggerKind.MpBelow,       "Mods.MacroMod.UI.TrigMp",        new Color(80, 110, 200)),
			(TriggerKind.FreeSlots,     "Mods.MacroMod.UI.TrigFreeSlots", new Color(150, 130, 70)),
			(TriggerKind.InventoryFull, "Mods.MacroMod.UI.TrigInvFull",   new Color(150, 130, 70)),
			(TriggerKind.BuffActive,    "Mods.MacroMod.UI.TrigBuff",      new Color(120, 90, 160)),
			(TriggerKind.BuffMissing,   "Mods.MacroMod.UI.TrigNoBuff",    new Color(120, 90, 160)),
			(TriggerKind.BossNearby,    "Mods.MacroMod.UI.TrigBoss",      new Color(180, 80, 80)),
			(TriggerKind.EnemyNearby,   "Mods.MacroMod.UI.TrigEnemy",     new Color(160, 110, 50)),
			(TriggerKind.NightTime,     "Mods.MacroMod.UI.TrigNight",     new Color(60, 70, 120)),
			(TriggerKind.DayTime,       "Mods.MacroMod.UI.TrigDay",       new Color(140, 130, 70)),
			(TriggerKind.OnHit,         "Mods.MacroMod.UI.TrigOnHit",     new Color(180, 80, 80)),
		};

		public TriggerRow(MacroTrigger t, int index, Func<UIElement, UIElement> openPopup, Action onChange, Action onDelete)
		{
			_trigger = t;
			_onChange = onChange;
			_onDelete = onDelete;
			_openPopup = openPopup;

			BackgroundColor = UIPalette.CardIdle;
			Width.Set(0f, 1f);
			Height.Set(46f, 0f);
			SetPadding(6f);

			_kindBtn = new UITextPanel<string>(KindLabel(t.Kind), 0.78f, true) {
				BackgroundColor = TintFor(t.Kind),
				BorderColor = Color.Transparent,
			};
			_kindBtn.Width.Set(160f, 0f);
			_kindBtn.Height.Set(30f, 0f);
			_kindBtn.OnLeftClick += (_, __) => OpenKindPicker();
			Append(_kindBtn);

			_opBtn = new UITextPanel<string>(OpLabel(t.Op), 0.78f, true) {
				BackgroundColor = UIPalette.CmdFlow,
				BorderColor = Color.Transparent,
			};
			_opBtn.Width.Set(48f, 0f);
			_opBtn.Height.Set(30f, 0f);
			_opBtn.Left.Set(168f, 0f);
			_opBtn.OnLeftClick += (_, __) => CycleOp();
			Append(_opBtn);

			var valueBg = new UIPanel { BackgroundColor = UIPalette.SunkenBg };
			valueBg.Width.Set(-(168f + 48f + 8f + 36f), 1f);
			valueBg.Height.Set(30f, 0f);
			valueBg.Left.Set(168f + 48f + 8f, 0f);
			valueBg.SetPadding(4f);
			Append(valueBg);

			_valueInput = new TextInput(InitialValue(t));
			_valueInput.Width.Set(0f, 1f);
			_valueInput.Height.Set(0f, 1f);
			_valueInput.OnTextChange += _ => CommitValue();
			valueBg.Append(_valueInput);

			var delBtn = new UITextPanel<string>("X", 0.85f, true) {
				BackgroundColor = UIPalette.PillError,
				BorderColor = Color.Transparent,
			};
			delBtn.Width.Set(30f, 0f);
			delBtn.Height.Set(30f, 0f);
			delBtn.HAlign = 1f;
			delBtn.OnLeftClick += (_, __) => onDelete?.Invoke();
			Append(delBtn);

			RefreshOpVisibility();
		}

		private void OpenKindPicker()
		{
			if (_openPopup == null) return;
			var popup = new TriggerKindPickerPopup {
				OnPicked = (kind) => {
					_trigger.Kind = kind;
					_kindBtn.SetText(KindLabel(kind));
					_kindBtn.BackgroundColor = TintFor(kind);
					if (kind == TriggerKind.BuffActive || kind == TriggerKind.BuffMissing) {
						_trigger.Number = 0;
					}
					else if (kind != TriggerKind.InventoryFull && kind != TriggerKind.BossNearby
					         && kind != TriggerKind.NightTime && kind != TriggerKind.DayTime
					         && kind != TriggerKind.OnHit) {
						_trigger.Text = string.Empty;
					}
					_valueInput.Text = InitialValue(_trigger);
					RefreshOpVisibility();
					_onChange?.Invoke();
				}
			};
			_openPopup(popup);
		}

		private void RefreshOpVisibility()
		{
			bool isNumeric = _trigger.Kind == TriggerKind.HpBelow
				|| _trigger.Kind == TriggerKind.MpBelow
				|| _trigger.Kind == TriggerKind.FreeSlots
				|| _trigger.Kind == TriggerKind.EnemyNearby;
			_opBtn.IgnoresMouseInteraction = !isNumeric;
			_opBtn.BackgroundColor = isNumeric ? UIPalette.CmdFlow : UIPalette.PillNeutral;

			bool noValue = _trigger.Kind == TriggerKind.InventoryFull
				|| _trigger.Kind == TriggerKind.BossNearby
				|| _trigger.Kind == TriggerKind.NightTime
				|| _trigger.Kind == TriggerKind.DayTime
				|| _trigger.Kind == TriggerKind.OnHit;
			_valueInput.IgnoresMouseInteraction = noValue;
			if (noValue) _valueInput.Text = string.Empty;
		}

		private void CycleOp()
		{
			_trigger.Op = _trigger.Op switch {
				TriggerOp.Less => TriggerOp.LessOrEqual,
				TriggerOp.LessOrEqual => TriggerOp.Equal,
				TriggerOp.Equal => TriggerOp.Greater,
				TriggerOp.Greater => TriggerOp.GreaterOrEqual,
				TriggerOp.GreaterOrEqual => TriggerOp.Less,
				_ => TriggerOp.LessOrEqual,
			};
			_opBtn.SetText(OpLabel(_trigger.Op));
			_onChange?.Invoke();
		}

		private void CommitValue()
		{
			string s = _valueInput.Text ?? string.Empty;
			if (_trigger.Kind == TriggerKind.BuffActive || _trigger.Kind == TriggerKind.BuffMissing) {
				_trigger.Text = s.Trim();
			}
			else if (float.TryParse(s.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float n)) {
				_trigger.Number = n;
			}
		}

		private static string InitialValue(MacroTrigger t)
		{
			return t.Kind switch {
				TriggerKind.BuffActive or TriggerKind.BuffMissing => t.Text ?? string.Empty,
				TriggerKind.InventoryFull or TriggerKind.BossNearby
					or TriggerKind.NightTime or TriggerKind.DayTime or TriggerKind.OnHit => string.Empty,
				_ => t.Number.ToString("0.##", System.Globalization.CultureInfo.InvariantCulture),
			};
		}

		private static string KindLabel(TriggerKind k)
		{
			foreach (var meta in KindMeta) {
				if (meta.Kind == k) return Language.GetTextValue(meta.LangKey);
			}
			return k.ToString();
		}

		private static Color TintFor(TriggerKind k)
		{
			foreach (var meta in KindMeta) {
				if (meta.Kind == k) return meta.Tint;
			}
			return UIPalette.PillNeutral;
		}

		private static string OpLabel(TriggerOp op) => op switch {
			TriggerOp.Less => "<",
			TriggerOp.LessOrEqual => "<=",
			TriggerOp.Equal => "=",
			TriggerOp.Greater => ">",
			TriggerOp.GreaterOrEqual => ">=",
			_ => "<=",
		};

		public static IEnumerable<(TriggerKind Kind, string Label, Color Tint)> Kinds()
		{
			foreach (var meta in KindMeta) {
				yield return (meta.Kind, Language.GetTextValue(meta.LangKey), meta.Tint);
			}
		}
	}

	internal class TriggerKindPickerPopup : Popup
	{
		public Action<TriggerKind> OnPicked;

		public TriggerKindPickerPopup() : base(420f, 480f)
		{
			var title = new UIText(Language.GetText("Mods.MacroMod.UI.TriggerPickKind"), 1.0f, true);
			Append(title);

			var listPanel = new UIPanel { BackgroundColor = UIPalette.SunkenBg };
			listPanel.Width.Set(0f, 1f);
			listPanel.Height.Set(-44f, 1f);
			listPanel.Top.Set(34f, 0f);
			listPanel.SetPadding(4f);
			Append(listPanel);

			var list = new UIList { ListPadding = 4f };
			list.Width.Set(-26f, 1f);
			list.Height.Set(0f, 1f);
			listPanel.Append(list);

			var scroll = new UIScrollbar();
			scroll.Height.Set(0f, 1f);
			scroll.HAlign = 1f;
			listPanel.Append(scroll);
			list.SetScrollbar(scroll);

			foreach (var (kind, label, tint) in TriggerRow.Kinds()) {
				var btn = new UITextPanel<string>(label, 0.85f, true) {
					BackgroundColor = tint,
					BorderColor = Color.Transparent,
				};
				btn.Width.Set(0f, 1f);
				btn.Height.Set(36f, 0f);
				TriggerKind captured = kind;
				btn.OnLeftClick += (_, __) => {
					OnPicked?.Invoke(captured);
					Close();
				};
				list.Add(btn);
			}
		}
	}
}
