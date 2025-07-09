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
// - add_to_stage(config_path): Adds a file file to the current stage.
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

using IronPython.Hosting;
using Microsoft.Scripting.Hosting;
using HtmlAgilityPack;
using System;
using System.Collections.Generic;
using System.IO;
using System.Windows;
using Microsoft.Win32;
using System.Linq;
using System.Windows.Forms;
using System.Threading;
using Newtonsoft.Json.Linq;
using OverstrikeShared.STG.Files;
using ModdingTool.Structs;
using System.Reflection;
using DAT1;
// niado: this could go very wrong if used incorrectly :3
namespace ModdingTool.Utils
{
    public class FileInfoHelper
    {
        public bool Exists(string path) => File.Exists(path);
        public long Size(string path) => File.Exists(path) ? new FileInfo(path).Length : -1;
        public string Name(string path) => Path.GetFileName(path);
        public string Directory(string path) => Path.GetDirectoryName(path);
    }

    public class ScriptAssetHelper
    {
        private IEnumerable<Structs.Asset> _assets;
        private DAT1.TOCBase _toc;
        public ScriptAssetHelper(IEnumerable<Structs.Asset> assets, DAT1.TOCBase toc)
        {
            _assets = assets;
            _toc = toc;
        }
        public void extract_selected_assets(string outputDir)
        {
            bool proceed = false;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var msgBox = new Windows.CustomMessageBox(
                    "WARNING: This script will try to export ALL assets to the selected folder. This may take a long time and use a lot of disk space. Continue?",
                    "Extract All Assets - Warning", true);
                msgBox.Owner = System.Windows.Application.Current.MainWindow;
                var res = msgBox.ShowDialog();
                proceed = (res == true);
            });
            if (!proceed) return;
            Utils.ProjectHelper.ExtractAssetsWithStructure(_assets, _toc, outputDir);
        }
        
        public void extract_selected_assets_by_paths(IList<object> paths, string outputDir)
        {
            var pathSet = new HashSet<string>(paths.Select(p => NormalizePath(p.ToString())));
            var matchingAssets = _assets.Where(a =>
                !string.IsNullOrEmpty(a.FullPath) && pathSet.Contains(NormalizePath(a.FullPath))
            ).ToList();

#if DEBUG
            Console.WriteLine("Requested paths:");
            foreach (var p in pathSet) Console.WriteLine(p);
            Console.WriteLine("Matching assets:");
            foreach (var a in matchingAssets) Console.WriteLine(a.FullPath);
#endif

            Utils.ProjectHelper.ExtractAssetsWithStructure(matchingAssets, _toc, outputDir);
        }
       
        public object get_asset_by_path(string relPath)
        {
            var asset = _assets.FirstOrDefault(a => a.FullPath != null && a.FullPath.Replace("\\", "/") == relPath.Replace("\\", "/"));
            if (asset == null) return null;
            return new {
                asset.Name,
                asset.FullPath,
                asset.Size,
                asset.Span,
                asset.Id,
                asset.Archive
            };
        }
       
        public List<string> list_assets()
        {
            return _assets.Where(a => !string.IsNullOrEmpty(a.FullPath)).Select(a => a.FullPath).ToList();
        }
     
        public string read_asset_text(string relPath)
        {
            var asset = _assets.FirstOrDefault(a => a.FullPath != null && a.FullPath.Replace("\\", "/") == relPath.Replace("\\", "/"));
            if (asset == null) return null;
            var bytes = _toc.GetAssetBytes(asset.Span, asset.Id);
            return System.Text.Encoding.UTF8.GetString(bytes);
        }

        public string debug_list_assets()
        {
            return string.Join("\n", _assets.Where(a => !string.IsNullOrEmpty(a.FullPath)).Select(a => a.FullPath));
        }

        public List<string> list_assets_normalized()
        {
            return _assets
                .Where(a => !string.IsNullOrEmpty(a.FullPath))
                .Select(a => NormalizePath(a.FullPath))
                .ToList();
        }

        private static string NormalizePath(string path)
        {
            if (string.IsNullOrEmpty(path)) return "";
            path = path.Replace("\\", "/").TrimStart('.', '/', '\\');
            return path.ToLowerInvariant();
        }
    }

    public class ScriptUIHelper
    {
        public string? ask_user_for_file(string message)
        {
            string? result = null;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var msgBox = new Windows.CustomMessageBox(message, "Select File", false);
                msgBox.Owner = System.Windows.Application.Current.MainWindow;
                var res = msgBox.ShowDialog();
                if (res == true)
                {
                    var dlg = new Microsoft.Win32.OpenFileDialog();
                    dlg.Filter = "All files (*.*)|*.*";
                    if (dlg.ShowDialog() == true)
                    {
                        result = dlg.FileName;
                    }
                }
            });
            return result;
        }
        public string? ask_user_for_folder(string message)
        {
            string? result = null;
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var msgBox = new Windows.CustomMessageBox(message, "Select Folder", false);
                msgBox.Owner = System.Windows.Application.Current.MainWindow;
                var res = msgBox.ShowDialog();
                if (res == true)
                {
                    using (var dlg = new System.Windows.Forms.FolderBrowserDialog())
                    {
                        dlg.Description = message;
                        dlg.ShowNewFolderButton = true;
                        if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
                        {
                            result = dlg.SelectedPath;
                        }
                    }
                }
            });
            return result;
        }
        public void save_text_to_file(string message, string text)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var msgBox = new Windows.CustomMessageBox(message, "Save File", false);
                msgBox.Owner = System.Windows.Application.Current.MainWindow;
                var res = msgBox.ShowDialog();
                if (res == true)
                {
                    var dlg = new Microsoft.Win32.SaveFileDialog();
                    dlg.Filter = "All files (*.*)|*.*";
                    if (dlg.ShowDialog() == true)
                    {
                        File.WriteAllText(dlg.FileName, text);
                    }
                }
            });
        }
        public void save_bytes_to_file(string message, byte[] bytes)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                var msgBox = new Windows.CustomMessageBox(message, "Save File", false);
                msgBox.Owner = System.Windows.Application.Current.MainWindow;
                var res = msgBox.ShowDialog();
                if (res == true)
                {
                    var dlg = new Microsoft.Win32.SaveFileDialog();
                    dlg.Filter = "All files (*.*)|*.*";
                    if (dlg.ShowDialog() == true)
                    {
                        File.WriteAllBytes(dlg.FileName, bytes);
                    }
                }
            });
        }
    }

    public class ScriptConfigHelper
    {
        public JObject load_config(string path)
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read);
            using var br = new BinaryReader(fs);
            byte[] sig = br.ReadBytes(4);
            fs.Seek(0, SeekOrigin.Begin);

            if (sig[0] == (byte)'S' && sig[1] == (byte)'T' && sig[2] == (byte)'G')
            {
                //STG HEADER FORMAT
                var cfg = new OverstrikeShared.STG.Files.Config();
                cfg.Load(path);
                return cfg.ContentSection.Data;
            }
            else if (sig[0] == (byte)'D' && sig[1] == (byte)'A' && sig[2] == (byte)'T' && sig[3] == (byte)'1')
            {
                //no stg
                var cfg = new DAT1.Files.Config_I30(br);
                return cfg.ContentSection.Data;
            }
            throw new InvalidDataException("Unknown config file signature.");
        }
        public void save_config(string path, JObject configObj)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext == ".config")
            {
                var type = configObj["TYPE"]?["Type"]?.ToString() ?? "Unknown";
                var hasRefs = configObj["REFS"] != null;
                var cfg = OverstrikeShared.STG.Files.Config.Make(type, hasRefs);
                // Only use ContentSection.Data from OverstrikeShared.STG.Files.Config
                cfg.ContentSection.Data = (JObject)configObj["DATA"];
                if (hasRefs)
                {
                    foreach (var refPath in configObj["REFS"])
                    {
                        cfg.AddReference((string)refPath);
                    }
                }
                File.WriteAllBytes(path, cfg.Save());
            }
            else
            {
                throw new InvalidDataException("Unknown config file extension: " + ext);
            }
        }
    }

    public class ScriptStageHelper
    {
        public void add_to_stage(string filePath, string relpath, int span = 0, bool showMessageBox = true)
        {
            System.Windows.Application.Current.Dispatcher.Invoke(() =>
            {
                try
                {
                    var mainWindow = System.Windows.Application.Current.Windows.OfType<ModdingTool.MainWindow>().FirstOrDefault();
                    if (mainWindow == null)
                    {
                        if (showMessageBox)
                        {
                            var msgBox = new Windows.CustomMessageBox("Main window not found. Cannot add to stage.", "Add to Stage", false);
                            msgBox.Owner = System.Windows.Application.Current.MainWindow;
                            msgBox.ShowDialog();
                        }
                        return;
                    }
                    var fileName = System.IO.Path.GetFileName(filePath);
                    var asset = new Asset
                    {
                        Name = fileName,
                        FullPath = relpath,
                        Span = (byte)span,
                        Id = CRC64.Hash(relpath)
                    };
                    var field = mainWindow.GetType().GetField("_replacedAssets", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    var dictObj = field?.GetValue(mainWindow);
                    var replacedAssetsDict = dictObj as Dictionary<Asset, string>;
                    if (replacedAssetsDict == null)
                    {
                        if (showMessageBox)
                        {
                            var msgBox = new Windows.CustomMessageBox("Could not access replaced assets dictionary.", "Add to Stage", false);
                            msgBox.Owner = System.Windows.Application.Current.MainWindow;
                            msgBox.ShowDialog();
                        }
                        return;
                    }
                    replacedAssetsDict[asset] = filePath;
                    mainWindow.GetType().GetMethod("SetProjectDirty", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                        ?.Invoke(mainWindow, new object[] { true });
                    if (showMessageBox)
                    {
                        var msgBox2 = new Windows.CustomMessageBox($"Added {fileName} to stage as {relpath} (span {span}).", "Add to Stage", false);
                        msgBox2.Owner = System.Windows.Application.Current.MainWindow;
                        msgBox2.ShowDialog();
                    }
                }
                catch (Exception ex)
                {
                    if (showMessageBox)
                    {
                        var msgBox = new Windows.CustomMessageBox($"Failed to add to stage: {ex.Message}", "Add to Stage", false);
                        msgBox.Owner = System.Windows.Application.Current.MainWindow;
                        msgBox.ShowDialog();
                    }
                }
            });
        }
    }

    public class SafeScriptRunner
    {
        private ScriptEngine _engine;
        private ScriptScope _scope;
        private Thread _scriptThread;
        private volatile bool _isRunning;

        public SafeScriptRunner()
        {
            _engine = Python.CreateEngine();
            _scope = _engine.CreateScope();
        }

        public object RunScript(string script, string file_content, System.Collections.Generic.IEnumerable<Structs.Asset> selectedAssets = null, DAT1.TOCBase toc = null)
        {
            object result = null;
            Exception threadEx = null;
            _isRunning = true;
            _scriptThread = new Thread(() =>
            {
                try
                {
                    _scope.SetVariable("file_content", file_content);
                    _scope.SetVariable("HtmlDocument", typeof(HtmlAgilityPack.HtmlDocument));
                    _scope.SetVariable("fileinfo", new FileInfoHelper());
                    _scope.SetVariable("ui", new ScriptUIHelper());
                    _scope.SetVariable("config", new ScriptConfigHelper());
                    _scope.SetVariable("add_to_stage", new Action<string, string, int, bool>(new ScriptStageHelper().add_to_stage));
                    if (selectedAssets != null && toc != null)
                        _scope.SetVariable("assets", new ScriptAssetHelper(selectedAssets, toc));
                    _engine.Execute(script, _scope);
                    if (_scope.ContainsVariable("result"))
                        result = _scope.GetVariable("result");
                }
                catch (ThreadAbortException)
                {
                    threadEx = new OperationCanceledException("Script execution aborted.");
                }
                catch (Exception ex)
                {
                    // extract Python error details
                    string errorMsg = ex.Message;
                    string errorType = ex.GetType().Name;
                    int? line = null;
                    int? column = null;
                    string codeLine = null;
                    try
                    {
                        var pyEx = ex.GetType().GetProperty("Line")?.GetValue(ex);
                        if (pyEx != null) line = (int)pyEx;
                        var pyCol = ex.GetType().GetProperty("Column")?.GetValue(ex);
                        if (pyCol != null) column = (int)pyCol;
                        var pyLine = ex.GetType().GetProperty("SourceCode")?.GetValue(ex) as string;
                        if (pyLine != null)
                        {
                            var lines = pyLine.Split('\n');
                            if (line.HasValue && line.Value > 0 && line.Value <= lines.Length)
                                codeLine = lines[line.Value - 1];
                        }
                    }
                    catch { }
                    string details;
                    if (errorType.Contains("UnboundNameException") && errorMsg.Contains("assets"))
                    {
                        details = "'assets' is not defined. The 'assets' helper is only available when a TOC or project is loaded. Please load a TOC or project before using 'assets' in your script.";
                    }
                    else
                    {
                        details = $"{errorType}: {errorMsg}";
                    }
                    if (line.HasValue)
                        details += $"\nLine: {line}";
                    if (column.HasValue)
                        details += $", Column: {column}";
                    if (!string.IsNullOrEmpty(codeLine))
                        details += $"\nCode: {codeLine.Trim()}";
                    threadEx = new Exception(details, ex);
                }
                finally
                {
                    _isRunning = false;
                }
            });
            _scriptThread.IsBackground = true;
            _scriptThread.Start();
            _scriptThread.Join();
            if (threadEx != null) throw threadEx;
            return result;
        }

        public void CancelScript()
        {
            if (_isRunning && _scriptThread != null && _scriptThread.IsAlive)
            {
                _scriptThread.Abort();
            }
        }
    }
} 