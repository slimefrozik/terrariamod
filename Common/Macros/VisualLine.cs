using System.Collections.Generic;
using System.Text;

namespace MacroMod.Common.Macros
{
	/// <summary>
	/// Editor-side representation of a macro line.  Round-trips with the
	/// raw text format produced by <see cref="MacroParser"/> but in a shape
	/// that's easier to mutate from the UI: keyword + args + condition
	/// prefix as a single string ("[hp&lt;50][mod:shift]") rather than the
	/// nested AST groups.
	/// </summary>
	public class VisualLine
	{
		public string Keyword;        // e.g. "/use" or "#" or "" for raw / blank
		public string Args;           // free-form
		public string Conditions;     // serialized "[..][..]" prefix or empty
		public string RawOverride;    // when non-null, written verbatim instead

		public VisualLine() { Keyword = string.Empty; Args = string.Empty; Conditions = string.Empty; }

		public string Serialize()
		{
			if (RawOverride != null) return RawOverride;
			var sb = new StringBuilder();
			if (!string.IsNullOrEmpty(Conditions)) sb.Append(Conditions).Append(' ');
			if (!string.IsNullOrEmpty(Keyword)) {
				sb.Append(Keyword);
				if (!string.IsNullOrEmpty(Args)) sb.Append(' ').Append(Args);
			}
			else if (!string.IsNullOrEmpty(Args)) {
				sb.Append(Args);
			}
			return sb.ToString();
		}

		// ---- factory: parse a single source line into a VisualLine -------

		public static VisualLine FromSource(string raw)
		{
			var line = new VisualLine();
			if (raw == null) raw = string.Empty;
			string trimmed = raw.TrimStart();
			if (trimmed.Length == 0) {
				line.RawOverride = raw;
				return line;
			}
			if (trimmed.StartsWith("#") || trimmed.StartsWith("//")) {
				line.Keyword = "#";
				line.Args = trimmed.StartsWith("//") ? trimmed.Substring(2).TrimStart() : trimmed.Substring(1).TrimStart();
				return line;
			}

			// Strip leading bracket conditions, allowing whitespace between groups.
			int i = 0;
			var conds = new StringBuilder();
			while (i < trimmed.Length && (trimmed[i] == '[' || char.IsWhiteSpace(trimmed[i]))) {
				if (char.IsWhiteSpace(trimmed[i])) { i++; continue; }
				int end = trimmed.IndexOf(']', i + 1);
				if (end < 0) break;
				conds.Append(trimmed, i, end - i + 1);
				i = end + 1;
			}
			line.Conditions = conds.ToString();
			string rest = trimmed.Substring(i).TrimStart();

			if (rest.StartsWith("/")) {
				int sp = rest.IndexOf(' ');
				if (sp < 0) { line.Keyword = rest; line.Args = string.Empty; }
				else { line.Keyword = rest.Substring(0, sp); line.Args = rest.Substring(sp + 1).TrimStart(); }
			}
			else {
				line.Keyword = string.Empty;
				line.Args = rest;
			}
			return line;
		}

		public static List<VisualLine> ParseAll(string source)
		{
			var list = new List<VisualLine>();
			if (string.IsNullOrEmpty(source)) return list;
			foreach (string raw in source.Replace("\r\n", "\n").Split('\n')) {
				list.Add(FromSource(raw));
			}
			// drop trailing pure-empty raw lines so saving is stable.
			while (list.Count > 0) {
				var last = list[^1];
				if (last.RawOverride != null && string.IsNullOrWhiteSpace(last.RawOverride)) list.RemoveAt(list.Count - 1);
				else break;
			}
			return list;
		}

		public static string SerializeAll(List<VisualLine> lines)
		{
			var sb = new StringBuilder();
			for (int i = 0; i < lines.Count; i++) {
				if (i > 0) sb.Append('\n');
				sb.Append(lines[i].Serialize());
			}
			return sb.ToString();
		}
	}
}
