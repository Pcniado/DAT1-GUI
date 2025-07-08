// HOW TO USE THE SCRIPT RUNNER SYSTEM
//
// This window lets you run Python-inspired scripts to automate asset extraction, conversion, and more.
//
// === AVAILABLE HELPERS ===
// - ui.ask_user_for_file(message): Prompts the user with a custom message box, then opens a file picker. Returns the path or None.
// - ui.ask_user_for_folder(message): Prompts the user with a custom message box, then opens a folder picker. Returns the folder path or None.
// - assets.list_assets(): Returns a list of all asset relative paths.
// - assets.list_assets_normalized(): Returns all asset paths, normalized for matching.
// - assets.read_asset_text(path): Reads an asset as UTF-8 text.
// - assets.get_asset_by_path(path): Returns metadata for an asset.
// - assets.extract_selected_assets(output_dir): Extracts all assets to the given directory.
// - assets.extract_selected_assets_by_paths(paths, output_dir): Extracts only the assets whose paths are in the list.
//
// === SCRIPTING TIPS ===
// - Always normalize paths for matching.
// - Use print/debug helpers to inspect available assets and dependencies.
// - Use Python control flow (if, for, etc.) to automate complex tasks.
// - The 'result' variable is shown in the UI after script execution.
//
// === EXAMPLE: Extract all assets under a folder ===
// import clr
// clr.AddReference("System")
// from System.IO import Path
// root = "ui/loaded/authored/"
// to_extract = [p for p in assets.list_assets() if p.startswith(root)]
// output_dir = ui.ask_user_for_folder("Select a folder to extract assets to.")
// if output_dir:
//     assets.extract_selected_assets_by_paths(to_extract, output_dir)
//     result = "Extracted %d assets to %s" % (len(to_extract), output_dir)
// else:
//     result = "No output folder selected."

using System;
using System.Windows;
using Microsoft.Win32;
using System.IO;
using ModdingTool.Utils;
using DAT1;
using System.Collections.Generic;
using ModdingTool.Structs;

namespace ModdingTool.Windows
{
    public partial class ScriptRunnerWindow : MahApps.Metro.Controls.MetroWindow
    {
        private SafeScriptRunner _runner = new SafeScriptRunner();
        private IEnumerable<Asset> _assets = null;
        private TOCBase _toc = null;
        private string _fileContent = string.Empty;
        private string _filePath = string.Empty;

        public ScriptRunnerWindow(IEnumerable<Asset> assets, TOCBase toc)
        {
            InitializeComponent();
            _assets = assets;
            _toc = toc;
            // Set the default script in the editor
            ScriptEditor.Text =
                "# HOW TO USE THE SCRIPT RUNNER SYSTEM\n" +
                "#\n" +
                "# This window lets you run Python-inspired scripts to automate asset extraction, conversion, and more.\n" +
                "#\n" +
                "# === AVAILABLE HELPERS ===\n" +
                "# - ui.ask_user_for_file(message): Prompts the user with a custom message box, then opens a file picker. Returns the path or None.\n" +
                "# - ui.ask_user_for_folder(message): Prompts the user with a custom message box, then opens a folder picker. Returns the folder path or None.\n" +
                "# - assets.list_assets(): Returns a list of all asset relative paths.\n" +
                "# - assets.list_assets_normalized(): Returns all asset paths, normalized for matching.\n" +
                "# - assets.read_asset_text(path): Reads an asset as UTF-8 text.\n" +
                "# - assets.get_asset_by_path(path): Returns metadata for an asset.\n" +
                "# - assets.extract_selected_assets(output_dir): Extracts all assets to the given directory. WARNING: THIS WILL REALLY EXPORT ALL ASSETS!!!\n" +
                "# - assets.extract_selected_assets_by_paths(paths, output_dir): Extracts only the assets whose paths are in the list.\n" +
                "# === SCRIPTING TIPS ===\n" +
                "# - Always normalize paths for matching.\n" +
                "# - Use print/debug helpers to inspect available assets and dependencies.\n" +
                "# - Use Python control flow (if, for, etc.) to automate complex tasks.\n" +
                "# - The 'result' variable is shown in the UI after script execution.\n" +
                "#\n" +
                "# === EXAMPLE: Extract all assets under a folder ===\n" +
                "import clr\nclr.AddReference(\"System\")\nfrom System.IO import Path\nroot = \"ui/loaded/authored/\"\nto_extract = [p for p in assets.list_assets() if p.startswith(root)]\noutput_dir = ui.ask_user_for_folder(\"Select a folder to extract assets to.\")\nif output_dir:\n    assets.extract_selected_assets_by_paths(to_extract, output_dir)\n    result = \"Extracted %d assets to %s\" % (len(to_extract), output_dir)\nelse:\n    result = \"No output folder selected.\"\n";
        }

        public ScriptRunnerWindow() : this(null, null) { }

        private void RunButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var script = ScriptEditor.Text;
                var result = _runner.RunScript(script, string.Empty, _assets, _toc);
                ResultsTextBox.Text = result != null ? result.ToString() : "(no result)";
            }
            catch (Exception ex)
            {
                ResultsTextBox.Text = $"Error: {ex.Message}";
            }
        }
    }
} 