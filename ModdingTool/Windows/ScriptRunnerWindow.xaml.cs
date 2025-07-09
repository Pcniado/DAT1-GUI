// HOW TO USE THE SCRIPT RUNNER SYSTEM
//
//
// === AVAILABLE HELPERS ===
// - ui.ask_user_for_file(message): Prompts the user with a custom message box, then opens a file picker. Returns the path or None.
// - ui.ask_user_for_folder(message): Prompts the user with a custom message box, then opens a folder picker. Returns the folder path or None.
// - ui.save_text_to_file(message, text): Prompts the user to choose a file and saves the given text.
// - ui.save_bytes_to_file(message, bytes): Prompts the user to choose a file and saves the given bytes.
// - assets.list_assets(): Returns a list of all asset relative paths.
// - assets.list_assets_normalized(): Returns all asset paths, normalized for matching.
// - assets.read_asset_text(path): Reads an asset as UTF-8 text.
// - assets.get_asset_by_path(path): Returns metadata for an asset.
// - assets.extract_selected_assets(output_dir): Extracts all assets to the given directory. WARNING: THIS WILL REALLY EXPORT ALL ASSETS!!!
// - assets.extract_selected_assets_by_paths(paths, output_dir): Extracts only the assets whose paths are in the list.
// - config.load_config(path): Loads a .config file and returns a Python dict (JSON).
// - config.save_config(path, config_obj): Converts a config (JSON) to a .config file.
// - add_to_stage(file_path, relpath, span=0, show_message_box=True): Adds or replaces any file in the stage. file_path = path to file, relpath = relative path in archive, span = span index (default 0 for configs). show_message_box = if False, disables popup notifications.
//
// === SCRIPTING TIPS ===
// - Always normalize paths for matching.
// - Use print/debug helpers to inspect available assets and dependencies.
// - Use Python control flow (if, for, etc.) to automate complex tasks.
// - The 'result' variable is shown in the UI after script execution.
//
// === EXAMPLES ===
// 1. Save a string to a file chosen by the user:
//    ui.save_text_to_file("Choose where to save your text file.", "Hello world!")
//
// 2. Load, edit, and save a config file:
//    cfg = config.load_config("myfile.config")
//    cfg['DATA']['foo'] = 123
//    config.save_config("myfile_out.config", cfg)
//
// 3. Add a config file to the current stage (see below for add_to_stage):
//    add_to_stage("myfile_out.config")
//
// 4. Extract all assets under a folder:
//    root = 'ui/loaded/authored/'
//    to_extract = [p for p in assets.list_assets() if p.startswith(root)]
//    output_dir = ui.ask_user_for_folder("Select a folder to extract assets to.")
//    if output_dir:
//        assets.extract_selected_assets_by_paths(to_extract, output_dir)
//        result = f'Extracted {len(to_extract)} assets to {output_dir}'
//    else:
//        result = "No output folder selected."
//
// === NOTE: add_to_stage(config_path) is available if you want to add a file to the current stage.

using System;
using System.Windows;
using Microsoft.Win32;
using System.IO;
using System.Threading;
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
        private CancellationTokenSource _scriptCancellationTokenSource;

        public ScriptRunnerWindow(IEnumerable<Asset> assets, TOCBase toc)
        {
            InitializeComponent();
            _assets = assets;
            _toc = toc;
            // Set the default script in the editor
            ScriptEditor.Text =
                "# HOW TO USE THE SCRIPT RUNNER SYSTEM\n" +
                "#\n" +
                "# This window lets you run Python-inspired scripts to automate asset extraction, conversion, config editing, and more.\n" +
                "#\n" +
                "# === AVAILABLE HELPERS ===\n" +
                "# - ui.ask_user_for_file(message): Prompts the user with a custom message box, then opens a file picker. Returns the path or None.\n" +
                "# - ui.ask_user_for_folder(message): Prompts the user with a custom message box, then opens a folder picker. Returns the folder path or None.\n" +
                "# - ui.save_text_to_file(message, text): Prompts the user to choose a file and saves the given text.\n" +
                "# - ui.save_bytes_to_file(message, bytes): Prompts the user to choose a file and saves the given bytes.\n" +
                "# - assets.list_assets(): Returns a list of all asset relative paths.\n" +
                "# - assets.list_assets_normalized(): Returns all asset paths, normalized for matching.\n" +
                "# - assets.read_asset_text(path): Reads an asset as UTF-8 text.\n" +
                "# - assets.get_asset_by_path(path): Returns metadata for an asset.\n" +
                "# - assets.extract_selected_assets(output_dir): Extracts all assets to the given directory. WARNING: THIS WILL REALLY EXPORT ALL ASSETS!!!\n" +
                "# - assets.extract_selected_assets_by_paths(paths, output_dir): Extracts only the assets whose paths are in the list.\n" +
                "# - config.load_config(path): Loads a .config file and returns a (JSON).\n" +
                "# - config.save_config(path, config_obj): Converts a config (JSON) to a .config file.\n" +
                "# - add_to_stage(file_path, relpath, span=0, show_message_box=True): Adds or replaces any file in the stage. file_path = path to file, relpath = relative path in archive, span = span index (default 0 for configs). show_message_box = if False, disables popup notifications.\n" +
                "#\n" +
                "# === SCRIPTING TIPS ===\n" +
                "# - Always normalize paths for matching.\n" +
                "# - Use print/debug helpers to inspect available assets and dependencies.\n" +
                "# - Use Python control flow (if, for, etc.) to automate complex tasks.\n" +
                "# - The 'result' variable is shown in the UI after script execution.\n" +
                "#\n" +
                "# === EXAMPLES ===\n" +
                "# 1. Save a string to a file chosen by the user:\n" +
                "#   ui.save_text_to_file(\"Choose where to save your text file.\", \"Hello world!\")\n" +
                "#\n" +
                "# 2. Load, edit, and save a config file:\n" +
                "#   cfg = config.load_config(\"myfile.config\")\n" +
                "#   cfg['DATA']['foo'] = 123\n" +
                "#   config.save_config(\"myfile_out.config\", cfg)\n" +
                "#\n" +
                "# 3. Add or replace any file in the stage (config, texture, etc):\n" +
                "#   add_to_stage(\"myfile.config\", \"ui/configs/myfile.config\")\n" +
                "#   add_to_stage(\"mytex.dds\", \"ui/textures/mytex.dds\", 1)\n" +
                "#   add_to_stage(\"myfile.config\", \"ui/configs/myfile.config\", 0, False)  # disables message box\n" +
                "#\n" +
                "# 4. Extract all assets under a folder:\n" +
                "#   root = 'ui/loaded/authored/'\n" +
                "#   to_extract = [p for p in assets.list_assets() if p.startswith(root)]\n" +
                "#   output_dir = ui.ask_user_for_folder(\"Select a folder to extract assets to.\")\n" +
                "#   if output_dir:\n" +
                "#       assets.extract_selected_assets_by_paths(to_extract, output_dir)\n" +
                "#       result = f'Extracted {len(to_extract)} assets to {output_dir}'\n" +
                "#   else:\n" +
                "#       result = \"No output folder selected.\"\n" +
                "#\n" +
                "# === NOTE: add_to_stage(file_path, relpath, span=0, show_message_box=True) is available for all file types.\n" +
                "#\n" +
                "# === Your script below ===\n";
        }

        public ScriptRunnerWindow() : this(null, null) { }
//fix for minimizing
        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            this.ShowInTaskbar = true;
            this.Owner = null;
        }

        private async void RunButton_Click(object sender, RoutedEventArgs e)
        {
            RunButton.IsEnabled = false;
            ForceStopButton.IsEnabled = true;
            ResultsTextBox.Clear();
            _scriptCancellationTokenSource = new CancellationTokenSource();
            var token = _scriptCancellationTokenSource.Token;
            try
            {
                var script = ScriptEditor.Text;
                var result = await System.Threading.Tasks.Task.Run(() =>
                    _runner.RunScript(script, string.Empty, _assets, _toc)
                );
                ResultsTextBox.Text = result != null ? result.ToString() : "(no result)";
            }
            catch (OperationCanceledException)
            {
                ResultsTextBox.Text = "Script execution cancelled.";
            }
            catch (Exception ex)
            {
                ResultsTextBox.Text = $"Error: {ex.Message}";
            }
            finally
            {
                RunButton.IsEnabled = true;
                ForceStopButton.IsEnabled = false;
            }
        }

        private void ForceStopButton_Click(object sender, RoutedEventArgs e)
        {
            _runner.CancelScript();
            _scriptCancellationTokenSource?.Cancel();
            ForceStopButton.IsEnabled = false;
        }

        private void SaveScriptButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new SaveFileDialog
            {
                Filter = "Python Script (*.py)|*.py|All Files (*.*)|*.*",
                DefaultExt = ".py"
            };
            if (dialog.ShowDialog() == true)
            {
                File.WriteAllText(dialog.FileName, ScriptEditor.Text);
            }
        }

        private void LoadScriptButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Filter = "Python Script (*.py)|*.py|All Files (*.*)|*.*",
                DefaultExt = ".py"
            };
            if (dialog.ShowDialog() == true)
            {
                ScriptEditor.Text = File.ReadAllText(dialog.FileName);
            }
        }
    }
} 