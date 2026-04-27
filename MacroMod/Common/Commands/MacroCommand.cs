using System.IO;
using System.Linq;
using MacroMod.Common.Macros;
using MacroMod.Common.Systems;
using Terraria;
using Terraria.ModLoader;

namespace MacroMod.Common.Commands
{
	/// <summary>Chat-side power user entry point: <c>/macro list|run|edit|create|delete|reload|stop|dir</c>.</summary>
	public class MacroCommand : ModCommand
	{
		public override CommandType Type => CommandType.Chat;
		public override string Command => "macro";
		public override string Usage => "/macro <list|run|edit|create|delete|reload|stop|dir|help> [name]";
		public override string Description => "Manage Macro Master macros.";

		public override void Action(CommandCaller caller, string input, string[] args)
		{
			if (args.Length == 0) {
				caller.Reply(Usage);
				return;
			}
			string sub = args[0].ToLowerInvariant();
			string rest = args.Length > 1 ? string.Join(' ', args.Skip(1)) : string.Empty;

			switch (sub) {
				case "help":
					caller.Reply(Usage);
					caller.Reply("/macro list — show known macros");
					caller.Reply("/macro run <name> — execute a macro");
					caller.Reply("/macro create <name> — create a new (empty) macro");
					caller.Reply("/macro edit <name> — open the macro file in your default editor");
					caller.Reply("/macro delete <name> — remove a macro");
					caller.Reply("/macro reload — re-read every .macro file from disk");
					caller.Reply("/macro stop — cancel all running macros");
					caller.Reply("/macro dir — print the macros directory path");
					break;
				case "list":
					if (MacroLibrary.All.Count == 0) caller.Reply("(no macros yet — use /macro create <name>)");
					foreach (var m in MacroLibrary.All) {
						string status = m.HasError ? " [parse error: " + m.ParseError + "]" : string.Empty;
						caller.Reply(m.Name + status);
					}
					break;
				case "run":
					MacroSystem.StartMacro(rest);
					break;
				case "create":
					if (string.IsNullOrWhiteSpace(rest)) { caller.Reply("name required"); return; }
					var created = MacroLibrary.CreateMacro(rest, "# " + rest + "\n# write your macro commands here\n");
					if (created != null) caller.Reply("created '" + created.Name + "' at " + Path.Combine(MacroLibrary.MacroDirectory, created.Name + MacroLibrary.FileExtension));
					break;
				case "delete":
				case "remove":
					if (MacroLibrary.DeleteMacro(rest)) caller.Reply("deleted '" + rest + "'");
					else caller.Reply("no macro named '" + rest + "'");
					break;
				case "reload":
					MacroLibrary.ReloadAll();
					caller.Reply("reloaded " + MacroLibrary.All.Count + " macro(s)");
					break;
				case "stop":
					MacroSystem.StopAll();
					caller.Reply("stopped all running macros");
					break;
				case "dir":
				case "folder":
					caller.Reply(MacroLibrary.MacroDirectory);
					break;
				case "edit":
				case "open":
					DoEdit(caller, rest);
					break;
				default:
					caller.Reply("unknown subcommand: " + sub);
					caller.Reply(Usage);
					break;
			}
		}

		private static void DoEdit(CommandCaller caller, string name)
		{
			MacroLibrary.EnsureDirectory();
			if (string.IsNullOrWhiteSpace(name)) { caller.Reply("name required"); return; }
			var macro = MacroLibrary.FindMacro(name) ?? MacroLibrary.CreateMacro(name);
			if (macro == null) { caller.Reply("could not create macro"); return; }
			string path = Path.Combine(MacroLibrary.MacroDirectory, macro.Name + MacroLibrary.FileExtension);
			caller.Reply("editing: " + path);
			try {
				var psi = new System.Diagnostics.ProcessStartInfo {
					FileName = path,
					UseShellExecute = true,
				};
				System.Diagnostics.Process.Start(psi);
			}
			catch (System.Exception e) {
				caller.Reply("could not open external editor: " + e.Message);
				caller.Reply("edit the file with your preferred text editor and run '/macro reload'");
			}
		}
	}
}
