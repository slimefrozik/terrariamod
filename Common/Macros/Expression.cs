using System;
using System.Collections.Generic;

namespace MacroMod.Common.Macros
{
	/// <summary>
	/// Tiny recursive-descent expression evaluator used both for /set and for
	/// boolean condition expressions in /if and /while.
	///
	/// Grammar (loosely):
	///   expr     := or
	///   or       := and ( ('||' | 'or')  and )*
	///   and      := not ( ('&amp;&amp;' | 'and') not )*
	///   not      := ('!' | 'not') not | comp
	///   comp     := add ( ('==' | '!=' | '&lt;' | '&lt;=' | '&gt;' | '&gt;=') add )*
	///   add      := mul ( ('+' | '-') mul )*
	///   mul      := unary ( ('*' | '/' | '%') unary )*
	///   unary    := '-' unary | atom
	///   atom     := number | string | '$'IDENT | IDENT '(' args? ')' | IDENT | '(' expr ')'
	///
	/// Identifiers without parentheses are looked up in the variable scope.
	/// Identifiers followed by '(' are evaluated through the supplied
	/// <see cref="MacroContext"/> as functions (these are the WoW-style
	/// pseudo-conditions like hp(), buff("Regen"), item("Wood")).
	/// </summary>
	public static class Expression
	{
		// ---------- public entry points ------------------------------------

		public static double EvalNumber(string source, MacroContext ctx)
		{
			object v = Eval(source, ctx);
			return ToNumber(v);
		}

		public static bool EvalBool(string source, MacroContext ctx)
		{
			if (string.IsNullOrWhiteSpace(source)) return true;
			object v = Eval(source, ctx);
			return ToBool(v);
		}

		public static object Eval(string source, MacroContext ctx)
		{
			var tokens = Tokenize(source ?? string.Empty);
			var p = new Parser(tokens, ctx);
			object value = p.ParseExpr();
			if (!p.AtEnd) throw new ArgumentException($"unexpected token '{p.Peek().Text}'");
			return value;
		}

		// ---------- helpers ------------------------------------------------

		public static double ToNumber(object v)
		{
			if (v == null) return 0;
			if (v is double d) return d;
			if (v is bool b) return b ? 1 : 0;
			if (v is string s) {
				if (double.TryParse(s, System.Globalization.NumberStyles.Float,
						System.Globalization.CultureInfo.InvariantCulture, out double r)) return r;
				return s.Length;
			}
			return 0;
		}

		public static bool ToBool(object v)
		{
			if (v == null) return false;
			if (v is bool b) return b;
			if (v is double d) return d != 0;
			if (v is string s) return !string.IsNullOrEmpty(s) && s != "0" && s.ToLowerInvariant() != "false";
			return true;
		}

		public static string ToText(object v) => v?.ToString() ?? string.Empty;

		// ---------- tokeniser ----------------------------------------------

		internal enum Tok { End, Number, String, Ident, Var, LParen, RParen, Comma, Op }

		internal struct Token { public Tok Kind; public string Text; public double Number; }

		internal static List<Token> Tokenize(string s)
		{
			var list = new List<Token>();
			int i = 0;
			while (i < s.Length) {
				char c = s[i];
				if (char.IsWhiteSpace(c)) { i++; continue; }

				if (char.IsDigit(c) || (c == '.' && i + 1 < s.Length && char.IsDigit(s[i + 1]))) {
					int start = i;
					while (i < s.Length && (char.IsDigit(s[i]) || s[i] == '.')) i++;
					string num = s.Substring(start, i - start);
					list.Add(new Token { Kind = Tok.Number, Text = num,
						Number = double.Parse(num, System.Globalization.CultureInfo.InvariantCulture) });
					continue;
				}

				if (c == '"') {
					i++;
					int start = i;
					while (i < s.Length && s[i] != '"') i++;
					string str = s.Substring(start, i - start);
					if (i < s.Length) i++;
					list.Add(new Token { Kind = Tok.String, Text = str });
					continue;
				}

				if (c == '$') {
					int start = ++i;
					while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) i++;
					list.Add(new Token { Kind = Tok.Var, Text = s.Substring(start, i - start) });
					continue;
				}

				if (char.IsLetter(c) || c == '_') {
					int start = i;
					while (i < s.Length && (char.IsLetterOrDigit(s[i]) || s[i] == '_')) i++;
					list.Add(new Token { Kind = Tok.Ident, Text = s.Substring(start, i - start) });
					continue;
				}

				if (c == '(') { list.Add(new Token { Kind = Tok.LParen, Text = "(" }); i++; continue; }
				if (c == ')') { list.Add(new Token { Kind = Tok.RParen, Text = ")" }); i++; continue; }
				if (c == ',') { list.Add(new Token { Kind = Tok.Comma, Text = "," }); i++; continue; }

				// multi-char operators
				if (i + 1 < s.Length) {
					string two = s.Substring(i, 2);
					if (two == "==" || two == "!=" || two == "<=" || two == ">=" || two == "&&" || two == "||") {
						list.Add(new Token { Kind = Tok.Op, Text = two });
						i += 2;
						continue;
					}
				}
				if ("+-*/%<>!".IndexOf(c) >= 0) {
					list.Add(new Token { Kind = Tok.Op, Text = c.ToString() });
					i++;
					continue;
				}

				throw new ArgumentException($"unexpected character '{c}' in expression");
			}
			list.Add(new Token { Kind = Tok.End, Text = string.Empty });
			return list;
		}

		// ---------- parser/evaluator ---------------------------------------

		internal class Parser
		{
			private readonly List<Token> _toks;
			private readonly MacroContext _ctx;
			private int _i;

			public Parser(List<Token> toks, MacroContext ctx) { _toks = toks; _ctx = ctx; }

			public bool AtEnd => _toks[_i].Kind == Tok.End;
			public Token Peek() => _toks[_i];
			private Token Next() => _toks[_i++];

			private bool Match(Tok kind, string text = null)
			{
				if (_toks[_i].Kind != kind) return false;
				if (text != null && _toks[_i].Text != text) return false;
				_i++;
				return true;
			}

			public object ParseExpr() => ParseOr();

			private object ParseOr()
			{
				object left = ParseAnd();
				while (true) {
					if (Match(Tok.Op, "||") || (Peek().Kind == Tok.Ident && Peek().Text.Equals("or", StringComparison.OrdinalIgnoreCase))) {
						if (_toks[_i - 1].Kind != Tok.Op) _i++;
						object right = ParseAnd();
						left = ToBool(left) || ToBool(right);
						continue;
					}
					break;
				}
				return left;
			}

			private object ParseAnd()
			{
				object left = ParseNot();
				while (true) {
					if (Match(Tok.Op, "&&") || (Peek().Kind == Tok.Ident && Peek().Text.Equals("and", StringComparison.OrdinalIgnoreCase))) {
						if (_toks[_i - 1].Kind != Tok.Op) _i++;
						object right = ParseNot();
						left = ToBool(left) && ToBool(right);
						continue;
					}
					break;
				}
				return left;
			}

			private object ParseNot()
			{
				if (Match(Tok.Op, "!") || (Peek().Kind == Tok.Ident && Peek().Text.Equals("not", StringComparison.OrdinalIgnoreCase))) {
					if (_toks[_i - 1].Kind != Tok.Op) _i++;
					return !ToBool(ParseNot());
				}
				return ParseComparison();
			}

			private object ParseComparison()
			{
				object left = ParseAdd();
				if (Peek().Kind == Tok.Op && (Peek().Text == "==" || Peek().Text == "!=" ||
						Peek().Text == "<" || Peek().Text == "<=" || Peek().Text == ">" || Peek().Text == ">=")) {
					string op = Next().Text;
					object right = ParseAdd();
					return Compare(left, right, op);
				}
				return left;
			}

			private static bool Compare(object l, object r, string op)
			{
				if (l is string sl && r is string sr) {
					int cmp = string.CompareOrdinal(sl, sr);
					return op switch {
						"==" => cmp == 0,
						"!=" => cmp != 0,
						"<" => cmp < 0,
						"<=" => cmp <= 0,
						">" => cmp > 0,
						">=" => cmp >= 0,
						_ => false,
					};
				}
				double dl = ToNumber(l);
				double dr = ToNumber(r);
				return op switch {
					"==" => Math.Abs(dl - dr) < 1e-9,
					"!=" => Math.Abs(dl - dr) >= 1e-9,
					"<" => dl < dr,
					"<=" => dl <= dr,
					">" => dl > dr,
					">=" => dl >= dr,
					_ => false,
				};
			}

			private object ParseAdd()
			{
				object left = ParseMul();
				while (Peek().Kind == Tok.Op && (Peek().Text == "+" || Peek().Text == "-")) {
					string op = Next().Text;
					object right = ParseMul();
					if (op == "+" && (left is string || right is string))
						left = ToText(left) + ToText(right);
					else
						left = op == "+" ? ToNumber(left) + ToNumber(right) : ToNumber(left) - ToNumber(right);
				}
				return left;
			}

			private object ParseMul()
			{
				object left = ParseUnary();
				while (Peek().Kind == Tok.Op && (Peek().Text == "*" || Peek().Text == "/" || Peek().Text == "%")) {
					string op = Next().Text;
					double right = ToNumber(ParseUnary());
					double l = ToNumber(left);
					left = op switch {
						"*" => l * right,
						"/" => right == 0 ? 0 : l / right,
						"%" => right == 0 ? 0 : l % right,
						_ => 0d,
					};
				}
				return left;
			}

			private object ParseUnary()
			{
				if (Match(Tok.Op, "-")) return -ToNumber(ParseUnary());
				if (Match(Tok.Op, "+")) return ToNumber(ParseUnary());
				return ParseAtom();
			}

			private object ParseAtom()
			{
				Token t = Next();
				switch (t.Kind) {
					case Tok.Number: return t.Number;
					case Tok.String: return t.Text;
					case Tok.Var: return _ctx.GetVariable(t.Text);
					case Tok.LParen: {
						object val = ParseExpr();
						if (!Match(Tok.RParen)) throw new ArgumentException("expected ')'");
						return val;
					}
					case Tok.Ident: {
						// boolean literals
						if (t.Text.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
						if (t.Text.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;

						// function call?
						if (Match(Tok.LParen)) {
							var args = new List<object>();
							if (Peek().Kind != Tok.RParen) {
								args.Add(ParseExpr());
								while (Match(Tok.Comma)) args.Add(ParseExpr());
							}
							if (!Match(Tok.RParen)) throw new ArgumentException("expected ')'");
							return _ctx.CallFunction(t.Text, args);
						}
						return _ctx.GetVariable(t.Text);
					}
					default:
						throw new ArgumentException($"unexpected token '{t.Text}' in expression");
				}
			}
		}
	}
}
