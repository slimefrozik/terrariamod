using System.Collections.Generic;

namespace MacroMod.Common.Macros
{
	/// <summary>
	/// Evaluates the WoW-style square-bracket condition modifiers attached
	/// to macro lines.  Each modifier is a short keyword that maps to a
	/// boolean predicate.  Comma-separated entries inside one bracket group
	/// are AND'd; multiple bracket groups are OR'd.
	///
	/// Supported modifiers (case-insensitive, leading <c>!</c> negates):
	///   mod:shift / mod:ctrl / mod:alt
	///   hp&lt;X / hp&gt;X / hp=X (X is a percent of max hp)
	///   mp&lt;X / mp&gt;X / mp=X
	///   hasbuff:Name / buff:Name / nobuff:Name
	///   hasitem:Name / item:Name / noitem:Name
	///   equipped:Name / holding:Name
	///   boss / hostile / mounted / wet / water / lava / honey
	///   day / night / hardmode / expert / master / underground / surface
	///   true / false  (always passes / fails — handy when scripting)
	///   any free-form expression — evaluated through <see cref="Expression"/>
	/// </summary>
	public static class Conditions
	{
		public static bool Evaluate(List<List<string>> groups, MacroContext ctx)
		{
			if (groups == null || groups.Count == 0) return true;
			// OR across groups.
			foreach (var group in groups) {
				if (EvaluateGroup(group, ctx)) return true;
			}
			return false;
		}

		private static bool EvaluateGroup(List<string> group, MacroContext ctx)
		{
			if (group == null || group.Count == 0) return true;
			foreach (string raw in group) {
				if (!EvaluateOne(raw, ctx)) return false;
			}
			return true;
		}

		private static bool EvaluateOne(string raw, MacroContext ctx)
		{
			string c = raw.Trim();
			if (c.Length == 0) return true;

			bool negate = false;
			if (c.StartsWith("!")) { negate = true; c = c.Substring(1).Trim(); }

			bool result = EvaluateBody(c, ctx);
			return negate ? !result : result;
		}

		private static bool EvaluateBody(string c, MacroContext ctx)
		{
			string lower = c.ToLowerInvariant();

			// Direct keyword booleans.
			switch (lower) {
				case "true": return true;
				case "false": return false;
				case "boss": return Expression.ToBool(ctx.CallFunction("boss", new List<object>()));
				case "hostile": return Expression.ToBool(ctx.CallFunction("hostile", new List<object>()));
				case "mounted": return Expression.ToBool(ctx.CallFunction("mounted", new List<object>()));
				case "wet": return Expression.ToBool(ctx.CallFunction("wet", new List<object>()));
				case "water": return Expression.ToBool(ctx.CallFunction("water", new List<object>()));
				case "lava": return Expression.ToBool(ctx.CallFunction("lava", new List<object>()));
				case "honey": return Expression.ToBool(ctx.CallFunction("honey", new List<object>()));
				case "day": return Expression.ToBool(ctx.CallFunction("isday", new List<object>()));
				case "night": return Expression.ToBool(ctx.CallFunction("isnight", new List<object>()));
				case "hardmode": return Expression.ToBool(ctx.CallFunction("hardmode", new List<object>()));
				case "expert": return Expression.ToBool(ctx.CallFunction("expert", new List<object>()));
				case "master": return Expression.ToBool(ctx.CallFunction("master", new List<object>()));
				case "underground": return Expression.ToBool(ctx.CallFunction("underground", new List<object>()));
				case "surface": return Expression.ToBool(ctx.CallFunction("surface", new List<object>()));
			}

			// Key:value style.
			int colon = c.IndexOf(':');
			if (colon > 0) {
				string key = c.Substring(0, colon).Trim().ToLowerInvariant();
				string val = c.Substring(colon + 1).Trim();
				switch (key) {
					case "mod":
					case "modifier":
						return val.ToLowerInvariant() switch {
							"shift" => Expression.ToBool(ctx.CallFunction("shift", new List<object>())),
							"ctrl" => Expression.ToBool(ctx.CallFunction("ctrl", new List<object>())),
							"alt" => Expression.ToBool(ctx.CallFunction("alt", new List<object>())),
							_ => false,
						};
					case "hasbuff":
					case "buff":
						return ctx.PlayerHasBuff(val);
					case "nobuff":
						return !ctx.PlayerHasBuff(val);
					case "hasitem":
					case "item":
						return ctx.PlayerHasItem(val, out _);
					case "noitem":
						return !ctx.PlayerHasItem(val, out _);
					case "equipped":
					case "holding":
						return ctx.PlayerHolding(val);
					case "time":
						return val.ToLowerInvariant() switch {
							"day" => Expression.ToBool(ctx.CallFunction("isday", new List<object>())),
							"night" => Expression.ToBool(ctx.CallFunction("isnight", new List<object>())),
							_ => false,
						};
					case "moonphase":
						return (int)Expression.ToNumber(ctx.CallFunction("moonphase", new List<object>())) ==
							(int)Expression.ToNumber(val);
					case "rand":
						double chance = Expression.ToNumber(val);
						return Terraria.Main.rand.NextDouble() < chance;
				}
			}

			// hp<50 / mp>=20 / defense>=10 — numeric comparisons.
			foreach (string op in new[] { ">=", "<=", "==", "!=", "=", ">", "<" }) {
				int idx = lower.IndexOf(op, System.StringComparison.Ordinal);
				if (idx > 0) {
					string left = c.Substring(0, idx).Trim();
					string right = c.Substring(idx + op.Length).Trim();
					string realOp = op == "=" ? "==" : op;
					try {
						double l = Expression.EvalNumber(MapShortName(left), ctx);
						double r = Expression.EvalNumber(right, ctx);
						return realOp switch {
							"==" => System.Math.Abs(l - r) < 1e-9,
							"!=" => System.Math.Abs(l - r) >= 1e-9,
							"<" => l < r,
							"<=" => l <= r,
							">" => l > r,
							">=" => l >= r,
							_ => false,
						};
					}
					catch { return false; }
				}
			}

			// Otherwise — try to evaluate as an arbitrary expression.
			try { return Expression.EvalBool(c, ctx); }
			catch { return false; }
		}

		// hp / mp / health / mana shortcuts default to the percent function.
		private static string MapShortName(string s)
		{
			return s.ToLowerInvariant() switch {
				"hp" => "hppct()",
				"health" => "hppct()",
				"mp" => "mppct()",
				"mana" => "mppct()",
				_ => s,
			};
		}
	}
}
