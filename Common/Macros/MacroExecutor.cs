using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ID;
using Terraria.Localization;

namespace MacroMod.Common.Macros
{
	/// <summary>
	/// Steps through a parsed <see cref="Macro"/> one tick at a time,
	/// applying conditions, blocks, /wait pauses and item/buff actions.
	/// One executor lives per running macro instance (so two macros can run
	/// concurrently from different keybinds).
	/// </summary>
	public class MacroExecutor
	{
		private const int MaxStepsPerTick = 256;
		private const int MaxLoopIterations = 5000;
		private const int MaxStackDepth = 8;

		private readonly Macro _entryMacro;
		private readonly MacroContext _ctx;
		private readonly Stack<Frame> _stack = new();
		private int _waitTicks;
		private int _loopCount;
		private bool _done;
		// True for one tick after an If/ElseIf jumped because its condition failed.
		// While set, an ElseIf/Else we land on should be evaluated rather than skipped.
		private bool _enteringBranch;

		// Action latches for the host system to consume after each tick.
		public int PendingHotbarSlot = -1;
		public bool PendingUseItem;
		public string PendingChat;
		public Action<Player> PendingPlayerAction;

		// Persistent latches: set to true by /attack hold etc., cleared by
		// /attack release or when the macro finishes.  The host system mirrors
		// these onto Player.control* every tick while the macro is alive.
		public bool HoldUseItem;
		public bool HoldUseAlt;

		private class Frame
		{
			public Macro Macro;
			public int Ip;
		}

		public Macro Entry => _entryMacro;
		public bool IsFinished => _done;

		public MacroExecutor(Macro macro, Player player)
		{
			_entryMacro = macro;
			_ctx = new MacroContext(player);
			_stack.Push(new Frame { Macro = macro, Ip = 0 });
		}

		public void Tick()
		{
			if (_done) return;
			if (_waitTicks > 0) { _waitTicks--; return; }

			int steps = 0;
			while (steps < MaxStepsPerTick) {
				steps++;

				Frame f = _stack.Peek();
				if (f.Ip >= f.Macro.Program.Count) {
					_stack.Pop();
					if (_stack.Count == 0) { _done = true; return; }
					continue;
				}

				MacroLine line = f.Macro.Program[f.Ip];
				bool yielded = !ExecuteLine(f, line);
				if (_loopCount >= MaxLoopIterations) {
					Notify(Language.GetTextValue("Mods.MacroMod.LoopLimit"), Color.IndianRed);
					_done = true;
					return;
				}
				if (yielded) return;
			}
		}

		// ------------------- per-line dispatch -----------------------------

		private bool ExecuteLine(Frame f, MacroLine line)
		{
			List<MacroLine> program = f.Macro.Program;
			switch (line.Kind) {
				case MacroLineKind.Empty:
				case MacroLineKind.Comment:
					f.Ip++;
					return true;

				case MacroLineKind.If: {
					_enteringBranch = false;
					bool cond = Conditions.Evaluate(line.Conditions, _ctx)
						&& EvaluateExprSafe(line.Args);
					if (cond) f.Ip++;
					else { f.Ip = Math.Max(line.JumpTarget, f.Ip + 1); _enteringBranch = true; }
					return true;
				}

				case MacroLineKind.ElseIf: {
					if (!_enteringBranch) {
						// Fell through after a previous branch's body executed.
						f.Ip = line.ChainEnd + 1;
						return true;
					}
					bool cond = Conditions.Evaluate(line.Conditions, _ctx)
						&& EvaluateExprSafe(line.Args);
					if (cond) { _enteringBranch = false; f.Ip++; }
					else { f.Ip = Math.Max(line.JumpTarget, f.Ip + 1); }
					return true;
				}

				case MacroLineKind.Else: {
					if (!_enteringBranch) {
						f.Ip = line.ChainEnd + 1;
					}
					else {
						_enteringBranch = false;
						f.Ip++;
					}
					return true;
				}

				case MacroLineKind.EndIf:
					_enteringBranch = false;
					f.Ip++;
					return true;

				case MacroLineKind.While: {
					bool cond = Conditions.Evaluate(line.Conditions, _ctx)
						&& EvaluateExprSafe(line.Args);
					if (cond) f.Ip++;
					else f.Ip = line.JumpTarget + 1;
					return true;
				}

				case MacroLineKind.EndWhile:
					_loopCount++;
					f.Ip = line.JumpTarget;
					return true;

				case MacroLineKind.Loop:
					if (!Conditions.Evaluate(line.Conditions, _ctx)) { f.Ip++; return true; }
					_loopCount++;
					f.Ip = 0;
					return true;

				case MacroLineKind.Stop:
					if (Conditions.Evaluate(line.Conditions, _ctx)) {
						_done = true;
						return false;
					}
					f.Ip++;
					return true;

				case MacroLineKind.Assign:
					if (Conditions.Evaluate(line.Conditions, _ctx)
						&& MacroParser.TrySplitAssignment(line.Args, out string varName, out string expr)) {
						try { _ctx.SetVariable(varName, Expression.Eval(expr, _ctx)); }
						catch (Exception e) { Notify($"set: {e.Message}", Color.IndianRed); }
					}
					f.Ip++;
					return true;

				case MacroLineKind.Command:
					if (!Conditions.Evaluate(line.Conditions, _ctx)) { f.Ip++; return true; }
					return ExecuteCommand(f, line);
			}
			f.Ip++;
			return true;
		}

		private bool EvaluateExprSafe(string expr)
		{
			if (string.IsNullOrWhiteSpace(expr)) return true;
			try { return Expression.EvalBool(expr, _ctx); }
			catch (Exception e) { Notify($"if: {e.Message}", Color.IndianRed); return false; }
		}

		// ------------------- commands --------------------------------------

		private bool ExecuteCommand(Frame f, MacroLine line)
		{
			string cmd = line.Command;
			string args = line.Args ?? string.Empty;

			switch (cmd) {
				case "use":
				case "cast":
					f.Ip++;
					return DoUseItem(args);
				case "swap":
				case "select":
					f.Ip++;
					return DoSelectItem(args);
				case "drop":
					f.Ip++;
					return DoDrop(args);
				case "buff":
					f.Ip++;
					return DoBuff(args);
				case "debuff":
				case "removebuff":
					f.Ip++;
					return DoDebuff(args);
				case "wait":
				case "sleep":
					f.Ip++;
					_waitTicks = ParseDelay(args);
					return _waitTicks <= 0;
				case "say":
				case "chat":
					f.Ip++;
					return DoSay(args);
				case "print":
				case "log":
					Notify(InterpolateText(args), Color.LightGoldenrodYellow);
					f.Ip++;
					return true;
				case "run":
				case "call":
					return DoRun(f, args);
				case "quickheal":
					PendingPlayerAction += p => p.QuickHeal();
					f.Ip++;
					return false;
				case "quickmana":
					PendingPlayerAction += p => p.QuickMana();
					f.Ip++;
					return false;
				case "quickbuff":
					PendingPlayerAction += p => p.QuickBuff();
					f.Ip++;
					return false;
				case "mount":
					PendingPlayerAction += p => p.QuickMount();
					f.Ip++;
					return false;
				case "recall":
				case "mirror":
					f.Ip++;
					return DoUseFirst(new[] {
						"MagicMirror", "IceMirror", "CellPhone", "Shellphone", "RecallPotion",
					});
				case "attack":
				case "useitem":
					f.Ip++;
					return DoAttack(args, alt: false);
				case "altattack":
				case "rightclick":
					f.Ip++;
					return DoAttack(args, alt: true);
				case "release":
					f.Ip++;
					HoldUseItem = false;
					HoldUseAlt = false;
					return true;
				default:
					Notify(string.Format(Language.GetTextValue("Mods.MacroMod.UnknownCommand"), cmd), Color.IndianRed);
					f.Ip++;
					return true;
			}
		}

		private bool DoUseItem(string args)
		{
			string firstWord = SplitFirst(args, out _);
			if (string.IsNullOrEmpty(firstWord)) return true;
			int slot = ItemResolver.FindHotbarSlot(_ctx.Player, firstWord);
			if (slot < 0) {
				int invSlot = ItemResolver.FindInventorySlot(_ctx.Player, firstWord);
				if (invSlot < 0) {
					Notify(string.Format(Language.GetTextValue("Mods.MacroMod.NoSuchItem"), firstWord), Color.IndianRed);
					return true;
				}
				MoveToHotbar(invSlot);
				slot = ItemResolver.FindHotbarSlot(_ctx.Player, firstWord);
				if (slot < 0) return true;
			}
			PendingHotbarSlot = slot;
			PendingUseItem = true;
			return false;
		}

		private bool DoUseFirst(IEnumerable<string> items)
		{
			foreach (string item in items) {
				int slot = ItemResolver.FindHotbarSlot(_ctx.Player, item);
				if (slot < 0) {
					int invSlot = ItemResolver.FindInventorySlot(_ctx.Player, item);
					if (invSlot < 0) continue;
					MoveToHotbar(invSlot);
					slot = ItemResolver.FindHotbarSlot(_ctx.Player, item);
				}
				if (slot < 0) continue;
				PendingHotbarSlot = slot;
				PendingUseItem = true;
				return false;
			}
			return true;
		}

		private bool DoSelectItem(string args)
		{
			string firstWord = SplitFirst(args, out _);
			// Numeric argument (1..10) selects that hotbar slot directly.
			if (int.TryParse(firstWord, out int n) && n >= 1 && n <= 10) {
				PendingHotbarSlot = n - 1;
				return true;
			}
			int slot = ItemResolver.FindHotbarSlot(_ctx.Player, firstWord);
			if (slot < 0) {
				int invSlot = ItemResolver.FindInventorySlot(_ctx.Player, firstWord);
				if (invSlot >= 0) MoveToHotbar(invSlot);
				slot = ItemResolver.FindHotbarSlot(_ctx.Player, firstWord);
			}
			if (slot >= 0) PendingHotbarSlot = slot;
			return true;
		}

		private bool DoDrop(string args)
		{
			string itemName = SplitFirst(args, out string rest);
			int amount = int.TryParse(rest.Trim(), out int n) ? n : int.MaxValue;
			int slot = ItemResolver.FindInventorySlot(_ctx.Player, itemName);
			if (slot < 0) return true;
			Item it = _ctx.Player.inventory[slot];
			int drop = Math.Min(amount, it.stack);
			Item.NewItem(new Terraria.DataStructures.EntitySource_Misc("MacroMod"),
				_ctx.Player.Center, Vector2.Zero, it.type, drop);
			it.stack -= drop;
			if (it.stack <= 0) it.TurnToAir();
			return true;
		}

		private bool DoBuff(string args)
		{
			string name = SplitFirst(args, out string rest);
			int seconds = int.TryParse(rest.Trim(), out int s) ? s : 60;
			if (BuffResolver.TryResolve(name, out int id)) {
				_ctx.Player.AddBuff(id, seconds * 60);
			}
			else {
				Notify(string.Format(Language.GetTextValue("Mods.MacroMod.NoSuchBuff"), name), Color.IndianRed);
			}
			return true;
		}

		private bool DoDebuff(string args)
		{
			string name = SplitFirst(args, out _);
			if (BuffResolver.TryResolve(name, out int id)) {
				_ctx.Player.ClearBuff(id);
			}
			return true;
		}

		private bool DoAttack(string args, bool alt)
		{
			string mode = (args ?? string.Empty).Trim().ToLowerInvariant();
			if (mode.Length == 0) mode = "once";
			switch (mode) {
				case "hold":
				case "start":
				case "begin":
					if (alt) HoldUseAlt = true;
					else HoldUseItem = true;
					return true;
				case "release":
				case "stop":
				case "end":
					if (alt) HoldUseAlt = false;
					else HoldUseItem = false;
					return true;
				case "once":
				case "click":
				default:
					if (alt) HoldUseAlt = true;
					else PendingUseItem = true;
					// alt only fires for one tick by virtue of DrainPending below.
					return false;
			}
		}

		private bool DoSay(string text)
		{
			string interpolated = InterpolateText(text);
			if (string.IsNullOrEmpty(interpolated)) return true;
			PendingChat = interpolated;
			return true;
		}

		private bool DoRun(Frame f, string args)
		{
			string name = SplitFirst(args, out _);
			Macro target = MacroLibrary.FindMacro(name);
			if (target == null) {
				Notify(string.Format(Language.GetTextValue("Mods.MacroMod.NoSuchMacro"), name), Color.IndianRed);
				f.Ip++;
				return true;
			}
			if (_stack.Count >= MaxStackDepth) {
				Notify(Language.GetTextValue("Mods.MacroMod.StackOverflow"), Color.IndianRed);
				f.Ip++;
				return true;
			}
			f.Ip++; // resume after the /run on return
			_stack.Push(new Frame { Macro = target, Ip = 0 });
			return true;
		}

		// ------------------- helpers ---------------------------------------

		private static int ParseDelay(string s)
		{
			if (string.IsNullOrWhiteSpace(s)) return 60;
			s = s.Trim().ToLowerInvariant();
			double mult = 60; // seconds by default
			if (s.EndsWith("ms")) { mult = 0.06; s = s[..^2]; }
			else if (s.EndsWith("ticks")) { mult = 1; s = s[..^5]; }
			else if (s.EndsWith("t")) { mult = 1; s = s[..^1]; }
			else if (s.EndsWith("s")) { mult = 60; s = s[..^1]; }
			else if (s.EndsWith("m")) { mult = 60 * 60; s = s[..^1]; }
			if (!double.TryParse(s.Trim(), System.Globalization.NumberStyles.Float,
				System.Globalization.CultureInfo.InvariantCulture, out double v)) return 60;
			return Math.Max(1, (int)Math.Round(v * mult));
		}

		private static string SplitFirst(string s, out string rest)
		{
			rest = string.Empty;
			if (string.IsNullOrEmpty(s)) return string.Empty;
			s = s.Trim();
			int i = 0;
			while (i < s.Length && !char.IsWhiteSpace(s[i])) i++;
			string head = s.Substring(0, i);
			rest = i < s.Length ? s.Substring(i + 1).TrimStart() : string.Empty;
			return head;
		}

		private string InterpolateText(string text)
		{
			if (string.IsNullOrEmpty(text)) return string.Empty;
			var sb = new System.Text.StringBuilder();
			int i = 0;
			while (i < text.Length) {
				if (text[i] == '{') {
					int close = text.IndexOf('}', i);
					if (close > i) {
						string body = text.Substring(i + 1, close - i - 1);
						try { sb.Append(Expression.ToText(Expression.Eval(body, _ctx))); }
						catch (Exception e) { sb.Append("{err:" + e.Message + "}"); }
						i = close + 1;
						continue;
					}
				}
				sb.Append(text[i]);
				i++;
			}
			return sb.ToString();
		}

		private void MoveToHotbar(int srcSlot)
		{
			if (srcSlot < 10 || srcSlot >= _ctx.Player.inventory.Length) return;
			Item moving = _ctx.Player.inventory[srcSlot];
			int targetSlot = _ctx.Player.selectedItem;
			if (targetSlot < 0 || targetSlot >= 10) targetSlot = 0;
			Item current = _ctx.Player.inventory[targetSlot];
			_ctx.Player.inventory[targetSlot] = moving;
			_ctx.Player.inventory[srcSlot] = current;
		}

		private static void Notify(string msg, Color color)
		{
			if (Main.netMode == NetmodeID.Server) return;
			Main.NewText(msg, color);
		}
	}
}
