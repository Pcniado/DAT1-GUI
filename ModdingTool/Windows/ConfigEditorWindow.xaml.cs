using ICSharpCode.AvalonEdit;
using Microsoft.Win32;
using Newtonsoft.Json.Linq;
using OverstrikeShared.STG.Files;
using System;
using System.IO;
using System.Windows;
using System.Threading.Tasks;
using System.Windows.Threading;
using MahApps.Metro.Controls;

namespace ModdingTool.Windows
{
    public partial class ConfigEditorWindow : MetroWindow
    {
        private string? _currentFilePath;
        private JObject? _currentJson;
        private string? _originalAssetName;
        private string? _originalAssetFullPath;
        private string? _selectedAssetName;
        private string? _pendingConfigFilePath;
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
            this.Activated += OnActivated;
            this.Deactivated += OnDeactivated;
            this.JsonEditor.TextChanged += JsonEditor_TextChanged;
            UpdateJsonValidityIndicator();
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

        private void JsonEditor_TextChanged(object sender, EventArgs e)
        {
            UpdateJsonValidityIndicator();
        }

        private void UpdateJsonValidityIndicator()
        {
            Dispatcher.Invoke(() => {
                try
                {
                    var text = JsonEditor.Text;
                    Newtonsoft.Json.Linq.JToken.Parse(text);
                    JsonValidityIndicator.Text = "Valid";
                    JsonValidityIndicator.Foreground = System.Windows.Media.Brushes.Green;
                }
                catch
                {
                    JsonValidityIndicator.Text = "Invalid";
                    JsonValidityIndicator.Foreground = System.Windows.Media.Brushes.Red;
                }
            });
        }

        public async Task SaveConfigFileAsync(string path)
        {
            await Dispatcher.InvokeAsync(() => ShowOverlay(true));
            string tempFile = Path.GetTempFileName();
            string jsonText = string.Empty;
            await Dispatcher.InvokeAsync(() => { jsonText = JsonEditor.Text; });
            try
            {
                await Task.Run(() =>
                {
                    var json = JObject.Parse(jsonText);
                    if (json["TYPE"] == null || json["TYPE"]["Type"] == null)
                        throw new InvalidDataException("Config JSON must contain a TYPE object with a Type property.");
                    if (json["DATA"] == null)
                        throw new InvalidDataException("Config JSON must contain a DATA object.");
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
                    try
                    {
                        File.WriteAllBytes(tempFile, config.Save());
                    }
                    catch (Exception fileEx)
                    {
                        throw new IOException($"Failed to write temp file: {fileEx.Message}");
                    }
                });
               
                try
                {
                    if (File.Exists(path))
                        File.Replace(tempFile, path, null, true);
                    else
                        File.Move(tempFile, path);
                }
                catch (Exception moveEx)
                {
                    throw new IOException($"Failed to move temp file to destination: {moveEx.Message}");
                }
                await Dispatcher.InvokeAsync(() =>
                {
                    StatusText.Text = $"Saved: {System.IO.Path.GetFileName(path)}";
                });
            }
            catch (Exception ex)
            {
                await Dispatcher.InvokeAsync(() =>
                {
                    StatusText.Text = $"Error saving config: {ex.Message}";
                });
            }
            finally
            {
                try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
                await Dispatcher.InvokeAsync(() => ShowOverlay(false));
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

        private async void SaveConfigButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog();
            dlg.Filter = "Config files (*.config)|*.config|All files (*.*)|*.*";
            var defaultName = _selectedAssetName ?? _originalAssetName ?? Path.GetFileName(_currentFilePath) ?? "config.config";
            dlg.FileName = defaultName;
            if (dlg.ShowDialog() == true)
            {
                await SaveConfigFileAsync(dlg.FileName);
            }
        }

        private async void SaveStageButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog();
            dlg.Filter = "Stage files (*.stage)|*.stage|All files (*.*)|*.*";
            var configAssetName = Path.GetFileName(_currentFilePath);
            dlg.FileName = Path.GetFileNameWithoutExtension(configAssetName) + ".stage";
            if (dlg.ShowDialog() == true)
            {
                
                await Dispatcher.InvokeAsync(() => ShowOverlay(true));
                try
                {
                    var tempConfigPath = Path.Combine(Path.GetTempPath(), $"Overstrike_Stage_{Guid.NewGuid()}.config");
                    await SaveConfigFileAsync(tempConfigPath);
                    await Task.Run(() =>
                    {
                        using (var fs = new FileStream(dlg.FileName, FileMode.Create, FileAccess.Write, FileShare.None))
                        using (var zip = new System.IO.Compression.ZipArchive(fs, System.IO.Compression.ZipArchiveMode.Create))
                        {
                            var assetPath = configAssetName;
                            var entry = zip.CreateEntry(assetPath);
                            using (var entryStream = entry.Open())
                            using (var fileStream = File.OpenRead(tempConfigPath))
                            {
                                fileStream.CopyTo(entryStream);
                            }
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
                    });
                    await Dispatcher.InvokeAsync(() =>
                    {
                        StatusText.Text = $"Saved stage: {Path.GetFileName(dlg.FileName)}";
                    });
                }
                catch (Exception ex)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        StatusText.Text = $"Error saving stage: {ex.Message}";
                    });
                }
                finally
                {
                    await Dispatcher.InvokeAsync(() => ShowOverlay(false));
                }
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
        }

        private async void SaveJsonButton_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog();
            dlg.Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*";
            var defaultName = (_selectedAssetName ?? _originalAssetName ?? Path.GetFileName(_currentFilePath) ?? "config") + ".json";
            if (defaultName.EndsWith(".config.json"))
                defaultName = defaultName.Replace(".config.json", ".json");
            dlg.FileName = defaultName;
            if (dlg.ShowDialog() == true)
            {
                await Dispatcher.InvokeAsync(() => ShowOverlay(true));
                string tempFile = Path.GetTempFileName();
                string jsonText = string.Empty;
                await Dispatcher.InvokeAsync(() => { jsonText = JsonEditor.Text; });
                try
                {
                   
                    Newtonsoft.Json.Linq.JToken.Parse(jsonText);
                    try
                    {
                        await Task.Run(() => File.WriteAllText(tempFile, jsonText));
                    }
                    catch (Exception fileEx)
                    {
                        throw new IOException($"Failed to write temp file: {fileEx.Message}");
                    }
                    try
                    {
                        if (File.Exists(dlg.FileName))
                            File.Replace(tempFile, dlg.FileName, null, true);
                        else
                            File.Move(tempFile, dlg.FileName);
                    }
                    catch (Exception moveEx)
                    {
                        throw new IOException($"Failed to move temp file to destination: {moveEx.Message}");
                    }
                    await Dispatcher.InvokeAsync(() =>
                    {
                        StatusText.Text = $"Saved JSON: {System.IO.Path.GetFileName(dlg.FileName)}";
                    });
                }
                catch (Exception ex)
                {
                    await Dispatcher.InvokeAsync(() =>
                    {
                        StatusText.Text = $"Error saving JSON: {ex.Message}";
                    });
                }
                finally
                {
                    try { if (File.Exists(tempFile)) File.Delete(tempFile); } catch { }
                    await Dispatcher.InvokeAsync(() => ShowOverlay(false));
                }
            }
        }

        private async void AddToModButton_Click(object sender, RoutedEventArgs e)
        {
            Dispatcher.Invoke(() => ShowOverlay(true));
            try
            {
                var tempConfigPath = Path.Combine(Path.GetTempPath(), _selectedAssetName ?? _originalAssetName ?? "config.config");
                await Task.Run(() => SaveConfigFileAsync(tempConfigPath));
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

        private void OnActivated(object sender, EventArgs e)
        {
            this.WindowTitleBrush = (System.Windows.Media.LinearGradientBrush)FindResource("AppTitleBarGradient");
        }

        private void OnDeactivated(object sender, EventArgs e)
        {
            this.WindowTitleBrush = (System.Windows.Media.LinearGradientBrush)FindResource("AppTitleBarGradient");
        }
    }
} 