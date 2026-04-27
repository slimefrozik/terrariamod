using Microsoft.Xna.Framework;
using Terraria;
using Terraria.ModLoader;
using Terraria.UI;

namespace MacroMod.Common.UI
{
	/// <summary>Hosts the Macro Master UI as a tModLoader interface layer.</summary>
	[Autoload(Side = ModSide.Client)]
	public class MacroUISystem : ModSystem
	{
		public static MacroUISystem Instance { get; private set; }

		private UserInterface _ui;
		internal MacroPanel Panel;
		private GameTime _lastUpdate;

		public bool Visible { get; private set; }

		public override void Load()
		{
			Instance = this;
			if (Main.dedServ) return;
			Panel = new MacroPanel();
			Panel.Activate();
			_ui = new UserInterface();
		}

		public override void Unload()
		{
			Instance = null;
			_ui = null;
			Panel = null;
		}

		public void Toggle()
		{
			if (Visible) Hide();
			else Show();
		}

		public void Show()
		{
			if (Panel == null) return;
			Panel.Refresh();
			_ui?.SetState(Panel);
			Visible = true;
		}

		public void Hide()
		{
			_ui?.SetState(null);
			Visible = false;
		}

		public void ClosePopup(UIElement popup)
		{
			Panel?.ClosePopup(popup);
		}

		public override void UpdateUI(GameTime gameTime)
		{
			_lastUpdate = gameTime;
			if (Visible) _ui?.Update(gameTime);
		}

		public override void ModifyInterfaceLayers(System.Collections.Generic.List<GameInterfaceLayer> layers)
		{
			int idx = layers.FindIndex(l => l.Name.Equals("Vanilla: Mouse Text"));
			if (idx < 0) idx = layers.Count - 1;
			layers.Insert(idx, new LegacyGameInterfaceLayer(
				"MacroMod: Macro UI",
				() => {
					if (Visible && _ui?.CurrentState != null) _ui.CurrentState.Draw(Main.spriteBatch);
					return true;
				},
				InterfaceScaleType.UI));
		}
	}
}
