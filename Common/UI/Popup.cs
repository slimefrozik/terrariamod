using Microsoft.Xna.Framework;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.UI;

namespace MacroMod.Common.UI
{
	/// <summary>
	/// Lightweight modal popup hosted by <see cref="MacroPanel"/>.  Each
	/// popup is a self-contained <see cref="UIElement"/> rendered above the
	/// main panel; the main panel manages a stack and disables interaction
	/// underneath while a popup is open.
	/// </summary>
	public abstract class Popup : UIPanel
	{
		protected Popup(float w, float h)
		{
			Width.Set(w, 0f);
			Height.Set(h, 0f);
			HAlign = 0.5f;
			VAlign = 0.5f;
			SetPadding(8f);
			BackgroundColor = new Color(28, 38, 70);
			BorderColor = new Color(140, 180, 255) * 0.7f;

			var closeBtn = new UITextPanel<string>("X", 0.8f, true);
			closeBtn.Width.Set(28f, 0f);
			closeBtn.Height.Set(28f, 0f);
			closeBtn.HAlign = 1f;
			closeBtn.OnLeftClick += (_, __) => Close();
			Append(closeBtn);
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (ContainsPoint(Main.MouseScreen)) Main.LocalPlayer.mouseInterface = true;
		}

		public void Close()
		{
			MacroUISystem.Instance?.ClosePopup(this);
		}
	}
}
