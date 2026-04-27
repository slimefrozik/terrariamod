using System.Collections.Generic;
using Terraria;
using Terraria.ID;
using Terraria.ModLoader;

namespace MacroMod.Common.Macros
{
	/// <summary>
	/// Looks up an item ID across <em>all</em> currently loaded mods.  Accepts
	/// (in order):
	/// <list type="bullet">
	///   <item>integer item id ("1234")</item>
	///   <item><c>ModName/InternalName</c> for modded items</item>
	///   <item>vanilla internal name (<see cref="ItemID.Search"/>) — case insensitive</item>
	///   <item>internal name of any modded item, unique across loaded mods</item>
	///   <item>display name as shown in-game (Lang.GetItemNameValue), case insensitive</item>
	///   <item>name with underscores in place of spaces ("Wooden_Sword")</item>
	/// </list>
	/// Results are cached per session.
	/// </summary>
	public static class ItemResolver
	{
		private static readonly Dictionary<string, int> Cache = new();

		public static void ResetCache() => Cache.Clear();

		public static bool TryResolve(string raw, out int itemId)
		{
			itemId = 0;
			if (string.IsNullOrWhiteSpace(raw)) return false;

			string key = raw.Trim();
			if (Cache.TryGetValue(key, out itemId)) return itemId > 0;

			int found = ResolveUncached(key);
			Cache[key] = found;
			itemId = found;
			return found > 0;
		}

		private static int ResolveUncached(string raw)
		{
			// 1. Integer id.
			if (int.TryParse(raw, out int n) && n > 0 && n < ItemLoader.ItemCount) {
				return n;
			}

			// 2. ModName/InternalName.
			if (raw.Contains('/')) {
				var parts = raw.Split('/', 2);
				if (parts.Length == 2 && ModContent.TryFind<ModItem>(parts[0], parts[1], out var mi)) {
					return mi.Type;
				}
			}

			string normalized = raw.Replace('_', ' ').Trim();

			// 3. Vanilla internal name.
			if (ItemID.Search.TryGetId(raw, out int vanilla)) return vanilla;
			if (ItemID.Search.TryGetId(normalized.Replace(" ", string.Empty), out vanilla)) return vanilla;

			// 4/5. Iterate all loaded items and match internal/display name.
			int displayMatch = 0;
			int internalMatch = 0;
			string lower = normalized.ToLowerInvariant();
			string lowerTight = lower.Replace(" ", string.Empty);

			for (int i = 1; i < ItemLoader.ItemCount; i++) {
				ModItem mi = ItemLoader.GetItem(i);
				if (mi != null) {
					if (string.Equals(mi.Name, raw, System.StringComparison.OrdinalIgnoreCase)) {
						return i; // unique internal name match wins.
					}
					if (internalMatch == 0 && mi.Name.Replace(" ", string.Empty)
							.Equals(lowerTight, System.StringComparison.OrdinalIgnoreCase)) {
						internalMatch = i;
					}
				}
				if (displayMatch == 0) {
					string disp = Lang.GetItemNameValue(i);
					if (!string.IsNullOrEmpty(disp) && disp.Equals(normalized, System.StringComparison.OrdinalIgnoreCase)) {
						displayMatch = i;
					}
				}
			}

			if (internalMatch > 0) return internalMatch;
			if (displayMatch > 0) return displayMatch;
			return 0;
		}

		// ---- helpers used elsewhere ---------------------------------------

		/// <summary>Tries to find any item in the player's inventory matching <paramref name="raw"/>.  Returns the slot index, or -1.</summary>
		public static int FindInventorySlot(Player player, string raw)
		{
			if (!TryResolve(raw, out int wanted)) return -1;
			for (int i = 0; i < player.inventory.Length; i++) {
				if (!player.inventory[i].IsAir && player.inventory[i].type == wanted) {
					return i;
				}
			}
			return -1;
		}

		/// <summary>Like <see cref="FindInventorySlot"/> but only checks hotbar slots 0-9.</summary>
		public static int FindHotbarSlot(Player player, string raw)
		{
			if (!TryResolve(raw, out int wanted)) return -1;
			for (int i = 0; i < 10 && i < player.inventory.Length; i++) {
				if (!player.inventory[i].IsAir && player.inventory[i].type == wanted) {
					return i;
				}
			}
			return -1;
		}
	}
}
