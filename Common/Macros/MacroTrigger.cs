using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Terraria;
using Terraria.ModLoader;

namespace MacroMod.Common.Macros
{
	/// <summary>
	/// Auto-execution trigger attached to a macro.  When enabled, the macro
	/// starts itself when the trigger condition transitions from false to
	/// true (edge-triggered, never spammed every tick).
	/// </summary>
	public class MacroTrigger
	{
		public TriggerKind Kind;
		public TriggerOp Op = TriggerOp.LessOrEqual;
		public float Number;
		public string Text = string.Empty;

		// Runtime state — used by MacroTriggerSystem to detect rising edges.
		// Not serialized.
		[NonSerialized] public bool LastValue;
		[NonSerialized] public bool Initialized;

		public MacroTrigger Clone() => new MacroTrigger {
			Kind = Kind, Op = Op, Number = Number, Text = Text,
		};

		public string Serialize()
		{
			// "# @trigger <kind> <op> <number-or-text>"
			return Kind switch {
				TriggerKind.HpBelow      => $"@trigger hp {OpToken(Op)} {Number.ToString(CultureInfo.InvariantCulture)}",
				TriggerKind.MpBelow      => $"@trigger mp {OpToken(Op)} {Number.ToString(CultureInfo.InvariantCulture)}",
				TriggerKind.FreeSlots    => $"@trigger free_slots {OpToken(Op)} {Number.ToString(CultureInfo.InvariantCulture)}",
				TriggerKind.InventoryFull=> "@trigger inventory_full",
				TriggerKind.BuffActive   => $"@trigger buff {Text}",
				TriggerKind.BuffMissing  => $"@trigger nobuff {Text}",
				TriggerKind.BossNearby   => "@trigger boss",
				TriggerKind.EnemyNearby  => $"@trigger enemy_within {Number.ToString(CultureInfo.InvariantCulture)}",
				TriggerKind.NightTime    => "@trigger night",
				TriggerKind.DayTime      => "@trigger day",
				TriggerKind.OnHit        => "@trigger on_hit",
				_ => "@trigger unknown",
			};
		}

		public static MacroTrigger TryParse(string text)
		{
			// Expects the @trigger header without the leading '#'.
			text = text.Trim();
			if (!text.StartsWith("@trigger", StringComparison.Ordinal)) return null;
			text = text.Substring("@trigger".Length).Trim();
			if (text.Length == 0) return null;

			string[] parts = text.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
			if (parts.Length == 0) return null;

			string head = parts[0].ToLowerInvariant();
			var t = new MacroTrigger();
			switch (head) {
				case "hp":
				case "hp_pct":
					t.Kind = TriggerKind.HpBelow;
					ParseOpAndNumber(parts, t);
					return t;
				case "mp":
				case "mp_pct":
					t.Kind = TriggerKind.MpBelow;
					ParseOpAndNumber(parts, t);
					return t;
				case "free_slots":
					t.Kind = TriggerKind.FreeSlots;
					ParseOpAndNumber(parts, t);
					return t;
				case "inventory_full":
					t.Kind = TriggerKind.InventoryFull;
					return t;
				case "buff":
					t.Kind = TriggerKind.BuffActive;
					t.Text = parts.Length >= 2 ? string.Join(" ", parts, 1, parts.Length - 1) : string.Empty;
					return t;
				case "nobuff":
					t.Kind = TriggerKind.BuffMissing;
					t.Text = parts.Length >= 2 ? string.Join(" ", parts, 1, parts.Length - 1) : string.Empty;
					return t;
				case "boss":
					t.Kind = TriggerKind.BossNearby;
					return t;
				case "enemy_within":
					t.Kind = TriggerKind.EnemyNearby;
					if (parts.Length >= 2 && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float n)) t.Number = n;
					return t;
				case "night":
					t.Kind = TriggerKind.NightTime;
					return t;
				case "day":
					t.Kind = TriggerKind.DayTime;
					return t;
				case "on_hit":
					t.Kind = TriggerKind.OnHit;
					return t;
			}
			return null;
		}

		private static void ParseOpAndNumber(string[] parts, MacroTrigger t)
		{
			if (parts.Length >= 3) {
				t.Op = ParseOp(parts[1]);
				if (float.TryParse(parts[2], NumberStyles.Float, CultureInfo.InvariantCulture, out float n)) t.Number = n;
			}
			else if (parts.Length == 2 && float.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out float n2)) {
				t.Op = TriggerOp.LessOrEqual;
				t.Number = n2;
			}
		}

		private static TriggerOp ParseOp(string s) => s switch {
			"<"  => TriggerOp.Less,
			"<=" => TriggerOp.LessOrEqual,
			"="  => TriggerOp.Equal,
			"==" => TriggerOp.Equal,
			">"  => TriggerOp.Greater,
			">=" => TriggerOp.GreaterOrEqual,
			_ => TriggerOp.LessOrEqual,
		};

		private static string OpToken(TriggerOp op) => op switch {
			TriggerOp.Less => "<",
			TriggerOp.LessOrEqual => "<=",
			TriggerOp.Equal => "==",
			TriggerOp.Greater => ">",
			TriggerOp.GreaterOrEqual => ">=",
			_ => "<=",
		};

		public bool Evaluate(Player player)
		{
			if (player == null) return false;
			switch (Kind) {
				case TriggerKind.HpBelow: {
					float pct = player.statLifeMax2 > 0 ? player.statLife * 100f / player.statLifeMax2 : 100f;
					return Compare(pct, Op, Number);
				}
				case TriggerKind.MpBelow: {
					float pct = player.statManaMax2 > 0 ? player.statMana * 100f / player.statManaMax2 : 100f;
					return Compare(pct, Op, Number);
				}
				case TriggerKind.FreeSlots: {
					int free = 0;
					for (int i = 0; i < 50; i++) {
						if (player.inventory[i] == null || player.inventory[i].IsAir) free++;
					}
					return Compare(free, Op, Number);
				}
				case TriggerKind.InventoryFull: {
					for (int i = 0; i < 50; i++) {
						if (player.inventory[i] == null || player.inventory[i].IsAir) return false;
					}
					return true;
				}
				case TriggerKind.BuffActive:
					return BuffResolver.PlayerHasBuff(player, Text);
				case TriggerKind.BuffMissing:
					return !BuffResolver.PlayerHasBuff(player, Text);
				case TriggerKind.BossNearby: {
					for (int i = 0; i < Main.maxNPCs; i++) {
						var npc = Main.npc[i];
						if (npc.active && npc.boss && npc.life > 0) return true;
					}
					return false;
				}
				case TriggerKind.EnemyNearby: {
					float radiusSq = Number > 0 ? Number * Number : 320f * 320f;
					for (int i = 0; i < Main.maxNPCs; i++) {
						var npc = Main.npc[i];
						if (!npc.active || npc.friendly || npc.life <= 0) continue;
						if ((npc.Center - player.Center).LengthSquared() <= radiusSq) return true;
					}
					return false;
				}
				case TriggerKind.NightTime:
					return !Main.dayTime;
				case TriggerKind.DayTime:
					return Main.dayTime;
				case TriggerKind.OnHit:
					return player.immune; // approximated — true while iframes are active just after a hit
			}
			return false;
		}

		private static bool Compare(float left, TriggerOp op, float right) => op switch {
			TriggerOp.Less => left < right,
			TriggerOp.LessOrEqual => left <= right,
			TriggerOp.Equal => Math.Abs(left - right) < 0.001f,
			TriggerOp.Greater => left > right,
			TriggerOp.GreaterOrEqual => left >= right,
			_ => false,
		};
	}

	public enum TriggerKind
	{
		HpBelow,
		MpBelow,
		FreeSlots,
		InventoryFull,
		BuffActive,
		BuffMissing,
		BossNearby,
		EnemyNearby,
		NightTime,
		DayTime,
		OnHit,
	}

	public enum TriggerOp
	{
		Less, LessOrEqual, Equal, Greater, GreaterOrEqual,
	}

	public enum TriggerMatchMode
	{
		Any,    // OR — fire if any one trigger is true
		All,    // AND — fire only if every trigger is true
	}

	public static class MacroTriggerSerializer
	{
		// Serializes triggers as a header block at the top of the macro file:
		//   # @triggers any
		//   # @trigger hp <= 50
		//   # @trigger nobuff Well Fed
		//   # @endtriggers
		// The body follows untouched.  Comments-only output keeps .macro
		// hand-editable in any text editor.
		public static string SerializeHeader(IList<MacroTrigger> triggers, TriggerMatchMode mode)
		{
			if (triggers == null || triggers.Count == 0) return string.Empty;
			var sb = new StringBuilder();
			sb.Append("# @triggers ").Append(mode == TriggerMatchMode.All ? "all" : "any").Append('\n');
			foreach (var t in triggers) {
				sb.Append("#   ").Append(t.Serialize()).Append('\n');
			}
			sb.Append("# @endtriggers\n");
			return sb.ToString();
		}

		// Splits the source into (header, body).  Removes the trigger header
		// from the body so the parser never sees it.  Updates `triggers` and
		// `mode` in-place.
		public static string ExtractAndStrip(string source, List<MacroTrigger> triggers, ref TriggerMatchMode mode)
		{
			triggers.Clear();
			mode = TriggerMatchMode.Any;
			if (string.IsNullOrEmpty(source)) return source ?? string.Empty;

			string[] lines = source.Replace("\r\n", "\n").Split('\n');
			int i = 0;
			while (i < lines.Length && lines[i].TrimStart().Length == 0) i++; // skip leading blanks
			if (i >= lines.Length) return source;

			string head = lines[i].TrimStart();
			if (!head.StartsWith("# @triggers", StringComparison.Ordinal)
				&& !head.StartsWith("#@triggers", StringComparison.Ordinal)) return source;

			// Parse the mode token after @triggers
			int spaceIdx = head.IndexOf(' ', head.IndexOf("@triggers", StringComparison.Ordinal) + "@triggers".Length);
			if (spaceIdx > 0) {
				string m = head.Substring(spaceIdx).Trim().ToLowerInvariant();
				mode = m == "all" ? TriggerMatchMode.All : TriggerMatchMode.Any;
			}

			int bodyStart = -1;
			for (int j = i + 1; j < lines.Length; j++) {
				string t = lines[j].TrimStart();
				if (t.StartsWith("# @endtriggers", StringComparison.Ordinal)
					|| t.StartsWith("#@endtriggers", StringComparison.Ordinal)) {
					bodyStart = j + 1;
					break;
				}
				// Trigger entry: "#   @trigger ...", any leading "# " variation.
				if (t.StartsWith("#")) {
					string inner = t.Substring(1).TrimStart();
					var trig = MacroTrigger.TryParse(inner);
					if (trig != null) triggers.Add(trig);
				}
			}
			if (bodyStart < 0) return source; // malformed header — leave source alone

			var sb = new StringBuilder();
			for (int j = bodyStart; j < lines.Length; j++) {
				sb.Append(lines[j]);
				if (j < lines.Length - 1) sb.Append('\n');
			}
			return sb.ToString();
		}
	}
}
