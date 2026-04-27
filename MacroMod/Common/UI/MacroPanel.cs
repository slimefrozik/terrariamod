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
		private const float PanelWidth = 880f;
		private const float PanelHeight = 540f;

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
		private UITextPanel<string> _editToggleBtn;
		private UITextPanel<string> _saveBtn;
		private UITextPanel<string> _runBtn;

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

			_newNameBox = new TextInput(Language.GetTextValue("Mods.MacroMod.UI.NewName"));
			_newNameBox.Width.Set(0f, 1f);
			_newNameBox.Height.Set(0f, 1f);
			newBg.Append(_newNameBox);

			var addBtn = new UITextPanel<string>(Language.GetTextValue("Mods.MacroMod.UI.NewMacro"), 0.85f, true);
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

			// Toolbar buttons (run / edit-toggle / save / reload / open-folder / external / delete)
			_runBtn = AppendToolbarBtn("Mods.MacroMod.UI.Run", 0, () => {
				if (_selected != null) MacroSystem.StartMacro(_selected);
			});
			_editToggleBtn = AppendToolbarBtn("Mods.MacroMod.UI.Edit", 1, ToggleEditMode);
			_saveBtn = AppendToolbarBtn("Mods.MacroMod.UI.Save", 2, SaveEdit);
			AppendToolbarBtn("Mods.MacroMod.UI.Reload", 3, () => { MacroLibrary.ReloadAll(); Refresh(); });
			AppendToolbarBtn("Mods.MacroMod.UI.OpenFolder", 4, OpenFolder);
			AppendToolbarBtn("Mods.MacroMod.UI.EditExternal", 5, OpenInExternalEditor);
			AppendToolbarBtn("Mods.MacroMod.UI.Delete", 6, DeleteSelected);

			// Preview (read-only) panel
			_previewPanel = new UIPanel { BackgroundColor = new Color(20, 25, 50) };
			_previewPanel.Width.Set(0f, 1f);
			_previewPanel.Height.Set(-180f, 1f);
			_previewPanel.Top.Set(76f, 0f);
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

			// Editor panel (visual builder)
			_editorPanel = new UIPanel { BackgroundColor = new Color(20, 25, 50) };
			_editorPanel.Width.Set(0f, 1f);
			_editorPanel.Height.Set(-180f, 1f);
			_editorPanel.Top.Set(76f, 0f);
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

			var addLineBtn = new UITextPanel<string>(Language.GetTextValue("Mods.MacroMod.UI.AddLine"), 0.85f, true);
			addLineBtn.Width.Set(0f, 1f);
			addLineBtn.Height.Set(36f, 0f);
			addLineBtn.Top.Set(-36f, 1f);
			addLineBtn.OnLeftClick += (_, __) => {
				_editorLines?.Add(new VisualLine());
				RebuildEditor();
			};
			_editorPanel.Append(addLineBtn);

			AppendKeybindRow();
		}

		private UITextPanel<string> AppendToolbarBtn(string langKey, int index, Action act)
		{
			var btn = new UITextPanel<string>(Language.GetTextValue(langKey), 0.75f, true);
			btn.Width.Set(0f, 0.135f);
			btn.Height.Set(28f, 0f);
			btn.HAlign = index / 6f;
			btn.Top.Set(-72f, 1f);
			btn.OnLeftClick += (_, __) => {
				try { act(); } catch (Exception e) { Main.NewText("MacroMod: " + e.Message, Color.IndianRed); }
			};
			_detailPanel.Append(btn);
			return btn;
		}

		private void AppendKeybindRow()
		{
			var row = new UIElement();
			row.Width.Set(0f, 1f);
			row.Height.Set(34f, 0f);
			row.Top.Set(-36f, 1f);
			_detailPanel.Append(row);

			var label = new UIText(Language.GetText("Mods.MacroMod.UI.BindSlot"), 0.85f);
			label.Top.Set(8f, 0f);
			row.Append(label);

			for (int i = 0; i < MacroKeybindSystem.SlotCount; i++) {
				int slotIndex = i;
				var btn = new UITextPanel<string>((i + 1).ToString(), 0.7f, true);
				btn.Width.Set(22f, 0f);
				btn.Height.Set(28f, 0f);
				btn.Left.Set(110f + i * 24f, 0f);
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

		private void DeleteSelected()
		{
			if (_selected == null) return;
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
			foreach (var m in MacroLibrary.All.OrderBy(m => m.Name)) {
				_macroList.Add(new MacroListItem(m, m.Name == _selected, () => Select(m.Name)));
			}
			RefreshDetail();
		}

		public void Select(string name)
		{
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
				? Language.GetTextValue("Mods.MacroMod.UI.Cancel")
				: Language.GetTextValue("Mods.MacroMod.UI.Edit"));

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
			row.RequestEditCommand = (idx, current) => OpenPopup(new CommandPickerPopup {
				OnPicked = kw => {
					if (idx >= 0 && idx < _editorLines.Count) {
						_editorLines[idx].Keyword = kw;
						_editorLines[idx].RawOverride = null;
						RebuildEditor();
					}
				}
			});
			row.RequestEditConditions = idx => OpenPopup(new ConditionPopup(idx >= 0 && idx < _editorLines.Count ? _editorLines[idx].Conditions : string.Empty) {
				OnApply = serialized => {
					if (idx >= 0 && idx < _editorLines.Count) {
						_editorLines[idx].Conditions = serialized;
						RebuildEditor();
					}
				}
			});
			row.RequestPickContent = (idx, isBuff) => OpenPopup(new ItemPickerPopup(isBuff) {
				OnPicked = name => {
					if (idx >= 0 && idx < _editorLines.Count) {
						_editorLines[idx].Args = name;
						RebuildEditor();
					}
				}
			});
			row.RequestMoveUp = idx => { if (idx > 0) { (_editorLines[idx], _editorLines[idx - 1]) = (_editorLines[idx - 1], _editorLines[idx]); RebuildEditor(); } };
			row.RequestMoveDown = idx => { if (idx >= 0 && idx < _editorLines.Count - 1) { (_editorLines[idx], _editorLines[idx + 1]) = (_editorLines[idx + 1], _editorLines[idx]); RebuildEditor(); } };
			row.RequestDelete = idx => { if (idx >= 0 && idx < _editorLines.Count) { _editorLines.RemoveAt(idx); RebuildEditor(); } };
			row.RequestDuplicate = idx => {
				if (idx >= 0 && idx < _editorLines.Count) {
					var src = _editorLines[idx];
					var clone = new VisualLine { Keyword = src.Keyword, Args = src.Args, Conditions = src.Conditions, RawOverride = src.RawOverride };
					_editorLines.Insert(idx + 1, clone);
					RebuildEditor();
				}
			};
			row.RequestInsertAfter = idx => { _editorLines.Insert(idx + 1, new VisualLine()); RebuildEditor(); };
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

		public void ClosePopup(UIElement popup)
		{
			if (popup == null) return;
			_popupStack.Remove(popup);
			RemoveChild(popup);
		}

		public bool HasOpenPopup => _popupStack.Count > 0;

		// ---- helper subclasses -------------------------------------------

		private class MacroListItem : UIPanel
		{
			public MacroListItem(Macro macro, bool selected, Action onClick)
			{
				BackgroundColor = selected ? new Color(120, 100, 200) : new Color(50, 60, 110);
				Width.Set(0f, 1f);
				Height.Set(28f, 0f);
				SetPadding(4f);
				Append(new UIText(macro.Name + (macro.HasError ? " (!)" : string.Empty), 0.9f));
				OnLeftClick += (_, __) => onClick?.Invoke();
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
