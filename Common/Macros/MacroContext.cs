using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
using Terraria.ID;

namespace MacroMod.Common.Macros
{
	/// <summary>
	/// Per-execution-instance state shared between an executor and the
	/// expression evaluator.  Holds variables, exposes "WoW-style" predicates
	/// as functions, and snapshots player data.
	/// </summary>
	public class MacroContext
	{
		public Player Player;
		public Dictionary<string, object> Variables = new(StringComparer.OrdinalIgnoreCase);

		public MacroContext(Player player)
		{
			Player = player;
		}

		public object GetVariable(string name)
		{
			if (Variables.TryGetValue(name, out var v)) return v;
			// Built-in variables — same names that work as functions.
			return CallFunction(name, new List<object>());
		}

		public void SetVariable(string name, object value)
		{
			Variables[name] = value;
		}

		// --------------------- function dispatch ---------------------------

		public object CallFunction(string name, List<object> args)
		{
			string n = name.ToLowerInvariant();
			switch (n) {
				// player stats ------------------------------------------------
				case "hp":
				case "health":
					return Player == null ? 0 : (double)Player.statLife;
				case "hpmax":
				case "maxhp":
					return Player == null ? 0 : (double)Player.statLifeMax2;
				case "hppct":
				case "hppercent":
					return Player == null || Player.statLifeMax2 == 0 ? 0 : 100.0 * Player.statLife / Player.statLifeMax2;
				case "mp":
				case "mana":
					return Player == null ? 0 : (double)Player.statMana;
				case "mpmax":
				case "maxmp":
					return Player == null ? 0 : (double)Player.statManaMax2;
				case "mppct":
				case "mppercent":
					return Player == null || Player.statManaMax2 == 0 ? 0 : 100.0 * Player.statMana / Player.statManaMax2;
				case "defense":
					return Player == null ? 0 : (double)Player.statDefense;

				// world ------------------------------------------------------
				case "time":
					return Main.dayTime ? "day" : "night";
				case "isday":
					return Main.dayTime;
				case "isnight":
					return !Main.dayTime;
				case "hardmode":
					return Main.hardMode;
				case "expert":
					return Main.expertMode;
				case "master":
					return Main.masterMode;
				case "moonphase":
					return (double)Main.moonPhase;
				case "rand":
					return args.Count >= 2
						? Main.rand.NextFloat((float)Expression.ToNumber(args[0]), (float)Expression.ToNumber(args[1]))
						: Main.rand.NextDouble();

				// state predicates ------------------------------------------
				case "mounted":
					return Player != null && Player.mount != null && Player.mount.Active;
				case "wet":
					return Player != null && Player.wet;
				case "water":
					return Player != null && Player.wet && !Player.lavaWet && !Player.honeyWet;
				case "lava":
					return Player != null && Player.lavaWet;
				case "honey":
					return Player != null && Player.honeyWet;
				case "underground":
					return Player != null && Player.ZoneRockLayerHeight;
				case "surface":
					return Player != null && Player.ZoneOverworldHeight;
				case "boss":
					return AnyBoss();
				case "hostile":
					return AnyHostile(args.Count > 0 ? (float)Expression.ToNumber(args[0]) : 800f);

				// string/number helpers used by /set ------------------------
				case "len":
					return args.Count == 0 ? 0 : (double)Expression.ToText(args[0]).Length;
				case "min":
					return MinMax(args, true);
				case "max":
					return MinMax(args, false);
				case "abs":
					return args.Count == 0 ? 0 : Math.Abs(Expression.ToNumber(args[0]));
				case "floor":
					return args.Count == 0 ? 0 : Math.Floor(Expression.ToNumber(args[0]));
				case "ceil":
					return args.Count == 0 ? 0 : Math.Ceiling(Expression.ToNumber(args[0]));
				case "round":
					return args.Count == 0 ? 0 : Math.Round(Expression.ToNumber(args[0]));

				// keyboard / inputs -----------------------------------------
				case "shift":
					return PlayerInput.Triggers.Current.KeyStatus.TryGetValue("LeftShift", out bool ls) && ls
						|| Keyboard.GetState().IsKeyDown(Keys.LeftShift) || Keyboard.GetState().IsKeyDown(Keys.RightShift);
				case "ctrl":
					return Keyboard.GetState().IsKeyDown(Keys.LeftControl) || Keyboard.GetState().IsKeyDown(Keys.RightControl);
				case "alt":
					return Keyboard.GetState().IsKeyDown(Keys.LeftAlt) || Keyboard.GetState().IsKeyDown(Keys.RightAlt);

				// content predicates ----------------------------------------
				case "buff":
				case "hasbuff":
					return args.Count > 0 && PlayerHasBuff(Expression.ToText(args[0]));
				case "item":
				case "hasitem":
					return args.Count > 0 && PlayerHasItem(Expression.ToText(args[0]), out _);
				case "equipped":
					return args.Count > 0 && PlayerHolding(Expression.ToText(args[0]));
				case "itemcount":
					return args.Count > 0 ? (double)CountItem(Expression.ToText(args[0])) : 0;

				// utility ---------------------------------------------------
				case "name":
				case "playername":
					return Player?.name ?? string.Empty;
			}
			return 0;
		}

		// --------------------- helper queries ------------------------------

		private static double MinMax(List<object> args, bool min)
		{
			if (args.Count == 0) return 0;
			double r = Expression.ToNumber(args[0]);
			for (int i = 1; i < args.Count; i++) {
				double v = Expression.ToNumber(args[i]);
				if (min ? v < r : v > r) r = v;
			}
			return r;
		}

		public bool PlayerHasBuff(string buff)
		{
			if (Player == null) return false;
			if (!BuffResolver.TryResolve(buff, out int id)) return false;
			return Player.HasBuff(id);
		}

		public bool PlayerHasItem(string itemName, out int slot)
		{
			slot = -1;
			if (Player == null) return false;
			slot = ItemResolver.FindInventorySlot(Player, itemName);
			return slot >= 0;
		}

		public bool PlayerHolding(string itemName)
		{
			if (Player == null) return false;
			if (!ItemResolver.TryResolve(itemName, out int id)) return false;
			Item held = Player.HeldItem;
			return held != null && held.type == id;
		}

		public int CountItem(string itemName)
		{
			if (Player == null) return 0;
			if (!ItemResolver.TryResolve(itemName, out int id)) return 0;
			int n = 0;
			foreach (Item it in Player.inventory) {
				if (it != null && !it.IsAir && it.type == id) n += it.stack;
			}
			return n;
		}

		private static bool AnyBoss()
		{
			for (int i = 0; i < Main.maxNPCs; i++) {
				if (Main.npc[i].active && Main.npc[i].boss) return true;
			}
			return false;
		}

		private bool AnyHostile(float radius)
		{
			if (Player == null) return false;
			float r2 = radius * radius;
			for (int i = 0; i < Main.maxNPCs; i++) {
				NPC n = Main.npc[i];
				if (!n.active || n.friendly || n.lifeMax <= 5) continue;
				if (Vector2.DistanceSquared(n.Center, Player.Center) <= r2) return true;
			}
			return false;
		}
	}
}
