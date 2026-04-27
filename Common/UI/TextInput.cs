using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using ReLogic.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.GameInput;
using Terraria.UI;

namespace MacroMod.Common.UI
{
	/// <summary>
	/// Standalone single-line text field.  We can't use
	/// <c>Terraria.ModLoader.UI.UIFocusInputTextField</c> because it is
	/// declared <c>internal</c> in tModLoader, so we re-implement just
	/// enough of it for the macro editor: hint text, focus toggle, blink,
	/// <see cref="OnTextChange"/> event, escape to unfocus.
	/// </summary>
	public class TextInput : UIElement
	{
		private string _hint;
		private string _text = string.Empty;
		private int _blink;
		private float _scale = 0.85f;
		private Color _textColor = Color.White;

		public bool Focused;
		public Action<string> OnTextChange;
		public Action OnSubmit;

		public string Text {
			get => _text;
			set {
				value ??= string.Empty;
				if (value == _text) return;
				_text = value;
				OnTextChange?.Invoke(_text);
			}
		}

		public TextInput(string hint = "")
		{
			_hint = hint ?? string.Empty;
			SetPadding(4f);
		}

		public TextInput SetScale(float s) { _scale = s; return this; }
		public TextInput SetColor(Color c) { _textColor = c; return this; }
		public TextInput SetHint(string h) { _hint = h ?? string.Empty; return this; }

		public override void LeftClick(UIMouseEvent evt)
		{
			Main.clrInput();
			Focused = true;
		}

		public override void Update(GameTime gameTime)
		{
			base.Update(gameTime);
			if (Focused) Main.LocalPlayer.mouseInterface = true;
			if (Focused && Main.mouseLeft && !ContainsPoint(Main.MouseScreen)) {
				Focused = false;
			}
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			if (Focused) {
				PlayerInput.WritingText = true;
				Main.instance.HandleIME();
				string nv = Main.GetInputText(_text);
				if (Main.inputTextEscape) {
					Main.inputTextEscape = false;
					Focused = false;
				}
				if (Main.inputTextEnter) {
					Main.inputTextEnter = false;
					Focused = false;
					OnSubmit?.Invoke();
				}
				if (nv != _text) {
					_text = nv;
					OnTextChange?.Invoke(_text);
				}
				if (++_blink >= 40) _blink = 0;
			}

			DynamicSpriteFont font = FontAssets.MouseText.Value;
			CalculatedStyle dim = GetInnerDimensions();
			Vector2 pos = new Vector2(dim.X, dim.Y);
			string display = _text;
			if (Focused && _blink < 20) display += "|";
			if (string.IsNullOrEmpty(_text) && !Focused) {
				spriteBatch.DrawString(font, _hint, pos, Color.Gray * 0.7f, 0f, Vector2.Zero, _scale, SpriteEffects.None, 0f);
			}
			else {
				spriteBatch.DrawString(font, display, pos, _textColor, 0f, Vector2.Zero, _scale, SpriteEffects.None, 0f);
			}
		}
	}
}
