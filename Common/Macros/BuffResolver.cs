using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace MacroMod.Common.Macros
{
	/// <summary>Resolves a buff name to a buff id across all loaded mods.</summary>
	public static class BuffResolver
	{
		private static readonly Dictionary<string, int> Cache = new();

		public static void ResetCache() => Cache.Clear();

		public static bool TryResolve(string raw, out int buffId)
		{
			buffId = 0;
			if (string.IsNullOrWhiteSpace(raw)) return false;
			string key = raw.Trim();
			if (Cache.TryGetValue(key, out buffId)) return buffId > 0;

			int found = ResolveUncached(key);
			Cache[key] = found;
			buffId = found;
			return found > 0;
		}

		private static int ResolveUncached(string raw)
		{
			if (int.TryParse(raw, out int n) && n > 0 && n < BuffLoader.BuffCount) return n;

			if (raw.Contains('/')) {
				var parts = raw.Split('/', 2);
				if (parts.Length == 2 && ModContent.TryFind<ModBuff>(parts[0], parts[1], out var mb)) {
					return mb.Type;
				}
			}

			if (BuffID.Search.TryGetId(raw, out int vanilla)) return vanilla;

			string normalized = raw.Replace('_', ' ').Trim();
			for (int i = 1; i < BuffLoader.BuffCount; i++) {
				ModBuff mb = BuffLoader.GetBuff(i);
				if (mb != null && mb.Name.Equals(raw, System.StringComparison.OrdinalIgnoreCase)) return i;
				string disp = Lang.GetBuffName(i);
				if (!string.IsNullOrEmpty(disp) && disp.Equals(normalized, System.StringComparison.OrdinalIgnoreCase)) return i;
			}
			return 0;
		}
	}
}
