using System;
using System.Collections.Generic;
using System.IO;
using Terraria;

namespace MacroMod.Common.Macros
{
	/// <summary>
	/// Holds every macro the player has authored, indexed by case-insensitive
	/// name.  Macros are persisted as plain <c>.macro</c> text files inside
	/// <see cref="MacroDirectory"/> so they can be edited with any external
	/// editor (VS Code, Notepad, etc.) and reloaded on demand.
	///
	/// Macros are also reachable via <see cref="ModSettings"/> bindings to a
	/// fixed pool of registered keybinds — see <c>MacroKeybindSystem</c>.
	/// </summary>
	public static class MacroLibrary
	{
		public const string FileExtension = ".macro";

		private static readonly Dictionary<string, Macro> Macros = new(StringComparer.OrdinalIgnoreCase);

		public static IReadOnlyCollection<Macro> All => Macros.Values;

		public static string MacroDirectory =>
			Path.Combine(Main.SavePath ?? AppDomain.CurrentDomain.BaseDirectory, "Macros");

		// ---------------- API ---------------------------------------------

		public static void EnsureDirectory()
		{
			try { Directory.CreateDirectory(MacroDirectory); } catch { /* best-effort */ }
		}

		public static Macro FindMacro(string name)
		{
			if (string.IsNullOrWhiteSpace(name)) return null;
			Macros.TryGetValue(name.Trim(), out var m);
			return m;
		}

		public static Macro CreateMacro(string name, string source = "")
		{
			if (string.IsNullOrWhiteSpace(name)) return null;
			name = SanitizeName(name);
			if (Macros.TryGetValue(name, out var existing)) return existing;
			var m = new Macro(name) { Source = source };
			ParseMacro(m);
			Macros[name] = m;
			Save(m);
			return m;
		}

		public static bool DeleteMacro(string name)
		{
			if (!Macros.TryGetValue(name, out var m)) return false;
			Macros.Remove(name);
			try {
				string path = Path.Combine(MacroDirectory, m.Name + FileExtension);
				if (File.Exists(path)) File.Delete(path);
			}
			catch { /* ignore */ }
			return true;
		}

		public static bool RenameMacro(string oldName, string newName)
		{
			if (!Macros.TryGetValue(oldName, out var m)) return false;
			newName = SanitizeName(newName);
			if (string.IsNullOrEmpty(newName) || Macros.ContainsKey(newName)) return false;
			Macros.Remove(oldName);
			try {
				string oldPath = Path.Combine(MacroDirectory, m.Name + FileExtension);
				string newPath = Path.Combine(MacroDirectory, newName + FileExtension);
				if (File.Exists(oldPath)) File.Move(oldPath, newPath);
			}
			catch { /* ignore */ }
			m.Name = newName;
			Macros[newName] = m;
			return true;
		}

		public static void UpdateSource(Macro macro, string newSource)
		{
			// The visual editor round-trips macro.Source verbatim through
			// VisualLine.ParseAll/SerializeAll, so newSource already contains
			// the existing @triggers header.  Strip whatever header is in
			// newSource before re-prepending the canonical header from the
			// macro model — otherwise every save would duplicate the block.
			var stripMode = TriggerMatchMode.Any;
			var dummy = new List<MacroTrigger>();
			string body = MacroTriggerSerializer.ExtractAndStrip(newSource ?? string.Empty, dummy, ref stripMode);
			string header = MacroTriggerSerializer.SerializeHeader(macro.Triggers ?? new List<MacroTrigger>(), macro.TriggerMode);
			macro.Source = header + body;
			ParseMacro(macro);
			Save(macro);
		}

		public static void Save(Macro macro)
		{
			EnsureDirectory();
			try {
				string path = Path.Combine(MacroDirectory, macro.Name + FileExtension);
				File.WriteAllText(path, macro.Source ?? string.Empty);
				macro.LastModifiedUtc = File.GetLastWriteTimeUtc(path);
			}
			catch (Exception e) {
				Main.NewText("MacroMod: failed to save '" + macro.Name + "': " + e.Message, Microsoft.Xna.Framework.Color.IndianRed);
			}
		}

		public static void ReloadAll()
		{
			Macros.Clear();
			EnsureDirectory();
			if (!Directory.Exists(MacroDirectory)) return;
			foreach (string file in Directory.EnumerateFiles(MacroDirectory, "*" + FileExtension)) {
				try {
					string name = Path.GetFileNameWithoutExtension(file);
					string source = File.ReadAllText(file);
					var m = new Macro(name) { Source = source, LastModifiedUtc = File.GetLastWriteTimeUtc(file) };
					ParseMacro(m);
					Macros[name] = m;
				}
				catch (Exception e) {
					Main.NewText("MacroMod: failed to load '" + Path.GetFileName(file) + "': " + e.Message,
						Microsoft.Xna.Framework.Color.IndianRed);
				}
			}
		}

		public static void ReloadIfChanged()
		{
			EnsureDirectory();
			if (!Directory.Exists(MacroDirectory)) return;
			foreach (string file in Directory.EnumerateFiles(MacroDirectory, "*" + FileExtension)) {
				try {
					string name = Path.GetFileNameWithoutExtension(file);
					DateTime mtime = File.GetLastWriteTimeUtc(file);
					if (Macros.TryGetValue(name, out var existing) && existing.LastModifiedUtc >= mtime) continue;
					string source = File.ReadAllText(file);
					if (existing == null) existing = new Macro(name);
					existing.Source = source;
					existing.LastModifiedUtc = mtime;
					ParseMacro(existing);
					Macros[name] = existing;
				}
				catch { /* ignore */ }
			}
		}

		// ---------------- helpers -----------------------------------------

		private static void ParseMacro(Macro m)
		{
			// Strip the @triggers header before tokenising so the parser
			// only sees executable lines.  Triggers are stored on the macro
			// model and round-tripped on Save().
			var mode = m.TriggerMode;
			string body = MacroTriggerSerializer.ExtractAndStrip(m.Source ?? string.Empty, m.Triggers, ref mode);
			m.TriggerMode = mode;
			m.Program = MacroParser.Parse(body, out string err);
			m.ParseError = err;
		}

		/// <summary>
		/// Replaces the macro's triggers and serialises the new header at
		/// the top of the file so external editors see a round-trippable
		/// document.
		/// </summary>
		public static void UpdateTriggers(Macro macro, IList<MacroTrigger> triggers, TriggerMatchMode mode)
		{
			if (macro == null) return;
			macro.Triggers = new List<MacroTrigger>(triggers ?? new List<MacroTrigger>());
			macro.TriggerMode = mode;

			// Strip any existing header from the source, then re-write.
			var stripMode = TriggerMatchMode.Any;
			var dummy = new List<MacroTrigger>();
			string body = MacroTriggerSerializer.ExtractAndStrip(macro.Source ?? string.Empty, dummy, ref stripMode);
			string header = MacroTriggerSerializer.SerializeHeader(macro.Triggers, macro.TriggerMode);
			macro.Source = header + body;
			Save(macro);
		}

		private static string SanitizeName(string s)
		{
			if (string.IsNullOrWhiteSpace(s)) return string.Empty;
			var sb = new System.Text.StringBuilder();
			foreach (char c in s.Trim()) {
				if (char.IsLetterOrDigit(c) || c == '_' || c == '-' || c == ' ') sb.Append(c);
			}
			return sb.ToString();
		}
	}
}
