using ICSharpCode.AvalonEdit;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using OverstrikeShared.STG.Files;
using System;
using System.IO;
using System.Windows;
using System.Threading.Tasks;
using System.Windows.Threading;

namespace ModdingTool.Windows
{
    public partial class ConfigEditorWindow : Window
    {
        private string _currentFilePath;
        private JObject _currentJson;
        private string _originalAssetName;
        private string _originalAssetFullPath;
        private string _selectedAssetName;
        private string _pendingConfigFilePath;
        private bool _pendingLoadStarted;

        public ConfigEditorWindow(string configFilePath = null, string selectedAssetName = null, bool showOpenButton = true, bool showAddToStageButton = false)
        {
            InitializeComponent();
            _selectedAssetName = selectedAssetName;
            this.OpenButton.Visibility = showOpenButton ? Visibility.Visible : Visibility.Collapsed;
            this.AddToModButton.Visibility = showAddToStageButton ? Visibility.Visible : Visibility.Collapsed;
            ShowOverlay(false);
            _pendingConfigFilePath = configFilePath;
            _pendingLoadStarted = false;
            this.Loaded += ConfigEditorWindow_Loaded;
        }

        private void ShowOverlay(bool show)
        {
            this.Overlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void LoadConfigFileAsync(string path)
        {
            Dispatcher.Invoke(() => ShowOverlay(true));
            try
            {
                var result = await Task.Run(() =>
                {
                    var config = new Config();
                    config.Load(path);
                    var json = new JObject
                    {
                        ["TYPE"] = config.TypeSection.Data,
                        ["DATA"] = config.ContentSection.Data
                    };
                    if (config.ReferencesSection != null)
                    {
                        var array = new JArray();
                        foreach (var refEntry in config.ReferencesSection.Values)
                        {
                            array.Add(config.Dat1.GetStringByOffset(refEntry.AssetPathStringOffset));
                        }
                        json["REFS"] = array;
                    }
                    string fullPath = null;
                    if (path.Contains("\\") || path.Contains("/"))
                    {
                        fullPath = path.Replace(Path.GetPathRoot(path), "").TrimStart('\\', '/');
                    }
                    return (json, Path.GetFileName(path), fullPath);
                });
                Dispatcher.Invoke(() => {
                    _currentFilePath = path;
                    _currentJson = result.json;
                    _originalAssetName = result.Item2;
                    _originalAssetFullPath = result.fullPath ?? result.Item2;
                    JsonEditor.Text = result.json.ToString(Newtonsoft.Json.Formatting.Indented);
                    StatusText.Text = $"Loaded: {_selectedAssetName ?? result.Item2}";
                    ShowOverlay(false);
                });
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => {
                    StatusText.Text = $"Error loading config: {ex.Message}";
                    ShowOverlay(false);
                });
            }
        }

        public void SaveConfigFile(string path)
        {
            try
            {
                var json = JObject.Parse(JsonEditor.Text);
                var configType = (string)json["TYPE"]["Type"];
                var hasRefs = json.ContainsKey("REFS");
                var config = Config.Make(configType, hasRefs);
                config.ContentSection.Data = (JObject)json["DATA"];
                if (hasRefs)
                {
                    foreach (var refPath in json["REFS"])
                    {
                        config.AddReference((string)refPath);
                    }
                }
                File.WriteAllBytes(path, config.Save());
                StatusText.Text = $"Saved: {System.IO.Path.GetFileName(path)}";
            }
            catch (Exception ex)
            {
                StatusText.Text = $"Error saving config: {ex.Message}";
            }
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog();
            dlg.Filter = "Config files (*.config)|*.config|All files (*.*)|*.*";
            if (dlg.ShowDialog() == true)
            {
                LoadConfigFileAsync(dlg.FileName);
            }
        }

        private void SaveConfigButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog();
            dlg.Filter = "Config files (*.config)|*.config|All files (*.*)|*.*";
            var defaultName = _selectedAssetName ?? _originalAssetName ?? Path.GetFileName(_currentFilePath) ?? "config.config";
            dlg.FileName = defaultName;
            if (dlg.ShowDialog() == true)
            {
                SaveConfigFile(dlg.FileName);
            }
        }

        private void SaveStageButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog();
            dlg.Filter = "Stage files (*.stage)|*.stage|All files (*.*)|*.*";
            var configAssetName = Path.GetFileName(_currentFilePath);
            dlg.FileName = Path.GetFileNameWithoutExtension(configAssetName) + ".stage";
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    // Save config to a temp file first
                    var tempConfigPath = Path.Combine(Path.GetTempPath(), $"Overstrike_Stage_{Guid.NewGuid()}.config");
                    SaveConfigFile(tempConfigPath);
                    // Create .stage zip
                    using (var fs = new FileStream(dlg.FileName, FileMode.Create, FileAccess.Write, FileShare.None))
                    using (var zip = new System.IO.Compression.ZipArchive(fs, System.IO.Compression.ZipArchiveMode.Create))
                    {
                        // Add config file (use asset name, e.g. 0/1234567890ABCDEF.config or original path if available)
                        var assetPath = configAssetName;
                        var entry = zip.CreateEntry(assetPath);
                        using (var entryStream = entry.Open())
                        using (var fileStream = File.OpenRead(tempConfigPath))
                        {
                            fileStream.CopyTo(entryStream);
                        }
                        // Add info.json
                        var info = new JObject
                        {
                            ["game"] = "unknown",
                            ["name"] = Path.GetFileNameWithoutExtension(dlg.FileName),
                            ["author"] = Environment.UserName,
                            ["format_version"] = 2
                        };
                        var infoEntry = zip.CreateEntry("info.json");
                        using (var infoStream = infoEntry.Open())
                        using (var writer = new StreamWriter(infoStream))
                        {
                            writer.Write(info.ToString());
                        }
                    }
                    File.Delete(tempConfigPath);
                    StatusText.Text = $"Saved stage: {Path.GetFileName(dlg.FileName)}";
                }
                catch (Exception ex)
                {
                    StatusText.Text = $"Error saving stage: {ex.Message}";
                }
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private void SaveJsonButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog();
            dlg.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
            var defaultName = (_selectedAssetName ?? _originalAssetName ?? Path.GetFileName(_currentFilePath) ?? "config") + ".json";
            if (defaultName.EndsWith(".config.json"))
                defaultName = defaultName.Replace(".config.json", ".json");
            dlg.FileName = defaultName;
            if (dlg.ShowDialog() == true)
            {
                try
                {
                    File.WriteAllText(dlg.FileName, JsonEditor.Text);
                    StatusText.Text = $"Saved JSON: {System.IO.Path.GetFileName(dlg.FileName)}";
                }
                catch (Exception ex)
                {
                    StatusText.Text = $"Error saving JSON: {ex.Message}";
                }
            }
        }

        private async void AddToModButton_Click(object sender, RoutedEventArgs e)
        {
            Dispatcher.Invoke(() => ShowOverlay(true));
            try
            {
                var tempConfigPath = Path.Combine(Path.GetTempPath(), _selectedAssetName ?? _originalAssetName ?? "config.config");
                await Task.Run(() => SaveConfigFile(tempConfigPath));
                Dispatcher.Invoke(() => {
                    StatusText.Text = "Added to mod session. Use 'Pack as .stage' in the main window.";
                });
            }
            catch (Exception ex)
            {
                // niado: i dont know what to do here
            }
            finally
            {
                Dispatcher.Invoke(() => ShowOverlay(false));
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);
            this.ShowInTaskbar = true;
            this.Owner = null;
        }

        public void SetStatusText(string text)
        {
            if (!Dispatcher.CheckAccess())
                Dispatcher.Invoke(() => StatusText.Text = text);
            else
                StatusText.Text = text;
        }

        private void ConfigEditorWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (!_pendingLoadStarted && !string.IsNullOrEmpty(_pendingConfigFilePath))
            {
                _pendingLoadStarted = true;
                Task.Run(() => LoadConfigFileAsync(_pendingConfigFilePath));
            }
        }
    }
} 