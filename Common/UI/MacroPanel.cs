using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MacroMod.Common.Macros;
using MacroMod.Common.Players;
using MacroMod.Common.Systems;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.Localization;
using Terraria.UI;

namespace MacroMod.Common.UI
{
	/// <summary>
	/// Main UIState shown when the player presses the Macro Master toggle
	/// keybind (default <c>M</c>).  Houses the macro list on the left and
	/// either the source preview or the visual editor on the right, plus
	/// a popup overlay (item / buff / command / condition pickers).
	/// </summary>
	public class MacroPanel : UIState
	{
		private const float PanelWidth = 1024f;
		private const float PanelHeight = 640f;

		// Vertical layout slots for the right-hand detail column.  Toolbar sits
		// just below the title block, the keybind bar pinned to the bottom.
		private const float DetailToolbarTop = 76f;   // title (0..28) + keybind (28..50) + status (50..70)
		private const float DetailToolbarHeight = 32f;
		private const float KeybindRowHeight = 38f;

		private DraggablePanel _root;
		private UIList _macroList;
		private UIScrollbar _macroScroll;
		private UIPanel _detailPanel;
		private UIText _detailTitle;
		private UIText _detailKeybind;
		private UIText _detailStatus;

		private UIPanel _previewPanel;
		private UIList _previewLines;
		private UIScrollbar _previewScroll;

		private UIPanel _editorPanel;
		private UIList _editorList;
		private UIScrollbar _editorScroll;

		private TextInput _newNameBox;
		private UITextPanel<LocalizedText> _editToggleBtn;
		private UITextPanel<LocalizedText> _saveBtn;
		private UITextPanel<LocalizedText> _runBtn;
		private UITextPanel<string> _runningPill;

		private string _selected;
		private bool _editMode;
		private List<VisualLine> _editorLines;

		private readonly List<UIElement> _popupStack = new();

		// ---- lifecycle ---------------------------------------------------

		public override void OnInitialize()
		{
			_root = new DraggablePanel();
			_root.Width.Set(PanelWidth, 0f);
			_root.Height.Set(PanelHeight, 0f);
			_root.HAlign = 0.5f;
			_root.VAlign = 0.5f;
			_root.SetPadding(8f);
			_root.BackgroundColor = new Color(33, 43, 79);
			Append(_root);

			var title = new UIText(Language.GetText("Mods.MacroMod.UI.Title"), 1.2f, true) { HAlign = 0f };
			_root.Append(title);

			// Running-macros pill: shows live count, opens a popup with a
			// per-macro Stop button.  Pinned to the top-right next to X.
			_runningPill = new UITextPanel<string>("", 0.75f, true) {
				BackgroundColor = UIPalette.PillRunning,
				BorderColor = Color.Transparent,
			};
			_runningPill.Width.Set(110f, 0f);
			_runningPill.Height.Set(28f, 0f);
			_runningPill.HAlign = 1f;
			_runningPill.Left.Set(-44f, 0f);
			_runningPill.Top.Set(2f, 0f);
			_runningPill.OnLeftClick += (_, __) => OpenPopup(new RunningMacrosPopup());
			_root.Append(_runningPill);

			var closeBtn = new UITextPanel<string>("X", 0.8f, true);
			closeBtn.Width.Set(32f, 0f);
			closeBtn.Height.Set(32f, 0f);
			closeBtn.HAlign = 1f;
			closeBtn.OnLeftClick += (_, __) => MacroUISystem.Instance?.Hide();
			_root.Append(closeBtn);

			BuildLeftColumn();
			BuildRightColumn();
		}

		// ---- left column -------------------------------------------------

		private void BuildLeftColumn()
		{
			var col = new UIElement();
			col.Width.Set(260f, 0f);
			col.Height.Set(PanelHeight - 60f, 0f);
			col.Top.Set(40f, 0f);
			col.HAlign = 0f;
			_root.Append(col);

			var listPanel = new UIPanel { BackgroundColor = new Color(60, 70, 130) };
			listPanel.Width.Set(0f, 1f);
			listPanel.Height.Set(-72f, 1f);
			listPanel.SetPadding(4f);
			col.Append(listPanel);

			_macroList = new UIList { ListPadding = 4f };
			_macroList.Width.Set(-26f, 1f);
			_macroList.Height.Set(0f, 1f);
			listPanel.Append(_macroList);

			_macroScroll = new UIScrollbar();
			_macroScroll.Height.Set(0f, 1f);
			_macroScroll.HAlign = 1f;
			listPanel.Append(_macroScroll);
			_macroList.SetScrollbar(_macroScroll);

			var newBg = new UIPanel { BackgroundColor = new Color(20, 25, 50) };
			newBg.Width.Set(0f, 1f);
			newBg.Height.Set(28f, 0f);
			newBg.Top.Set(-66f, 1f);
			newBg.SetPadding(2f);
			col.Append(newBg);

			_newNameBox = new TextInput(Language.GetText("Mods.MacroMod.UI.NewName").Value);
			_newNameBox.Width.Set(0f, 1f);
			_newNameBox.Height.Set(0f, 1f);
			newBg.Append(_newNameBox);

			var addBtn = new UITextPanel<LocalizedText>(Language.GetText("Mods.MacroMod.UI.NewMacro"), 0.85f, true);
			addBtn.Width.Set(0f, 1f);
			addBtn.Height.Set(28f, 0f);
			addBtn.Top.Set(-32f, 1f);
			addBtn.OnLeftClick += (_, __) => {
				string n = _newNameBox.Text;
				if (string.IsNullOrWhiteSpace(n)) return;
				var m = MacroLibrary.CreateMacro(n,
					"# " + n + "\n# Example:\n# /use Wooden Sword\n# /wait 1\n# /quickheal\n");
				if (m != null) Select(m.Name);
				_newNameBox.Text = string.Empty;
				Refresh();
			};
			col.Append(addBtn);
		}

		// ---- right column ------------------------------------------------

		private void BuildRightColumn()
		{
			_detailPanel = new UIPanel { BackgroundColor = new Color(50, 60, 110) };
			_detailPanel.Width.Set(-272f, 1f);
			_detailPanel.Height.Set(PanelHeight - 60f, 0f);
			_detailPanel.Top.Set(40f, 0f);
			_detailPanel.HAlign = 1f;
			_detailPanel.SetPadding(8f);
			_root.Append(_detailPanel);

			_detailTitle = new UIText(Language.GetText("Mods.MacroMod.UI.NoSelection"), 1.0f, true);
			_detailPanel.Append(_detailTitle);

			_detailKeybind = new UIText(string.Empty, 0.85f);
			_detailKeybind.Top.Set(28f, 0f);
			_detailPanel.Append(_detailKeybind);

			_detailStatus = new UIText(string.Empty, 0.8f);
			_detailStatus.Top.Set(50f, 0f);
			_detailPanel.Append(_detailStatus);

			// Toolbar buttons (run / edit-toggle / save / templates / reload / open-folder / external / delete).
			// All eight buttons share a single fixed row at DetailToolbarTop; widths
			// are computed from the index so localised labels never overflow into
			// the keybind row pinned to the bottom of the panel.  Buttons are
			// grouped (run/stop · edit/save · templates · reload/folder/external · delete)
			// with small visual gaps between groups.
			var btnSpecs = new (string Key, Action Act, int Group, Color Tint)[] {
				("Mods.MacroMod.UI.Run",          () => { if (_selected != null) MacroSystem.StartMacro(_selected); }, 0, new Color(70, 130, 90)),
				("Mods.MacroMod.UI.Stop",         MacroSystem.StopAll,                                                  0, new Color(150, 80, 80)),
				("Mods.MacroMod.UI.Edit",         ToggleEditMode,                                                       1, new Color(80, 110, 170)),
				("Mods.MacroMod.UI.Save",         SaveEdit,                                                             1, new Color(80, 110, 170)),
				("Mods.MacroMod.UI.Templates",    OpenTemplatePopup,                                                    2, new Color(120, 90, 160)),
				("Mods.MacroMod.UI.Reload",       () => { MacroLibrary.ReloadAll(); Refresh(); },                       3, new Color(80, 100, 130)),
				("Mods.MacroMod.UI.OpenFolder",   OpenFolder,                                                           3, new Color(80, 100, 130)),
				("Mods.MacroMod.UI.EditExternal", OpenInExternalEditor,                                                 3, new Color(80, 100, 130)),
				("Mods.MacroMod.UI.Delete",       DeleteSelected,                                                       4, new Color(150, 80, 80)),
			};
			int total = btnSpecs.Length;
			const int groupCount = 5;
			const float groupGap = 0.012f;
			float usable = 1f - groupGap * (groupCount - 1);
			float slot = usable / total;
			for (int i = 0; i < total; i++) {
				float baseLeft = i * slot + (btnSpecs[i].Group == 0 ? 0f : groupGap * btnSpecs[i].Group);
				var btn = MakeToolbarBtn(btnSpecs[i].Key, slot, baseLeft, btnSpecs[i].Tint, btnSpecs[i].Act);
				if (i == 0) _runBtn = btn;
				if (i == 2) _editToggleBtn = btn;
				if (i == 3) _saveBtn = btn;
			}

			// Preview (read-only) panel — fills the gap between toolbar and the
			// keybind row at the bottom (KeybindRowHeight + small padding).
			float previewTop = DetailToolbarTop + DetailToolbarHeight + 8f; // 76 + 32 + 8 = 116
			float previewBottomReserved = KeybindRowHeight + 8f;            // pinned bind row
			_previewPanel = new UIPanel { BackgroundColor = new Color(20, 25, 50) };
			_previewPanel.Width.Set(0f, 1f);
			_previewPanel.Height.Set(-(previewTop + previewBottomReserved), 1f);
			_previewPanel.Top.Set(previewTop, 0f);
			_previewPanel.SetPadding(4f);
			_detailPanel.Append(_previewPanel);

			_previewLines = new UIList { ListPadding = 2f };
			_previewLines.Width.Set(-26f, 1f);
			_previewLines.Height.Set(0f, 1f);
			_previewPanel.Append(_previewLines);

			_previewScroll = new UIScrollbar();
			_previewScroll.Height.Set(0f, 1f);
			_previewScroll.HAlign = 1f;
			_previewPanel.Append(_previewScroll);
			_previewLines.SetScrollbar(_previewScroll);

			// Editor panel (visual builder) shares the same vertical envelope as
			// the preview panel so toggling between them does not shift other UI.
			_editorPanel = new UIPanel { BackgroundColor = new Color(20, 25, 50) };
			_editorPanel.Width.Set(0f, 1f);
			_editorPanel.Height.Set(-(previewTop + previewBottomReserved), 1f);
			_editorPanel.Top.Set(previewTop, 0f);
			_editorPanel.SetPadding(4f);

			_editorList = new UIList { ListPadding = 4f };
			_editorList.Width.Set(-26f, 1f);
			_editorList.Height.Set(-44f, 1f);
			_editorPanel.Append(_editorList);

			_editorScroll = new UIScrollbar();
			_editorScroll.Height.Set(-44f, 1f);
			_editorScroll.HAlign = 1f;
			_editorPanel.Append(_editorScroll);
			_editorList.SetScrollbar(_editorScroll);

			var addLineBtn = new UITextPanel<LocalizedText>(Language.GetText("Mods.MacroMod.UI.AddLine"), 0.85f, true);
			addLineBtn.Width.Set(-160f, 1f);
			addLineBtn.Height.Set(36f, 0f);
			addLineBtn.Top.Set(-36f, 1f);
			addLineBtn.OnLeftClick += (_, __) => {
				_editorLines?.Add(new VisualLine());
				RebuildEditor();
			};
			_editorPanel.Append(addLineBtn);

			var insertTplBtn = new UITextPanel<LocalizedText>(Language.GetText("Mods.MacroMod.UI.Templates"), 0.85f, true);
			insertTplBtn.Width.Set(150f, 0f);
			insertTplBtn.Height.Set(36f, 0f);
			insertTplBtn.Top.Set(-36f, 1f);
			insertTplBtn.HAlign = 1f;
			insertTplBtn.OnLeftClick += (_, __) => OpenTemplatePopup();
			_editorPanel.Append(insertTplBtn);

			AppendKeybindRow();
		}

		private UITextPanel<LocalizedText> MakeToolbarBtn(string langKey, float slotFraction, float leftFraction, Color tint, Action act)
		{
			var btn = new UITextPanel<LocalizedText>(Language.GetText(langKey), 0.78f, true) {
				BackgroundColor = tint,
				BorderColor = Color.Transparent,
			};
			btn.Width.Set(-4f, slotFraction);
			btn.Height.Set(DetailToolbarHeight, 0f);
			btn.Left.Set(0f, leftFraction);
			btn.Top.Set(DetailToolbarTop, 0f);
			btn.OnLeftClick += (_, __) => {
				try { act(); } catch (Exception e) { Main.NewText("MacroMod: " + e.Message, Color.IndianRed); }
			};
			_detailPanel.Append(btn);
			return btn;
		}

		private void AppendKeybindRow()
		{
			var row = new UIPanel { BackgroundColor = new Color(20, 25, 50) };
			row.Width.Set(0f, 1f);
			row.Height.Set(KeybindRowHeight, 0f);
			row.Top.Set(-KeybindRowHeight, 1f);
			row.SetPadding(4f);
			_detailPanel.Append(row);

			var label = new UIText(Language.GetText("Mods.MacroMod.UI.BindSlot"), 0.78f);
			label.Top.Set(8f, 0f);
			row.Append(label);

			// 24 slots fit in two rows of 12 if needed; but we have ~700px
			// available so a single row of 22px buttons fits.
			for (int i = 0; i < MacroKeybindSystem.SlotCount; i++) {
				int slotIndex = i;
				var btn = new UITextPanel<string>((i + 1).ToString(), 0.7f, true);
				btn.Width.Set(24f, 0f);
				btn.Height.Set(26f, 0f);
				btn.Left.Set(120f + i * 26f, 0f);
				btn.Top.Set(2f, 0f);
				btn.OnLeftClick += (_, __) => BindSlot(slotIndex);
				row.Append(btn);
			}
		}

		private void BindSlot(int slotIndex)
		{
			if (_selected == null) return;
			var p = Main.LocalPlayer?.GetModPlayer<MacroPlayer>();
			if (p == null) return;
			bool already = string.Equals(p.GetSlot(slotIndex), _selected, StringComparison.OrdinalIgnoreCase);
			if (already) {
				p.SetSlot(slotIndex, string.Empty);
			}
			else {
				for (int i = 0; i < MacroKeybindSystem.SlotCount; i++) {
					if (string.Equals(p.GetSlot(i), _selected, StringComparison.OrdinalIgnoreCase)) p.SetSlot(i, string.Empty);
				}
				p.SetSlot(slotIndex, _selected);
			}
			Refresh();
		}

		// ---- toolbar actions ---------------------------------------------

		private void ToggleEditMode()
		{
			if (_selected == null) return;
			if (_editMode) { CancelEdit(); return; }
			var macro = MacroLibrary.FindMacro(_selected);
			if (macro == null) return;
			_editorLines = VisualLine.ParseAll(macro.Source);
			_editMode = true;
			RefreshDetail();
			RebuildEditor();
		}

		private void CancelEdit()
		{
			CloseAllPopups();
			_editMode = false;
			_editorLines = null;
			RefreshDetail();
		}

		private void SaveEdit()
		{
			if (!_editMode || _selected == null || _editorLines == null) return;
			var macro = MacroLibrary.FindMacro(_selected);
			if (macro == null) return;
			MacroLibrary.UpdateSource(macro, VisualLine.SerializeAll(_editorLines));
			CloseAllPopups();
			_editMode = false;
			_editorLines = null;
			Refresh();
		}

		private void OpenFolder()
		{
			MacroLibrary.EnsureDirectory();
			try {
				System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
					FileName = MacroLibrary.MacroDirectory,
					UseShellExecute = true,
				});
			}
			catch (Exception e) { Main.NewText("MacroMod: " + e.Message, Color.IndianRed); }
		}

		private void OpenInExternalEditor()
		{
			if (_selected == null) return;
			string path = Path.Combine(MacroLibrary.MacroDirectory, _selected + MacroLibrary.FileExtension);
			try {
				System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
					FileName = path, UseShellExecute = true,
				});
			}
			catch (Exception e) { Main.NewText("MacroMod: " + e.Message, Color.IndianRed); }
		}

		private void OpenTemplatePopup()
		{
			OpenPopup(new TemplatePopup {
				OnPicked = (name, source) => {
					string baseName = string.IsNullOrWhiteSpace(name) ? "template" : name;
					string finalName = baseName;
					int suffix = 1;
					while (MacroLibrary.FindMacro(finalName) != null) {
						suffix++;
						finalName = baseName + " " + suffix;
					}
					var m = MacroLibrary.CreateMacro(finalName, source);
					if (m != null) Select(m.Name);
					Refresh();
				}
			});
		}

		private void DeleteSelected()
		{
			if (_selected == null) return;
			CloseAllPopups();
			MacroLibrary.DeleteMacro(_selected);
			_selected = null;
			_editMode = false;
			_editorLines = null;
			Refresh();
		}

		// ---- refresh / selection -----------------------------------------

		public void Refresh()
		{
			_macroList?.Clear();
			var p = Main.LocalPlayer?.GetModPlayer<MacroPlayer>();
			foreach (var m in MacroLibrary.All.OrderBy(m => m.Name)) {
				int bound = -1;
				if (p != null) {
					for (int i = 0; i < MacroKeybindSystem.SlotCount; i++) {
						if (string.Equals(p.GetSlot(i), m.Name, StringComparison.OrdinalIgnoreCase)) { bound = i; break; }
					}
				}
				_macroList.Add(new MacroListItem(m, m.Name == _selected, bound, () => Select(m.Name)));
			}
			RefreshDetail();
		}

		public void Select(string name)
		{
			CloseAllPopups();
			_selected = name;
			_editMode = false;
			_editorLines = null;
			Refresh();
		}

		private void RefreshDetail()
		{
			_detailPanel.RemoveChild(_previewPanel);
			_detailPanel.RemoveChild(_editorPanel);

			Macro macro = _selected != null ? MacroLibrary.FindMacro(_selected) : null;
			if (macro == null) {
				_detailTitle.SetText(Language.GetText("Mods.MacroMod.UI.NoSelection"));
				_detailKeybind.SetText(string.Empty);
				_detailStatus.SetText(string.Empty);
				_detailPanel.Append(_previewPanel);
				_previewLines.Clear();
				return;
			}
			_detailTitle.SetText(macro.Name);

			var p = Main.LocalPlayer?.GetModPlayer<MacroPlayer>();
			int boundSlot = -1;
			if (p != null) {
				for (int i = 0; i < MacroKeybindSystem.SlotCount; i++) {
					if (string.Equals(p.GetSlot(i), macro.Name, StringComparison.OrdinalIgnoreCase)) { boundSlot = i; break; }
				}
			}
			_detailKeybind.SetText(boundSlot < 0
				? Language.GetTextValue("Mods.MacroMod.UI.Unbound")
				: string.Format(Language.GetTextValue("Mods.MacroMod.UI.Bound"), boundSlot + 1));

			_detailStatus.SetText(macro.HasError
				? string.Format(Language.GetTextValue("Mods.MacroMod.UI.ParseError"), macro.ParseError)
				: string.Format(Language.GetTextValue("Mods.MacroMod.UI.Lines"), macro.Source?.Split('\n').Length ?? 0));

			_editToggleBtn?.SetText(_editMode
				? Language.GetText("Mods.MacroMod.UI.Cancel")
				: Language.GetText("Mods.MacroMod.UI.Edit"));

			if (_editMode) {
				_detailPanel.Append(_editorPanel);
			}
			else {
				_detailPanel.Append(_previewPanel);
				FillPreview(macro);
			}
		}

		private void FillPreview(Macro macro)
		{
			_previewLines.Clear();
			foreach (string raw in (macro.Source ?? string.Empty).Replace("\r\n", "\n").Split('\n')) {
				var t = new UIText(raw.Length == 0 ? " " : raw, 0.78f) { TextColor = ColorForLine(raw) };
				_previewLines.Add(t);
			}
		}

		private static Color ColorForLine(string raw)
		{
			string s = raw.TrimStart();
			if (s.StartsWith("#") || s.StartsWith("//")) return new Color(150, 200, 150);
			if (s.StartsWith("/if") || s.StartsWith("/elseif") || s.StartsWith("/else") || s.StartsWith("/endif")
				|| s.StartsWith("/while") || s.StartsWith("/endwhile") || s.StartsWith("/loop") || s.StartsWith("/stop")) {
				return new Color(255, 200, 120);
			}
			if (s.StartsWith("/")) return new Color(180, 220, 255);
			return Color.White;
		}

		// ---- editor list -------------------------------------------------

		private void RebuildEditor()
		{
			_editorList.Clear();
			if (_editorLines == null) return;
			int depth = 0;
			for (int i = 0; i < _editorLines.Count; i++) {
				int displayDepth = depth;
				switch (_editorLines[i].Keyword) {
					case "/elseif":
					case "/else":
						displayDepth = Math.Max(0, depth - 1);
						break;
					case "/endif":
					case "/endwhile":
						displayDepth = Math.Max(0, depth - 1);
						depth = displayDepth;
						break;
				}
				var row = new LineRow(_editorLines[i], i) { IndentLevel = displayDepth };
				WireRow(row);
				row.RefreshFromModel(i, displayDepth);
				_editorList.Add(row);

				switch (_editorLines[i].Keyword) {
					case "/if":
					case "/while":
						depth++;
						break;
				}
			}
		}

		private void WireRow(LineRow row)
		{
			// Each callback re-checks _editorLines because the popup may outlive
			// the editor (e.g. the user clicks a different macro in the list while
			// a picker is still open).  CloseAllPopups normally handles that, but
			// the guard makes the row defensively crash-proof.
			row.RequestEditCommand = (idx, current) => OpenPopup(new CommandPickerPopup {
				OnPicked = kw => {
					if (_editorLines == null || idx < 0 || idx >= _editorLines.Count) return;
					_editorLines[idx].Keyword = kw;
					_editorLines[idx].RawOverride = null;
					RebuildEditor();
				}
			});
			row.RequestEditConditions = idx => {
				if (_editorLines == null || idx < 0 || idx >= _editorLines.Count) return;
				OpenPopup(new ConditionPopup(_editorLines[idx].Conditions) {
					OnApply = serialized => {
						if (_editorLines == null || idx < 0 || idx >= _editorLines.Count) return;
						_editorLines[idx].Conditions = serialized;
						RebuildEditor();
					}
				});
			};
			row.RequestPickContent = (idx, isBuff) => OpenPopup(new ItemPickerPopup(isBuff) {
				OnPicked = name => {
					if (_editorLines == null || idx < 0 || idx >= _editorLines.Count) return;
					_editorLines[idx].Args = name;
					RebuildEditor();
				}
			});
			row.RequestMoveUp = idx => { if (_editorLines != null && idx > 0 && idx < _editorLines.Count) { (_editorLines[idx], _editorLines[idx - 1]) = (_editorLines[idx - 1], _editorLines[idx]); RebuildEditor(); } };
			row.RequestMoveDown = idx => { if (_editorLines != null && idx >= 0 && idx < _editorLines.Count - 1) { (_editorLines[idx], _editorLines[idx + 1]) = (_editorLines[idx + 1], _editorLines[idx]); RebuildEditor(); } };
			row.RequestDelete = idx => { if (_editorLines != null && idx >= 0 && idx < _editorLines.Count) { _editorLines.RemoveAt(idx); RebuildEditor(); } };
			row.RequestDuplicate = idx => {
				if (_editorLines == null || idx < 0 || idx >= _editorLines.Count) return;
				var src = _editorLines[idx];
				var clone = new VisualLine { Keyword = src.Keyword, Args = src.Args, Conditions = src.Conditions, RawOverride = src.RawOverride };
				_editorLines.Insert(idx + 1, clone);
				RebuildEditor();
			};
			row.RequestInsertAfter = idx => { if (_editorLines != null && idx >= -1 && idx < _editorLines.Count) { _editorLines.Insert(idx + 1, new VisualLine()); RebuildEditor(); } };
			row.OnDirty = () => { /* args field already updated on the model */ };
		}

		// ---- popup management --------------------------------------------

		public void OpenPopup(UIElement popup)
		{
			_popupStack.Add(popup);
			Append(popup);
			popup.Activate();
			popup.Recalculate();
		}

		public void CloseAllPopups()
		{
			if (_popupStack.Count == 0) return;
			// Snapshot to avoid mutation during iteration.
			var pending = _popupStack.ToArray();
			_popupStack.Clear();
			foreach (var p in pending) RemoveChild(p);
		}

		public void ClosePopup(UIElement popup)
		{
			if (popup == null) return;
			_popupStack.Remove(popup);
			RemoveChild(popup);
		}

		public bool HasOpenPopup => _popupStack.Count > 0;

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			RefreshRunningPill();
		}

		private void RefreshRunningPill()
		{
			if (_runningPill == null) return;
			int count = MacroSystem.Instance?.Running?.Count ?? 0;
			_runningPill.SetText(count == 0
				? Language.GetText("Mods.MacroMod.UI.RunningIdle").Value
				: string.Format(Language.GetText("Mods.MacroMod.UI.RunningPill").Value, count));
			_runningPill.BackgroundColor = count == 0 ? UIPalette.PillNeutral : UIPalette.PillRunning;
		}

		// ---- helper subclasses -------------------------------------------

		private class MacroListItem : UIPanel
		{
			private readonly UIText _runningDot;

			public MacroListItem(Macro macro, bool selected, int boundSlot, Action onClick)
			{
				BackgroundColor = selected
					? UIPalette.CardSelected
					: (macro.HasError ? UIPalette.CardError : UIPalette.CardIdle);
				BorderColor = selected ? new Color(220, 220, 255) * 0.7f : Color.Transparent;
				Width.Set(0f, 1f);
				Height.Set(40f, 0f);
				SetPadding(4f);

				// Vertical accent bar on the left so the eye can scan the
				// list quickly.  Bound macros get a brighter cyan stripe.
				var stripe = new UIPanel {
					BackgroundColor = boundSlot >= 0 ? UIPalette.PillBound : UIPalette.PillNeutral,
					BorderColor = Color.Transparent,
				};
				stripe.Width.Set(4f, 0f);
				stripe.Height.Set(0f, 1f);
				stripe.Left.Set(0f, 0f);
				Append(stripe);

				var name = new UIText(macro.Name, 0.95f, true) { TextColor = Color.White };
				name.Left.Set(10f, 0f);
				name.Top.Set(2f, 0f);
				Append(name);

				// Sub-line: line count + parse-error short note.
				int lineCount = macro.Source?.Split('\n').Length ?? 0;
				string sub = macro.HasError
					? "! " + (macro.ParseError ?? "parse error")
					: lineCount + " lines";
				if (sub.Length > 36) sub = sub.Substring(0, 33) + "...";
				var subText = new UIText(sub, 0.7f) { TextColor = new Color(190, 200, 230) };
				subText.Left.Set(10f, 0f);
				subText.Top.Set(20f, 0f);
				Append(subText);

				// Right-side badges
				if (boundSlot >= 0) {
					var bind = MakePill("⌨ " + (boundSlot + 1), UIPalette.PillBound);
					bind.HAlign = 1f;
					bind.Top.Set(2f, 0f);
					bind.Left.Set(-4f, 0f);
					Append(bind);
				}
				_runningDot = new UIText("●", 0.95f, true) {
					TextColor = UIPalette.PillRunning,
					HAlign = 1f,
				};
				_runningDot.Top.Set(20f, 0f);
				_runningDot.Left.Set(-4f, 0f);
				_runningDot.Width.Set(14f, 0f);
				_runningDot.Height.Set(16f, 0f);
				Append(_runningDot);

				OnLeftClick += (_, __) => onClick?.Invoke();
			}

			private static UITextPanel<string> MakePill(string label, Color bg)
			{
				var p = new UITextPanel<string>(label, 0.7f, true) {
					BackgroundColor = bg,
					BorderColor = Color.Transparent,
				};
				p.Width.Set(46f, 0f);
				p.Height.Set(18f, 0f);
				return p;
			}

			public override void Update(GameTime gameTime)
			{
				base.Update(gameTime);
				// Lazy retag — running status is read every frame instead of
				// pushed via events, since macros can finish mid-frame.
				if (_runningDot != null) {
					_runningDot.TextColor = MacroSystem.IsRunning(GetMacroName())
						? UIPalette.PillRunning
						: Color.Transparent;
				}
			}

			private string GetMacroName()
			{
				foreach (var c in Children) {
					if (c is UIText t && t.Top.Pixels < 10f) return t.Text;
				}
				return string.Empty;
			}
		}

		private class DraggablePanel : UIPanel
		{
			private Vector2 _dragOffset;
			private bool _dragging;

			public override void LeftMouseDown(UIMouseEvent evt)
			{
				if (evt.Target == this) {
					_dragging = true;
					_dragOffset = new Vector2(evt.MousePosition.X - Left.Pixels, evt.MousePosition.Y - Top.Pixels);
				}
				base.LeftMouseDown(evt);
			}

			public override void LeftMouseUp(UIMouseEvent evt)
			{
				_dragging = false;
				base.LeftMouseUp(evt);
			}

			public override void Update(GameTime gameTime)
			{
				base.Update(gameTime);
				if (_dragging) {
					Left.Set(Main.MouseScreen.X - _dragOffset.X, 0f);
					Top.Set(Main.MouseScreen.Y - _dragOffset.Y, 0f);
					HAlign = 0f; VAlign = 0f;
					Recalculate();
				}
				if (ContainsPoint(Main.MouseScreen)) Main.LocalPlayer.mouseInterface = true;
			}
		}
	}
}
