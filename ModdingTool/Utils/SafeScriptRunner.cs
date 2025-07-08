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
                    "Extract All Assets - Warning", false);
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
    }

    public class SafeScriptRunner
    {
        private ScriptEngine _engine;
        private ScriptScope _scope;

        public SafeScriptRunner()
        {
            _engine = Python.CreateEngine();
            _scope = _engine.CreateScope();
        }

        public object RunScript(string script, string file_content, System.Collections.Generic.IEnumerable<Structs.Asset> selectedAssets = null, DAT1.TOCBase toc = null)
        {
            _scope.SetVariable("file_content", file_content);
            _scope.SetVariable("HtmlDocument", typeof(HtmlAgilityPack.HtmlDocument));
            _scope.SetVariable("fileinfo", new FileInfoHelper());
            _scope.SetVariable("ui", new ScriptUIHelper());
            if (selectedAssets != null && toc != null)
                _scope.SetVariable("assets", new ScriptAssetHelper(selectedAssets, toc));
            _engine.Execute(script, _scope);
            return _scope.ContainsVariable("result") ? _scope.GetVariable("result") : null;
        }
    }
} 