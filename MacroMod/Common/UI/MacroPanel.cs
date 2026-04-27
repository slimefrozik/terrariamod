using System;
using System.IO;
using System.Linq;
using MacroMod.Common.Macros;
using MacroMod.Common.Players;
using MacroMod.Common.Systems;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent;
using Terraria.GameContent.UI.Elements;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.UI;

namespace MacroMod.Common.UI
{
	/// <summary>
	/// Main UIState shown when the player presses the Macro Master toggle
	/// keybind (default <c>M</c>).  Renders a draggable panel containing:
	/// <list type="bullet">
	///   <item>a scrollable list of every <see cref="Macro"/> known to <see cref="MacroLibrary"/>;</item>
	///   <item>a details/preview pane for the selected macro;</item>
	///   <item>buttons to run, reload, edit externally, delete and create macros;</item>
	///   <item>a per-character keybind slot picker so each macro can be assigned to one of the 24 hotkeys.</item>
	/// </list>
	/// </summary>
	public class MacroPanel : UIState
	{
		private const float PanelWidth = 720f;
		private const float PanelHeight = 460f;

		private DraggablePanel _root;
		private UIList _macroList;
		private UIScrollbar _macroScroll;
		private UIPanel _detailPanel;
		private UIText _detailTitle;
		private UIText _detailKeybind;
		private UIText _detailStatus;
		private UIList _detailLines;
		private UIScrollbar _detailScroll;
		private UITextBox _newNameBox;

		private string _selected;

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

			var title = new UIText(Language.GetText("Mods.MacroMod.UI.Title"), 1.2f, true) {
				HAlign = 0f,
			};
			title.Top.Set(0f, 0f);
			_root.Append(title);

			var closeBtn = new UITextPanel<string>("X", 0.8f, true);
			closeBtn.Width.Set(32f, 0f);
			closeBtn.Height.Set(32f, 0f);
			closeBtn.HAlign = 1f;
			closeBtn.OnLeftClick += (_, __) => MacroUISystem.Instance?.Hide();
			_root.Append(closeBtn);

			// --- left column: macro list + new entry ----------------------
			var leftCol = new UIElement();
			leftCol.Width.Set(260f, 0f);
			leftCol.Height.Set(PanelHeight - 60f, 0f);
			leftCol.Top.Set(40f, 0f);
			leftCol.HAlign = 0f;
			_root.Append(leftCol);

			var listPanel = new UIPanel { BackgroundColor = new Color(60, 70, 130) };
			listPanel.Width.Set(0f, 1f);
			listPanel.Height.Set(-72f, 1f);
			listPanel.SetPadding(4f);
			leftCol.Append(listPanel);

			_macroList = new UIList { ListPadding = 4f };
			_macroList.Width.Set(-26f, 1f);
			_macroList.Height.Set(0f, 1f);
			listPanel.Append(_macroList);

			_macroScroll = new UIScrollbar();
			_macroScroll.Height.Set(0f, 1f);
			_macroScroll.HAlign = 1f;
			listPanel.Append(_macroScroll);
			_macroList.SetScrollbar(_macroScroll);

			_newNameBox = new UITextBox(Language.GetTextValue("Mods.MacroMod.UI.NewName"), 0.9f);
			_newNameBox.Width.Set(0f, 1f);
			_newNameBox.Height.Set(28f, 0f);
			_newNameBox.Top.Set(-66f, 1f);
			_newNameBox.OnLeftClick += (_, __) => {
				if (_newNameBox.Text == Language.GetTextValue("Mods.MacroMod.UI.NewName"))
					_newNameBox.SetText(string.Empty);
			};
			leftCol.Append(_newNameBox);

			var addBtn = MakeButton("Mods.MacroMod.UI.NewMacro", 60f, _ => {
				string n = _newNameBox.Text;
				if (string.IsNullOrWhiteSpace(n) || n == Language.GetTextValue("Mods.MacroMod.UI.NewName")) return;
				var m = MacroLibrary.CreateMacro(n,
					"# " + n + "\n# Example:\n# /use Wooden Sword\n# /wait 1\n# /quickheal\n");
				if (m != null) Select(m.Name);
				_newNameBox.SetText(string.Empty);
				Refresh();
			});
			addBtn.Top.Set(-32f, 1f);
			addBtn.Width.Set(0f, 1f);
			leftCol.Append(addBtn);

			// --- right column: details ------------------------------------
			_detailPanel = new UIPanel { BackgroundColor = new Color(50, 60, 110) };
			_detailPanel.Width.Set(-272f, 1f);
			_detailPanel.Height.Set(PanelHeight - 60f, 0f);
			_detailPanel.Top.Set(40f, 0f);
			_detailPanel.HAlign = 1f;
			_detailPanel.SetPadding(8f);
			_root.Append(_detailPanel);

			_detailTitle = new UIText(Language.GetText("Mods.MacroMod.UI.NoSelection"), 1.0f, true);
			_detailTitle.Top.Set(0f, 0f);
			_detailPanel.Append(_detailTitle);

			_detailKeybind = new UIText(string.Empty, 0.85f);
			_detailKeybind.Top.Set(28f, 0f);
			_detailPanel.Append(_detailKeybind);

			_detailStatus = new UIText(string.Empty, 0.8f);
			_detailStatus.Top.Set(50f, 0f);
			_detailPanel.Append(_detailStatus);

			var sourcePanel = new UIPanel { BackgroundColor = new Color(20, 25, 50) };
			sourcePanel.Width.Set(0f, 1f);
			sourcePanel.Height.Set(-160f, 1f);
			sourcePanel.Top.Set(76f, 0f);
			sourcePanel.SetPadding(4f);
			_detailPanel.Append(sourcePanel);

			_detailLines = new UIList { ListPadding = 2f };
			_detailLines.Width.Set(-26f, 1f);
			_detailLines.Height.Set(0f, 1f);
			sourcePanel.Append(_detailLines);

			_detailScroll = new UIScrollbar();
			_detailScroll.Height.Set(0f, 1f);
			_detailScroll.HAlign = 1f;
			sourcePanel.Append(_detailScroll);
			_detailLines.SetScrollbar(_detailScroll);

			// bottom row of buttons
			AppendButton("Mods.MacroMod.UI.Run", 0f, () => {
				if (_selected != null) MacroSystem.StartMacro(_selected);
			});
			AppendButton("Mods.MacroMod.UI.Reload", 1f, () => {
				MacroLibrary.ReloadAll();
				Refresh();
			});
			AppendButton("Mods.MacroMod.UI.EditExternal", 2f, () => {
				if (_selected == null) return;
				string path = Path.Combine(MacroLibrary.MacroDirectory, _selected + MacroLibrary.FileExtension);
				try {
					System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
						FileName = path,
						UseShellExecute = true,
					});
				}
				catch (Exception e) {
					Main.NewText("MacroMod: " + e.Message, Color.IndianRed);
				}
			});
			AppendButton("Mods.MacroMod.UI.OpenFolder", 3f, () => {
				try {
					System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo {
						FileName = MacroLibrary.MacroDirectory,
						UseShellExecute = true,
					});
				}
				catch (Exception e) {
					Main.NewText("MacroMod: " + e.Message, Color.IndianRed);
				}
			});
			AppendButton("Mods.MacroMod.UI.Delete", 4f, () => {
				if (_selected == null) return;
				MacroLibrary.DeleteMacro(_selected);
				_selected = null;
				Refresh();
			});

			AppendKeybindRow();
		}

		private UITextPanel<string> MakeButton(string langKey, float ty, Action<UIElement> onClick)
		{
			var btn = new UITextPanel<string>(Language.GetTextValue(langKey), 0.85f, true);
			btn.Width.Set(0f, 0.32f);
			btn.Height.Set(28f, 0f);
			btn.OnLeftClick += (_, e) => onClick(e);
			return btn;
		}

		private void AppendButton(string langKey, float index, Action act)
		{
			var btn = new UITextPanel<string>(Language.GetTextValue(langKey), 0.8f, true);
			btn.Width.Set(0f, 0.19f);
			btn.Height.Set(30f, 0f);
			btn.HAlign = index / 4f;
			btn.Top.Set(-72f, 1f);
			btn.OnLeftClick += (_, __) => {
				try { act(); } catch (Exception e) { Main.NewText("MacroMod: " + e.Message, Color.IndianRed); }
			};
			_detailPanel.Append(btn);
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
			// Toggle: if already bound to this slot, clear; otherwise assign and clear other slots holding this macro.
			bool alreadyBound = string.Equals(p.GetSlot(slotIndex), _selected, StringComparison.OrdinalIgnoreCase);
			if (alreadyBound) {
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

		// ---- refresh / selection -----------------------------------------

		public void Refresh()
		{
			_macroList?.Clear();
			foreach (var m in MacroLibrary.All.OrderBy(m => m.Name)) {
				var entry = new MacroListItem(m, m.Name == _selected, () => Select(m.Name));
				_macroList.Add(entry);
			}
			RefreshDetail();
		}

		public void Select(string name)
		{
			_selected = name;
			Refresh();
		}

		private void RefreshDetail()
		{
			if (_detailLines == null) return;
			_detailLines.Clear();
			var macro = _selected != null ? MacroLibrary.FindMacro(_selected) : null;
			if (macro == null) {
				_detailTitle.SetText(Language.GetText("Mods.MacroMod.UI.NoSelection"));
				_detailKeybind.SetText(string.Empty);
				_detailStatus.SetText(string.Empty);
				return;
			}
			_detailTitle.SetText(macro.Name);

			var p = Main.LocalPlayer?.GetModPlayer<MacroPlayer>();
			int boundSlot = -1;
			if (p != null) {
				for (int i = 0; i < MacroKeybindSystem.SlotCount; i++) {
					if (string.Equals(p.GetSlot(i), macro.Name, StringComparison.OrdinalIgnoreCase)) {
						boundSlot = i; break;
					}
				}
			}
			_detailKeybind.SetText(boundSlot < 0
				? Language.GetTextValue("Mods.MacroMod.UI.Unbound")
				: string.Format(Language.GetTextValue("Mods.MacroMod.UI.Bound"), boundSlot + 1));

			_detailStatus.SetText(macro.HasError
				? string.Format(Language.GetTextValue("Mods.MacroMod.UI.ParseError"), macro.ParseError)
				: string.Format(Language.GetTextValue("Mods.MacroMod.UI.Lines"), macro.Source?.Split('\n').Length ?? 0));

			foreach (string raw in (macro.Source ?? string.Empty).Replace("\r\n", "\n").Split('\n')) {
				var t = new UIText(raw.Length == 0 ? " " : raw, 0.78f) { TextColor = ColorForLine(raw) };
				_detailLines.Add(t);
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

		// ---- helper element ----------------------------------------------

		private class MacroListItem : UIPanel
		{
			private readonly Action _onClick;
			public MacroListItem(Macro macro, bool selected, Action onClick)
			{
				_onClick = onClick;
				BackgroundColor = selected ? new Color(120, 100, 200) : new Color(50, 60, 110);
				Width.Set(0f, 1f);
				Height.Set(28f, 0f);
				SetPadding(4f);
				var t = new UIText(macro.Name + (macro.HasError ? " (!)" : string.Empty), 0.9f);
				Append(t);
				OnLeftClick += (_, __) => _onClick?.Invoke();
			}
		}

		// ---- drag-friendly root panel ------------------------------------

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
