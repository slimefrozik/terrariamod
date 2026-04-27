using System;
using System.Collections.Generic;
using System.Text;

namespace MacroMod.Common.Macros
{
	/// <summary>
	/// Tokenises macro source into <see cref="MacroLine"/>s and resolves block
	/// jump targets (if/else/endif, while/endwhile).  Does no semantic
	/// validation of command names or argument shapes — the executor will
	/// raise its own errors for unknown commands.
	/// </summary>
	public static class MacroParser
	{
		/// <summary>Parse the given source.  On failure, returns a list with a
		/// single Comment line and sets <paramref name="error"/>.</summary>
		public static List<MacroLine> Parse(string source, out string error)
		{
			error = null;
			var program = new List<MacroLine>();
			if (string.IsNullOrEmpty(source)) {
				return program;
			}

			string[] rawLines = source.Replace("\r\n", "\n").Split('\n');
			for (int i = 0; i < rawLines.Length; i++) {
				var line = ParseLine(rawLines[i], i + 1);
				if (line != null) program.Add(line);
			}

			try {
				LinkBlocks(program);
			}
			catch (Exception e) {
				error = e.Message;
			}

			return program;
		}

		// ---- line tokeniser ------------------------------------------------

		private static MacroLine ParseLine(string raw, int lineNumber)
		{
			var line = new MacroLine { LineNumber = lineNumber, RawText = raw };
			string s = raw.Trim();

			if (s.Length == 0) {
				line.Kind = MacroLineKind.Empty;
				return line;
			}
			if (s.StartsWith("#") || s.StartsWith("//")) {
				line.Kind = MacroLineKind.Comment;
				return line;
			}

			// All real macro statements start with '/'. Allow plain text as a
			// shortcut for /say.
			if (!s.StartsWith("/")) {
				line.Kind = MacroLineKind.Command;
				line.Command = "say";
				line.Args = s;
				return line;
			}

			s = s.Substring(1).TrimStart();
			int spaceIdx = IndexOfWhitespace(s);
			string head = spaceIdx < 0 ? s : s.Substring(0, spaceIdx);
			string rest = spaceIdx < 0 ? string.Empty : s.Substring(spaceIdx + 1).TrimStart();

			line.Command = head.ToLowerInvariant();

			line.Kind = line.Command switch {
				"if" => MacroLineKind.If,
				"elseif" => MacroLineKind.ElseIf,
				"else" => MacroLineKind.Else,
				"endif" => MacroLineKind.EndIf,
				"while" => MacroLineKind.While,
				"endwhile" => MacroLineKind.EndWhile,
				"loop" => MacroLineKind.Loop,
				"stop" => MacroLineKind.Stop,
				"set" => MacroLineKind.Assign,
				_ => MacroLineKind.Command,
			};

			// Extract leading [..] condition groups from the argument string.
			rest = ExtractConditions(rest, line.Conditions);
			line.Args = rest;
			return line;
		}

		private static int IndexOfWhitespace(string s)
		{
			for (int i = 0; i < s.Length; i++) {
				if (char.IsWhiteSpace(s[i])) return i;
			}
			return -1;
		}

		private static string ExtractConditions(string rest, List<List<string>> groups)
		{
			while (rest.Length > 0 && rest[0] == '[') {
				int close = rest.IndexOf(']');
				if (close < 0) break;
				string inner = rest.Substring(1, close - 1).Trim();
				var group = new List<string>();
				foreach (string raw in inner.Split(',')) {
					string c = raw.Trim();
					if (c.Length > 0) group.Add(c);
				}
				groups.Add(group);
				rest = rest.Substring(close + 1).TrimStart();
			}
			return rest;
		}

		// ---- block linker --------------------------------------------------

		private static void LinkBlocks(List<MacroLine> program)
		{
			var ifStack = new Stack<List<int>>();   // stack of "if-chain index lists"
			var whileStack = new Stack<int>();

			for (int i = 0; i < program.Count; i++) {
				var l = program[i];
				switch (l.Kind) {
					case MacroLineKind.If:
						ifStack.Push(new List<int> { i });
						break;
					case MacroLineKind.ElseIf:
					case MacroLineKind.Else:
						if (ifStack.Count == 0)
							throw new Exception($"line {l.LineNumber}: /{l.Command} without matching /if");
						ifStack.Peek().Add(i);
						break;
					case MacroLineKind.EndIf:
						if (ifStack.Count == 0)
							throw new Exception($"line {l.LineNumber}: /endif without matching /if");
						var chain = ifStack.Pop();
						chain.Add(i);
						// each chain entry jumps to the next; final EndIf is ChainEnd of all.
						for (int k = 0; k < chain.Count - 1; k++) {
							var head = program[chain[k]];
							head.JumpTarget = chain[k + 1];
							head.ChainEnd = i;
						}
						program[i].JumpTarget = i;
						program[i].ChainEnd = i;
						break;
					case MacroLineKind.While:
						whileStack.Push(i);
						break;
					case MacroLineKind.EndWhile:
						if (whileStack.Count == 0)
							throw new Exception($"line {l.LineNumber}: /endwhile without matching /while");
						int wIdx = whileStack.Pop();
						program[wIdx].JumpTarget = i;
						l.JumpTarget = wIdx;
						break;
				}
			}

			if (ifStack.Count > 0)
				throw new Exception("unterminated /if (missing /endif)");
			if (whileStack.Count > 0)
				throw new Exception("unterminated /while (missing /endwhile)");
		}

		// ---- helper used by executor for /set ------------------------------

		/// <summary>Splits a /set argument like "$foo = 1 + 2" into name/expression.</summary>
		public static bool TrySplitAssignment(string args, out string varName, out string expression)
		{
			varName = null; expression = null;
			if (string.IsNullOrEmpty(args)) return false;
			int eq = args.IndexOf('=');
			if (eq < 0) return false;
			varName = args.Substring(0, eq).Trim();
			expression = args.Substring(eq + 1).Trim();
			if (varName.StartsWith("$")) varName = varName.Substring(1);
			return varName.Length > 0;
		}

		/// <summary>Helper used by various commands: split arguments by a single delimiter, respecting nothing fancy.</summary>
		public static List<string> SplitArgs(string s, char delim = ' ')
		{
			var result = new List<string>();
			if (string.IsNullOrEmpty(s)) return result;
			var sb = new StringBuilder();
			bool inQuotes = false;
			foreach (char c in s) {
				if (c == '"') { inQuotes = !inQuotes; continue; }
				if (c == delim && !inQuotes) {
					if (sb.Length > 0) { result.Add(sb.ToString()); sb.Clear(); }
				}
				else sb.Append(c);
			}
			if (sb.Length > 0) result.Add(sb.ToString());
			return result;
		}
	}
}
