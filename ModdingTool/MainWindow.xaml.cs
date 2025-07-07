//
// This program is free software, and can be redistributed and/or modified by you. It is provided 'as-is', without any warranty.
// For more details, terms and conditions, see GNU General Public License.
// A copy of the that license should come with this program (LICENSE.txt). If not, see <http://www.gnu.org/licenses/>.

using DAT1;
using Microsoft.WindowsAPICodePack.Dialogs;
using ModdingTool.Structs;
using ModdingTool.Utils;
using ModdingTool.Windows;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Threading.Tasks;
using System.Net.Http;
using MahApps.Metro.Controls;

namespace ModdingTool {
	public partial class MainWindow: MetroWindow {
		// tick
		private Thread? _tickThread;
		private List<Thread> _taskThreads = new();

		// settings
		private List<string> _recentPaths = new();

		// loaded data
		private TOCBase? _toc = null;
		private List<Asset> _assets = new();
		private Dictionary<string, List<int>> _assetsByPath = new();
		private ObservableCollection<Asset> _displayedAssetList = new();

		// replaced data
		private Dictionary<Asset, string> _replacedAssets = new();
		private Dictionary<Asset, string> _addedAssets = new();

		// ui
		private SearchWindow? _searchWindow = null;
		private HashToolWindow? _hashToolWindow = null;

		private ConfigEditorWindow? _configEditorWindow = null;

		private string? _currentProjectFolder = null;
		private string? _currentModName = null;
		private string? _currentAuthor = null;
		private string? _gameId = null;
		private string? _gamePath = null;
		private string? _lastLoadedTocPath = null;
		private bool _projectDirty = false;
		private List<string> _recentProjectFolders = new();
		private const int MaxRecentProjects = 5;

		public MainWindow() {
			InitializeComponent();
			this.Activated += OnActivated;
			this.Deactivated += OnDeactivated;
			CommandBindings.Add(new CommandBinding(AssetsListContextMenu.ExtractAssetCommand, ContextMenu_ExtractAsset));
			CommandBindings.Add(new CommandBinding(AssetsListContextMenu.ExtractAssetToStageCommand, ContextMenu_ExtractAssetToStage));
			CommandBindings.Add(new CommandBinding(AssetsListContextMenu.ReplaceAssetCommand, ContextMenu_ReplaceAsset));
			CommandBindings.Add(new CommandBinding(AssetsListContextMenu.ReplaceAssetsCommand, ContextMenu_ReplaceAssets));
			CommandBindings.Add(new CommandBinding(AssetsListContextMenu.CopyPathCommand, ContextMenu_CopyPath));
			CommandBindings.Add(new CommandBinding(AssetsListContextMenu.CopyRefCommand, ContextMenu_CopyRef));
			CommandBindings.Add(new CommandBinding(AssetsListContextMenu.EditConfigCommand, ContextMenu_EditConfig));

			StartTickThread();
			LoadSettings();

			if (_recentPaths.Count > 0 ) {
				StartLoadTOCThread(_recentPaths[0]);
			}
		}

		public MainWindow(string projectFolder, string modName, string author, bool isNewProject = false)
		{
			InitializeComponent();
			this.Activated += OnActivated;
			this.Deactivated += OnDeactivated;
			CommandBindings.Add(new CommandBinding(AssetsListContextMenu.ExtractAssetCommand, ContextMenu_ExtractAsset));
			CommandBindings.Add(new CommandBinding(AssetsListContextMenu.ExtractAssetToStageCommand, ContextMenu_ExtractAssetToStage));
			CommandBindings.Add(new CommandBinding(AssetsListContextMenu.ReplaceAssetCommand, ContextMenu_ReplaceAsset));
			CommandBindings.Add(new CommandBinding(AssetsListContextMenu.ReplaceAssetsCommand, ContextMenu_ReplaceAssets));
			CommandBindings.Add(new CommandBinding(AssetsListContextMenu.CopyPathCommand, ContextMenu_CopyPath));
			CommandBindings.Add(new CommandBinding(AssetsListContextMenu.CopyRefCommand, ContextMenu_CopyRef));
			CommandBindings.Add(new CommandBinding(AssetsListContextMenu.EditConfigCommand, ContextMenu_EditConfig));

			StartTickThread();
			LoadSettings();

			_currentProjectFolder = projectFolder;
			_currentModName = modName;
			_currentAuthor = author;
			_replacedAssets.Clear();
			var stageJsonPath = System.IO.Path.Combine(projectFolder, "stage.json");
			if (File.Exists(stageJsonPath))
			{
				var json = System.IO.File.ReadAllText(stageJsonPath);
				var project = System.Text.Json.JsonSerializer.Deserialize<ModdingTool.Structs.ModProject>(json);
				_gameId = project.GameId;
				_gamePath = project.GamePath;
			}
			SetProjectDirty(false);
			UpdateWindowTitle();
			ShowAssetsFromFolder("");
			AddRecentProject(projectFolder);
			if (!string.IsNullOrEmpty(_gamePath))
			{
				var tocPath = System.IO.Path.Combine(_gamePath, "toc");
				if (File.Exists(tocPath))
				{
					StartLoadTOCThread(tocPath);
					_lastLoadedTocPath = tocPath;
				}
			}
			if (isNewProject)
			{
				this.Loaded += (s, e) => {
					ShowCustomMessageBox($"Created new project at: {_currentProjectFolder}", "New Project");
				};
			}
		}

		private void OnActivated(object sender, EventArgs e)
		{
            this.WindowTitleBrush = (System.Windows.Media.Brush)FindResource("AppTitleBarGradient");
        }

		private void OnDeactivated(object sender, EventArgs e)
		{
            this.WindowTitleBrush = (System.Windows.Media.Brush)FindResource("AppTitleBarGradient");
        }

		#region tick

		private void StartTickThread() {
			_tickThread = new Thread(TickThread);
			_tickThread.Start();
		}

		private void TickThread() {
			try {
				while (true) {
					Thread.Sleep(16);
					Tick();
				}
			} catch {}
		}

		private void Tick() {
			List<Thread> threadsToRemove = new();
			foreach (var thread in _taskThreads) {
				if (!thread.IsAlive) {
					threadsToRemove.Add(thread);
				}
			}
			foreach (Thread thread in threadsToRemove) {
				_taskThreads.Remove(thread);
			}

			bool hasTasks = _taskThreads.Count > 0;
			Dispatcher.Invoke(() => {
				Overlay.Visibility = (hasTasks ? Visibility.Visible : Visibility.Collapsed);
			});
		}

		#endregion
		#region settings

		private void LoadSettings() {
			LoadRecentTxt();
		}

		private void LoadRecentTxt() {
			_recentPaths.Clear();

			var fn = "recent.txt";
			if (File.Exists(fn)) {
				foreach (var line in File.ReadLines(fn)) {
					if (line == null) continue;

					var l = line.Trim();
					if (l != "") _recentPaths.Add(l);
				}
			}
		}

		private void SaveRecentTxt() {
			using var f = File.OpenWrite("recent.txt");
			using var w = new StreamWriter(f);
			foreach (var l in _recentPaths) {
				w.WriteLine(l);
			}
		}

		#endregion
		#region load toc

		private void StartLoadTOCThread(string path) {
			string baseDir = path;
			if (File.Exists(path)) {
				baseDir = Path.GetDirectoryName(path);
			}
			if (!Directory.Exists(baseDir)) {
				return;
			}

			string tocPath = null;
			string gameFolder = null;
			string detectedGameId = null;

			// i20: asset_archive/toc
			string i20Toc = Path.Combine(baseDir, "asset_archive", "toc");
			if (File.Exists(i20Toc)) {
				tocPath = i20Toc;
				gameFolder = baseDir;
				// Try to detect game by exe
				if (File.Exists(Path.Combine(baseDir, "MilesMorales.exe"))) detectedGameId = "msmr";
				else if (File.Exists(Path.Combine(baseDir, "MM.exe"))) detectedGameId = "mm";
			}
			// i29+: toc in game folder
			else if (File.Exists(Path.Combine(baseDir, "toc"))) {
				tocPath = Path.Combine(baseDir, "toc");
				gameFolder = baseDir;
				if (File.Exists(Path.Combine(baseDir, "RiftApart.exe"))) detectedGameId = "rcra";
				else if (File.Exists(Path.Combine(baseDir, "Spider-Man2.exe"))) detectedGameId = "msm2";
				else if (File.Exists(Path.Combine(baseDir, "i33.exe"))) detectedGameId = "i33";
			}
			// fallback: toc.BAK in game folder
			else if (File.Exists(Path.Combine(baseDir, "toc.BAK"))) {
				tocPath = Path.Combine(baseDir, "toc.BAK");
				gameFolder = baseDir;
			}
			else {
				// Not found
				return;
			}

			_recentPaths.Remove(baseDir);
			_recentPaths.Insert(0, baseDir);
			SaveRecentTxt();

			// Store gameFolder and gameId for later use
			_gamePath = gameFolder;
			if (!string.IsNullOrEmpty(detectedGameId))
				_gameId = detectedGameId;

			// Save project if loaded (to persist gameId/gamePath)
			SaveProjectIfLoaded();

			Thread thread = new(() => LoadTOC(tocPath));
			_taskThreads.Add(thread);
			thread.Start();
		}

		class TreeNode {
			public Dictionary<string, TreeNode> Children = new();
			public TreeNode() {}
		}

        private void LoadTOC(string path)
        {
            Dispatcher.Invoke(() => {
                OverlayHeaderLabel.Text = "Loading 'toc'...";
                OverlayOperationLabel.Text = "-";
            });

            string hashesFileToLoad = null;
            try
            {
                var tocDir = Path.GetDirectoryName(path);
                string hashesUrl = null;
                string exeName = null;
                string hashesTarget = null;
                bool needDownload = false;
                bool gameDetected = false;
                string exeSearchDir = tocDir;
                if (path.Replace('/', '\\').EndsWith("asset_archive\\toc", StringComparison.OrdinalIgnoreCase)) {
                    exeSearchDir = Directory.GetParent(Directory.GetParent(path).FullName).FullName; // parent of asset_archive
                }

                using (var f = File.OpenRead(path))
                using (var r = new BinaryReader(f))
                {
                    uint magic = r.ReadUInt32();
                    if (magic == 0x77AF12AF)
                    { // TOC_I20 (MSMR/MM)
                        string[] exes = new[] { "MilesMorales.exe", "MM.exe" };
                        foreach (var exe in exes)
                        {
                            if (File.Exists(Path.Combine(exeSearchDir, exe)))
                            {
                                exeName = exe;
                                break;
                            }
                        }
                        if (exeName == "MilesMorales.exe")
                        {
                            hashesUrl = "https://raw.githubusercontent.com/Pcniado/IGHASHES/refs/heads/main/hashes_i31.txt";
                            hashesTarget = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "hashes_i31.txt");
                            hashesFileToLoad = hashesTarget;
                            gameDetected = true;
                            needDownload = !File.Exists(hashesTarget);
                        }
                        else
                        {
                            hashesUrl = "https://raw.githubusercontent.com/Pcniado/IGHASHES/refs/heads/main/hashes_i20.txt";
                            hashesTarget = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "hashes_i20.txt");
                            hashesFileToLoad = hashesTarget;
                            gameDetected = true;
                            needDownload = !File.Exists(hashesTarget);
                        }
                    }
                    else if (magic == 0x34E89035)
                    { // TOC_I29 (RCRA/MSM2/i33)
                        string[] exes = new[] { "RiftApart.exe", "Spider-Man2.exe", "i33.exe" };
                        foreach (var exe in exes)
                        {
                            if (File.Exists(Path.Combine(exeSearchDir, exe)))
                            {
                                exeName = exe;
                                break;
                            }
                        }
                        if (exeName == "RiftApart.exe")
                        {
                            hashesUrl = "https://raw.githubusercontent.com/Pcniado/IGHASHES/refs/heads/main/hashes_i29.txt";
                            hashesTarget = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "hashes_i29.txt");
                            hashesFileToLoad = hashesTarget;
                            gameDetected = true;
                            needDownload = !File.Exists(hashesTarget);
                        }
                        else if (exeName == "Spider-Man2.exe")
                        {
                            hashesUrl = "https://raw.githubusercontent.com/Pcniado/IGHASHES/refs/heads/main/hashes_i30.txt";
                            hashesTarget = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "hashes_i30.txt");
                            hashesFileToLoad = hashesTarget;
                            gameDetected = true;
                            needDownload = !File.Exists(hashesTarget);
                        }
                        else if (exeName == "i33.exe")
                        {
                            hashesUrl = "https://raw.githubusercontent.com/Pcniado/IGHASHES/refs/heads/main/hashes_i33.txt";
                            hashesTarget = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "hashes_i33.txt");
                            hashesFileToLoad = hashesTarget;
                            gameDetected = true;
                            needDownload = !File.Exists(hashesTarget);
                        }
                    }
                }
                if (gameDetected && needDownload && hashesUrl != null && hashesTarget != null)
                {
                    Dispatcher.Invoke(() => {
                        OverlayHeaderLabel.Text = $"Downloading hashes...";
                        OverlayOperationLabel.Text = hashesUrl;
                    });
                    using (var client = new System.Net.Http.HttpClient())
                    {
                        using (var response = client.GetAsync(hashesUrl, System.Net.Http.HttpCompletionOption.ResponseHeadersRead).GetAwaiter().GetResult())
                        {
                            response.EnsureSuccessStatusCode();
                            var contentLength = response.Content.Headers.ContentLength;
                            using (var stream = response.Content.ReadAsStreamAsync().GetAwaiter().GetResult())
                            using (var fs = new FileStream(hashesTarget, FileMode.Create, FileAccess.Write, FileShare.None))
                            {
                                var buffer = new byte[8192];
                                long totalRead = 0;
                                int read;
                                int lastKb = 0;
                                int totalKb = contentLength.HasValue ? (int)(contentLength.Value / 1024) : -1;
                                while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
                                {
                                    fs.Write(buffer, 0, read);
                                    totalRead += read;
                                    int currentKb = (int)(totalRead / 1024);
                                    if (currentKb != lastKb || read == 0)
                                    {
                                        lastKb = currentKb;
                                        Dispatcher.Invoke(() => {
                                            if (totalKb > 0)
                                            {
                                                OverlayHeaderLabel.Text = $"Downloading hashes... ({currentKb}/{totalKb} KB)";
                                            }
                                            else
                                            {
                                                OverlayHeaderLabel.Text = $"Downloading hashes... ({currentKb} KB)";
                                            }
                                            OverlayOperationLabel.Text = hashesUrl;
                                        });
                                    }
                                }
                            }
                        }
                    }
                    // fix for the file not being released
                    bool fileReady = false;
                    for (int attempt = 0; attempt < 10; attempt++)
                    {
                        try
                        {
                            using (var sr = new FileStream(hashesTarget, FileMode.Open, FileAccess.Read, FileShare.Read))
                            {
                                fileReady = true;
                            }
                            break;
                        }
                        catch (IOException)
                        {
                            Thread.Sleep(100);
                        }
                    }
                    if (!fileReady)
                    {
                        Dispatcher.Invoke(() => {
                            OverlayHeaderLabel.Text = $"Failed to access hashes file after download.";
                            OverlayOperationLabel.Text = "-";
                        });
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                Dispatcher.Invoke(() => {
                    OverlayHeaderLabel.Text = $"Failed to download hashes: {ex.Message}";
                    OverlayOperationLabel.Text = "-";
                });

            }

            // toc
			_toc = LoadTOCFile(path);
			if (_toc == null) {
				return;
			}

			var archiveNames = new List<string>();
			for (uint i = 0; i < _toc.GetArchivesCount(); ++i) {
				var fn = _toc.GetArchiveFilename(i);

				if (_toc is TOC_I29 && fn.StartsWith("d\\")) { // for RCRA to look a bit better
					fn = fn.Substring(2);
				}

				archiveNames.Add(fn);
			}

			_assets.Clear();
			_replacedAssets.Clear();

			var progress = 0;
			var progressTotal = _toc.AssetIdsSection.Values.Count;
			byte spanIndex = 0;
			foreach (var span in _toc.SpansSection.Values) {
				for (int i = (int)span.AssetIndex; i < span.AssetIndex + span.Count; ++i) {
					var hasHeader = (spanIndex % 8 == 0);
					if (hasHeader && _toc is TOC_I29) {
						hasHeader = (((TOC_I29)_toc).SizesSection.Values[i].HeaderOffset != -1);
					}

					_assets.Add(new Asset {
						Span = spanIndex,
						Id = _toc.AssetIdsSection.Values[i],
						Size = (uint)_toc.GetSizeInArchiveByAssetIndex(i),
						HasHeader = hasHeader,
						Name = "",
						Archive = archiveNames[(int)_toc.GetArchiveIndexByAssetIndex(i)]
					});

					++progress;
					if (progress % 1000 == 0) {
						Dispatcher.Invoke(() => {
							OverlayHeaderLabel.Text = "Loading 'toc'...";
							OverlayOperationLabel.Text = $"{progress}/{progressTotal} assets";
						});
					}
				}
				++spanIndex;
			}

			Dispatcher.Invoke(() => {
				OverlayOperationLabel.Text = $"-";
			});

			// hashes
			var appdir = AppDomain.CurrentDomain.BaseDirectory;
			string fallbackHashes = Path.Combine(appdir, "hashes.txt");
			string hashes_fn = null;
			if (hashesFileToLoad != null && File.Exists(hashesFileToLoad)) {
				hashes_fn = hashesFileToLoad;
			} else if (File.Exists(fallbackHashes)) {
				hashes_fn = fallbackHashes;
			} else {
				// As a last resort, try hashes_i30.txt if it exists
				string hashes_i30 = Path.Combine(appdir, "hashes_i30.txt");
				if (File.Exists(hashes_i30)) {
					hashes_fn = hashes_i30;
				}
			}
			var knownHashes = new Dictionary<ulong, string>();
			if (hashes_fn != null && File.Exists(hashes_fn)) {
				var lines = File.ReadLines(hashes_fn);
				progress = 0;
				progressTotal = lines.Count();
				foreach (var line in lines) {
					try {
						var firstComma = line.IndexOf(',');
						if (firstComma == -1) continue;

						var lastComma = line.LastIndexOf(',');
						var assetPath = (lastComma == -1 ? line.Substring(firstComma + 1) : line.Substring(firstComma + 1, lastComma - firstComma - 1));
						var assetId = ulong.Parse(line.Substring(0, firstComma), System.Globalization.NumberStyles.HexNumber);

						if (assetPath.Trim().Length > 0) {
							knownHashes.Add(assetId, assetPath);
						}
					} catch { }

					++progress;
					if (progress % 1000 == 0) {
						Dispatcher.Invoke(() => {
							OverlayHeaderLabel.Text = $"Loading '{Path.GetFileName(hashes_fn)}'...";
							OverlayOperationLabel.Text = $"{progress}/{progressTotal} hashes";
						});
					}
				}
			}
			Dispatcher.Invoke(() => {
				OverlayHeaderLabel.Text = $"Loaded '{(hashes_fn != null ? Path.GetFileName(hashes_fn) : "NO HASHES FILE FOUND")}'";
				OverlayOperationLabel.Text = "-";
			});

			// tree

			_assetsByPath.Clear();
			TreeNode root = new();
			root.Children["[UNKNOWN]"] = new();
			root.Children["[WEM]"] = new();

			void AddPath(string dir, int assetIndex, bool makeFullPath = false) {
				if (assetIndex < 0 || assetIndex >= _assets.Count) return;
				if (dir == null) dir = "";
				if (makeFullPath)
					_assets[assetIndex].FullPath = Path.Combine(dir, _assets[assetIndex].Name);

				if (dir == "") dir = "/";
				var parts = dir.Split("\\");
				var currentNode = root;
				foreach (var part in parts) {
					if (!currentNode.Children.ContainsKey(part)) {
						currentNode.Children.Add(part, new());
					}
					currentNode = currentNode.Children[part];
				}

				if (!_assetsByPath.ContainsKey(dir)) {
					_assetsByPath[dir] = new();
				}
				_assetsByPath[dir].Add(assetIndex);
			};

			// tree: named assets

			progress = 0;
			progressTotal = _assets.Count;

			var usedHashes = new Dictionary<ulong, string>();
			for (var i = 0; i < _assets.Count; ++i) {
				var asset = _assets[i];
				var assetId = asset.Id;
				if (knownHashes.ContainsKey(assetId)) {
					var assetPath = DAT1.Utils.Normalize(knownHashes[assetId]);
					usedHashes[assetId] = assetPath;
					asset.Name = Path.GetFileName(assetPath);
					AddPath(Path.GetDirectoryName(assetPath), i, true);
				}

				++progress;
				if (progress % 1000 == 0) {
					Dispatcher.Invoke(() => {
						OverlayHeaderLabel.Text = "Building tree...";
						OverlayOperationLabel.Text = $"{progress}/{progressTotal} assets";
					});
				}
			}

			Dispatcher.Invoke(() => {
				OverlayOperationLabel.Text = $"-";
			});

			// tree: other assets

			var unknown = root.Children["[UNKNOWN]"];
			var wems = root.Children["[WEM]"];
			
			for (var i = 0; i < _assets.Count; ++i) {
				var asset = _assets[i];
				if (asset.Name != "") continue;

				var assetId = asset.Id;
				var isWem = ((assetId & 0xFFFFFFFF00000000) == 0xE000000000000000);

				if (isWem) {
					var wemNumber = assetId & 0xFFFFFFFF;
					asset.Name = $"{wemNumber}.wem";
					AddPath($"[WEM]\\{asset.Archive}", i);
				} else {
					asset.Name = $"{assetId:X016}";
					AddPath($"[UNKNOWN]\\{asset.Archive}", i);
				}
			}

			// build the UI

			Dispatcher.Invoke(() => {
				OverlayHeaderLabel.Text = "Building tree...";
				OverlayOperationLabel.Text = $"-";

				void Traverse(TreeNode n, ItemCollection i) {
					var keysSorted = n.Children.Keys.ToList();
					keysSorted.Sort((x, y) => {
						if (x == y) return 0;
						if (x == null || x == "") return 1;
						if (y == null || y == "") return -1;
						
						if (x[0] == '/' || x[0] == '[') {
							if (y[0] != '/' && y[0] != '[') return 1;
							if (x[0] == y[0])
								return x.CompareTo(y);
							return x[0] - y[0];
						}

						if (y[0] == '/' || y[0] == '[') return -1;

						return x.CompareTo(y);
					});
					foreach (var k in keysSorted) {
						var i2 = new TreeViewItem() {
							Header = k
						};

						Traverse(n.Children[k], i2.Items);

						i.Add(i2);
					}
				};

				Folders.Items.Clear();
				Traverse(root, Folders.Items);

				ShowAssetsFromFolder("", Folders.Items.Count);
			});
		}

		private static TOCBase? LoadTOCFile(string tocPath) {
			TOC_I29 toc_i29 = new();
			if (toc_i29.Load(tocPath)) {
				return toc_i29;
			}

			TOC_I20 toc_i20 = new();
			if (toc_i20.Load(tocPath)) {
				return toc_i20;
			}

			return null;
		}

		#endregion
		#region common

		private void ShowAssetsFromFolder(string path, int dirs) {
			_displayedAssetList.Clear();
			List<Asset> assetList = new();

			if (_assetsByPath.ContainsKey(path)) {
				foreach (var index in GetAssetIndices(path)) {
					assetList.Add(_assets[index]);
				}

				assetList.Sort((x, y) => {
					if (x.Name == y.Name) {
						return x.Span - y.Span;
					}
					return x.Name.CompareTo(y.Name);
				});
			}

			foreach (var asset in assetList) {
				if (Path.GetExtension(asset.Name).Equals(".texture", StringComparison.OrdinalIgnoreCase) && asset.Span == 1)
				{
					var hdAsset = new Asset
					{
						Span = asset.Span,
						Id = asset.Id,
						Size = asset.Size,
						HasHeader = asset.HasHeader,
						Name = asset.Name + " (HD)",
						Archive = asset.Archive,
						FullPath = asset.FullPath
					};
					_displayedAssetList.Add(hdAsset);
				}
				else
				{
					_displayedAssetList.Add(asset);
				}
			}

			AssetsList.ItemsSource = _displayedAssetList;

			// update status bar

			CurrentPath.Text = $"Selected directory: {path}";
			var hint = "";
			if (dirs > 0) {
				hint = $"{dirs} director" + (dirs > 1 ? "ies" : "y");
			}

			if (hint == "" || assetList.Count > 0) {
				if (hint != "") hint += ", ";
				hint += $"{assetList.Count} asset" + (assetList.Count == 1 ? "" : "s");
			}
			DirectoryDetails.Text = hint;
		}

		private void ShowAssetsFromFolder(string path) {
			var parts = path.Split('\\');
			TreeViewItem currentNode = null;
			var currentItems = Folders.Items;

			var actualPath = "";
			foreach (var part in parts) {
				var found = false;
				foreach (TreeViewItem item in currentItems) {
					if ((string)(item.Header) == part) {
						currentNode = item;
						currentItems = item.Items;
						found = true;
						break;
					}
				}

				if (found) { actualPath = Path.Combine(actualPath, part); } else break;
			}

			if (path != "/" && actualPath == "") {
				ShowAssetsFromFolder("/");
				return;
			}

			if (currentNode != null) {
				currentNode.IsSelected = true;
				currentNode.BringIntoView();
			}
			ShowAssetsFromFolder(actualPath, currentItems.Count);
		}

		private void JumpTo(string path) {
			string folderToOpen = null;
			bool openAssetById = false;
			byte assetSpanToOpen = 0;
			ulong assetIdToOpen = 0;
			bool openAssetByName = false;
			string assetNameToOpen = null;

			if (Regex.IsMatch(path, "^[0-9]+/[0-9a-fA-F]{16}$")) { // ref
				var i = path.IndexOf('/');
				var span = path.Substring(0, i);
				var assetId = path.Substring(++i);

				try {
					var spanIndex = byte.Parse(span);
					var id = ulong.Parse(assetId, NumberStyles.HexNumber);
					var assetIndex = _toc.FindAssetIndex(spanIndex, id);
					if (assetIndex != -1) {
						var asset = _assets[assetIndex];

						folderToOpen = Path.GetDirectoryName(asset.FullPath);
						openAssetById = true;
						assetSpanToOpen = spanIndex;
						assetIdToOpen = id;

						if (folderToOpen == null) {
							foreach (var dirname in _assetsByPath.Keys) {
								if (GetAssetIndices(dirname).Contains(assetIndex)) {
									folderToOpen = dirname;
									break;
								}
							}
						}
					}
				} catch {}
			} else {
				if (path != "/") path = path.Replace('/', '\\');

				folderToOpen = path;
				openAssetByName = true;
				assetNameToOpen = Path.GetFileName(path);
			}

			if (folderToOpen != null) {
				ShowAssetsFromFolder(folderToOpen);

				if (openAssetById) {
					foreach (Asset assetItem in AssetsList.Items) {
						if (assetItem.Span == assetSpanToOpen && assetItem.Id == assetIdToOpen) {
							AssetsList.SelectedItem = assetItem;
							AssetsList.ScrollIntoView(assetItem);
							break;
						}
					}
				} else if (openAssetByName) {
					foreach (Asset assetItem in AssetsList.Items) {
						if (assetItem.Name == assetNameToOpen) {
							AssetsList.SelectedItem = assetItem;
							AssetsList.ScrollIntoView(assetItem);
							break;
						}
					}
				}
			}
		}

		private void ExtractOneAssetDialog(Asset asset) {
			CommonSaveFileDialog dialog = new();
			dialog.Title = "Extract asset...";
			dialog.RestoreDirectory = true;
			dialog.Filters.Add(new CommonFileDialogFilter("All files", "*") { ShowExtensions = true });
			dialog.DefaultFileName = asset.Name;

			if (dialog.ShowDialog() != CommonFileDialogResult.Ok) {
				return;
			}

			ExtractAsset(asset, dialog.FileName);
		}

		private void ExtractMultipleAssetsDialog(System.Collections.IList assets) {
			CommonOpenFileDialog dialog = new();
			dialog.Title = "Select directory to extract assets to...";
			dialog.IsFolderPicker = true;
			dialog.RestoreDirectory = true;

			var result = dialog.ShowDialog();
			Activate();

			if (result != CommonFileDialogResult.Ok) {
				return;
			}

			var path = dialog.FileName;
			if (!Directory.Exists(path)) {
				return;
			}

			foreach (var item in assets) {
				var asset = (Asset)item;
				ExtractAsset(asset, Path.Combine(path, asset.Name));
			}
		}

		private void ExtractAssetsToStageDialog(System.Collections.IList assets) {
			var window = new StageSelector();
			window.ShowDialog();

			if (window.Stage == null) return;

			var cwd = Directory.GetCurrentDirectory();
			var path = Path.Combine(cwd, "stages");
			var stagePath = Path.Combine(path, window.Stage);
			if (!Directory.Exists(stagePath)) Directory.CreateDirectory(stagePath);

			foreach (var item in assets) {
				var asset = (Asset)item;

				var dirname = Path.Combine(stagePath, $"{asset.Span}");
				var assetPath = Path.Combine(dirname, $"{asset.Id:X016}");
				if (asset.FullPath != null) {
					assetPath = Path.Combine(stagePath, $"{asset.Span}", asset.FullPath);
					dirname = Path.GetDirectoryName(assetPath);
				}

				if (!Directory.Exists(dirname)) Directory.CreateDirectory(dirname);
				ExtractAsset(asset, assetPath);
			}
		}

		private string GetTextureExportFileName(string originalFileName, int span)
		{
			string extension = Path.GetExtension(originalFileName);
			string baseName = Path.GetFileNameWithoutExtension(originalFileName);
			if (span == 1)
			{
				return $"{baseName}.hd{extension}";
			}
			else
			{
				return originalFileName;
			}
		}

		private void ExtractAsset(Asset asset, string path) {
			try {
				var bytes = _toc.GetAssetBytes(asset.Span, asset.Id);
				byte[] header = null;
				byte[] textureMeta = null;

				if (_toc is TOC_I29 toc_i29) {
					var index = _toc.FindAssetIndex(asset.Span, asset.Id);
					header = toc_i29.GetHeaderByAssetIndex(index);
					textureMeta = toc_i29.GetTextureMetaByAssetIndex(index);
				}

                // Updated code: strip " (HD)" before checking extension
                string assetNameForExport = asset.Name;
                if (assetNameForExport.EndsWith(" (HD)", StringComparison.OrdinalIgnoreCase))
                    assetNameForExport = assetNameForExport.Substring(
                        0, assetNameForExport.Length - " (HD)".Length);
                string extension = Path.GetExtension(assetNameForExport);
                if (extension.Equals(".texture", StringComparison.OrdinalIgnoreCase))
                {
                    string exportFileName = GetTextureExportFileName(assetNameForExport, asset.Span);
                    path = Path.Combine(Path.GetDirectoryName(path), exportFileName);
                }

                var packExtras = true;
				var hasExtras = (header != null || textureMeta != null);
				if (packExtras && hasExtras) {
					using var ms = new MemoryStream();
					using var w = new BinaryWriter(ms);
					w.Write(0x00475453);
					uint flags = 0;
					if (header != null) flags |= 0x1;
					if (textureMeta != null) flags |= 0x2;
					w.Write(flags);
					w.Write((header == null ? 0 : header.Length));
					w.Write((textureMeta == null ? 0 : textureMeta.Length));
					if (header != null) {
						w.Write(header);
						Align16(w);
					}
					if (textureMeta != null) {
						w.Write(textureMeta);
						Align16(w);
					}
					w.Write(bytes);
					File.WriteAllBytes(path, ms.ToArray());
				} else {
					File.WriteAllBytes(path, bytes);
				}
			} catch {}

			static void Align16(BinaryWriter w) {
				var pos = w.BaseStream.Position % 16;
				if (pos != 0) {
					var rem = 16 - pos;
					w.Write(new byte[rem]);
				}
			}
		}

		private void ExtractFolder(string folder, string path) {
			Dispatcher.Invoke(() => {
				OverlayHeaderLabel.Text = "Scanning tree...";
				OverlayOperationLabel.Text = "-";
			});

			Dictionary<string, List<int>> matchingPaths = new();
			var foundAssetsTotal = 0;
			foreach (var _path in _assetsByPath.Keys) {
				if (_path.StartsWith(folder)) {
					var assets = GetAssetIndices(_path);
					matchingPaths.Add(Path.GetRelativePath(folder, _path), assets);
					foundAssetsTotal += assets.Count;

					Dispatcher.Invoke(() => {
						OverlayOperationLabel.Text = folder;
					});
				}
			}

			// remember which assets have the same name

			Dispatcher.Invoke(() => {
				OverlayHeaderLabel.Text = "Scanning tree...";
				OverlayOperationLabel.Text = "-";
			});

			Dictionary<ulong, int> countById = new();
			foreach (var suffix in matchingPaths.Keys) {
				foreach (var assetIndex in matchingPaths[suffix]) {
					var asset = _assets[assetIndex];
					countById.Update(asset.Id, 1, (int mapValue, int updateValue) => { return mapValue + updateValue; });
				}
			}

			// extract

			var progress = 0;
			var progressTotal = foundAssetsTotal;
			foreach (var suffix in matchingPaths.Keys) {
				var dirname = Path.Combine(path, suffix);
				if (!Directory.Exists(dirname)) Directory.CreateDirectory(dirname);

				foreach (var assetIndex in matchingPaths[suffix]) {
					var asset = _assets[assetIndex];
					Dispatcher.Invoke(() => {
						OverlayHeaderLabel.Text = $"Extracting assets ({progress}/{progressTotal} done)...";
						OverlayOperationLabel.Text = $"'{asset.Name}'";
					});

					var assetPath = Path.Combine(dirname, asset.Name);
					if (countById[asset.Id] > 1) {
						assetPath = Path.Combine(dirname, $"{asset.Name}.{asset.Span}");
					}
					ExtractAsset(asset, assetPath);
					++progress;
				}
			}
		}

	private void ExtractFolderToStage(string folder, string stage) {
			var cwd = Directory.GetCurrentDirectory();
			var path = Path.Combine(cwd, "stages");
			var stagePath = Path.Combine(path, stage);
			if (!Directory.Exists(stagePath)) Directory.CreateDirectory(stagePath);

			Dispatcher.Invoke(() => {
				OverlayHeaderLabel.Text = "Scanning tree...";
				OverlayOperationLabel.Text = "-";
			});

			Dictionary<string, List<int>> matchingPaths = new();
			var foundAssetsTotal = 0;
			foreach (var _path in _assetsByPath.Keys) {
				if (_path.StartsWith(folder)) {
					var assets = GetAssetIndices(_path);
					matchingPaths.Add(_path, assets);
					foundAssetsTotal += assets.Count;

					Dispatcher.Invoke(() => {
						OverlayOperationLabel.Text = folder;
					});
				}
			}

			// extract

			var progress = 0;
			var progressTotal = foundAssetsTotal;
			foreach (var suffix in matchingPaths.Keys) {
				foreach (var assetIndex in matchingPaths[suffix]) {
					var asset = _assets[assetIndex];
					Dispatcher.Invoke(() => {
						OverlayHeaderLabel.Text = $"Extracting assets ({progress}/{progressTotal} done)...";
						OverlayOperationLabel.Text = $"'{asset.Name}'";
					});

					var dirname = Path.Combine(stagePath, $"{asset.Span}", suffix);
					var assetPath = Path.Combine(dirname, asset.Name);
					if (asset.FullPath == null) {
						dirname = Path.Combine(stagePath, $"{asset.Span}");
						assetPath = Path.Combine(dirname, $"{asset.Id:X016}");
					}

					if (!Directory.Exists(dirname)) Directory.CreateDirectory(dirname);

					ExtractAsset(asset, assetPath);
					++progress;
				}
			}
		}

		private void CloseSearchWindow() {
			if (_searchWindow != null) {
				_searchWindow.Close();
			}
		}

		private void CloseHashToolWindow() {
			if (_hashToolWindow != null) {
				_hashToolWindow.Close();
			}
		}

		private static void SetClipboard(string text) {
			try {
				Clipboard.SetText(text);
			} catch {
				// if failed once, try a few more times (in some cases clipboard in windows might fail to open)
				for (int i = 0; i < 10; i++) {
					try {
						Clipboard.SetText(text);
						return;
					} catch {
						continue;
					}
				}

				// if reached here, it means we failed all those times
				// TODO: angry beep or something to warn user of failed clipboard copy
			}
		}

		#endregion
		#region event handlers

		#region menu

		private void File_LoadToc_Click(object sender, RoutedEventArgs e) {
			CommonOpenFileDialog dialog = new CommonOpenFileDialog();
			dialog.Title = "Select 'toc' to load...";
			dialog.Multiselect = false;
			dialog.RestoreDirectory = true;
			dialog.Filters.Add(new CommonFileDialogFilter("All files", "*") { ShowExtensions = true });

			if (dialog.ShowDialog() != CommonFileDialogResult.Ok) {
				return;
			}

			CloseSearchWindow();
			StartLoadTOCThread(dialog.FileName);
		}

		private void File_SubmenuOpened(object sender, RoutedEventArgs e) {
			File_LoadRecent.Visibility = (_recentPaths.Count > 0 ? Visibility.Visible : Visibility.Collapsed);

			void UpdateItem(MenuItem item, int index) {
				item.Visibility = (_recentPaths.Count > index ? Visibility.Visible : Visibility.Collapsed);
				item.Header = (_recentPaths.Count > index ? _recentPaths[index] : "").Replace("_", "__");
			};

			UpdateItem(File_LoadRecent1, 0);
			UpdateItem(File_LoadRecent2, 1);
			UpdateItem(File_LoadRecent3, 2);
			UpdateItem(File_LoadRecent4, 3);
			UpdateItem(File_LoadRecent5, 4);
		}

		private void File_LoadRecentItem_Click(object sender, RoutedEventArgs e) {
			bool CheckItem(MenuItem item, int index) {
				if (sender == item) {
					if (_recentPaths.Count > index) {
						CloseSearchWindow();
						StartLoadTOCThread(_recentPaths[index]);
					}
					return true;
				}
				return false;
			};

			if (CheckItem(File_LoadRecent1, 0)) {}
			else if (CheckItem(File_LoadRecent2, 1)) {}
			else if (CheckItem(File_LoadRecent3, 2)) {}
			else if (CheckItem(File_LoadRecent4, 3)) {}
			else if (CheckItem(File_LoadRecent5, 4)) {}
		}

		private void Search_Search_Click(object sender, RoutedEventArgs e) {
			if (_searchWindow == null) {
				_searchWindow = new SearchWindow(_assets, _assetsByPath, JumpTo, AssetsListContextMenuClicked);
				_searchWindow.Closed += (object? sender, EventArgs e) => {
					_searchWindow = null;
				};
				_searchWindow.Show();
			} else {
				_searchWindow.Focus();
			}
		}

		private void Search_JumpTo_Click(object sender, RoutedEventArgs e) {
			var window = new JumpToWindow();
			window.ShowDialog();

			if (!window.Jumped) return;
			JumpTo(window.Path.Trim());
		}

		private void Mod_SubmenuOpened(object sender, RoutedEventArgs e) {
			Mod_ReplacedItemsCount.Header = $"{_replacedAssets.Count} replaced, {_addedAssets.Count} new";

			Mod_ClearReplaced.IsEnabled = (_replacedAssets.Count + _addedAssets.Count > 0);
			Mod_ReplaceAssetsFromStage.IsEnabled = StagesExist();
		}

		private bool StagesExist() {
			var cwd = Directory.GetCurrentDirectory();
			var path = Path.Combine(cwd, "stages");
			if (Directory.Exists(path)) {
				var dirs = Directory.GetDirectories(path, "*", SearchOption.TopDirectoryOnly);
				return (dirs.Length > 0);
			}
			return false;
		}


		private void Mod_ClearReplaced_Click(object sender, RoutedEventArgs e) {
			var result = MessageBox.Show("Are you sure?", "Clear all replaced and added files", MessageBoxButton.YesNo);
			if (result == MessageBoxResult.Yes) {
				_replacedAssets.Clear();
				_addedAssets.Clear();
			}
		}

		private void Mod_ReplaceAssetsFromStage_Click(object sender, RoutedEventArgs e) {
			var window = new StageSelector();
			window.OnlyExisting = true;
			window.ShowDialog();

			if (window.Stage == null) return;

			var cwd = Directory.GetCurrentDirectory();
			var path = Path.Combine(cwd, "stages");
			var stagePath = Path.Combine(path, window.Stage);

			for (var spanIndex = 0; spanIndex < 256; ++spanIndex) {
				var spanDir = Path.Combine(stagePath, $"{spanIndex}");
				if (!Directory.Exists(spanDir)) continue;

				var files = Directory.GetFiles(spanDir, "*", SearchOption.AllDirectories);
				foreach (var file in files) {
					var relpath = Path.GetRelativePath(spanDir, file);
					string fullpath = null;
					ulong assetId;
					if (Regex.IsMatch(relpath, "^[0-9A-Fa-f]{16}$")) {
						assetId = ulong.Parse(relpath, NumberStyles.HexNumber);
					} else {
						assetId = CRC64.Hash(relpath);
						fullpath = relpath;
					}

					var assetIndex = _toc.FindAssetIndex((byte)spanIndex, assetId);
					if (assetIndex != -1) {
						var asset = _assets[assetIndex];
						_replacedAssets.Set(asset, file);
						continue;
					}

					// record to _addedAssets, updating the record if it's already present
					Asset newAsset = null;

					foreach (var addedAsset in _addedAssets.Keys) {
						if (addedAsset.Span == spanIndex && addedAsset.Id == assetId) {
							newAsset = addedAsset;
							break;
						}
					}

					var adding = (newAsset == null);
					if (adding) newAsset = new Asset();

					newAsset.Span = (byte)spanIndex;
					newAsset.Id = assetId;
					newAsset.Size = 0; // TODO?
					newAsset.HasHeader = true;
					newAsset.Name = Path.GetFileName(relpath);
					newAsset.Archive = "-";
					newAsset.FullPath = fullpath;
													
					if (adding) {
						_addedAssets.Add(newAsset, file);
					} else {
						_addedAssets.Set(newAsset, file);
					}
				}
			}
		}

		private void Mod_CreateFromReplaced_Click(object sender, RoutedEventArgs e) {
			var window = new PackStageWindow(_replacedAssets, _addedAssets, _toc);
			window.ShowDialog();
		}

		private void Mod_CreateModular_Click(object sender, RoutedEventArgs e) {
			var window = new ModularCreationWindow();
			window.ShowDialog();
		}

		private void Tools_CalculateHash_Click(object sender, RoutedEventArgs e) {
			if (_hashToolWindow == null) {
				_hashToolWindow = new HashToolWindow();
				_hashToolWindow.Closed += (object? sender, EventArgs e) => {
					_hashToolWindow = null;
				};
				_hashToolWindow.Show();
			} else {
				_hashToolWindow.Focus();
			}
		}

		private void Tools_ConfigEditor_Click(object sender, RoutedEventArgs e)
		{
			var win = new ModdingTool.Windows.ConfigEditorWindow(null, null, true, false);
			win.Show();
		}

		#endregion
		#region folders view

		private void Folders_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e) {
			if (Folders.SelectedItem == null) return;

			var path = GetSelectedFolderPath();
			ShowAssetsFromFolder(path, ((TreeViewItem)Folders.SelectedItem).Items.Count);
		}

		private void Folders_ContextMenuOpening(object sender, ContextMenuEventArgs e) {
			var element = (DependencyObject)e.OriginalSource;
			while (element != null && !(element is TreeViewItem))
				element = VisualTreeHelper.GetParent(element);

			if (element != null && element is TreeViewItem) {
				var treeItem = (TreeViewItem)element;
				treeItem.Focus();
				treeItem.IsSelected = true;
			} else {
				e.Handled = true; // don't show the menu if it wasn't tree item clicked
			}
		}

		private void FoldersMenu_ExtractAssets_Click(object sender, RoutedEventArgs e) {
			CommonOpenFileDialog dialog = new();
			dialog.Title = "Select directory to extract assets to...";
			dialog.IsFolderPicker = true;
			dialog.RestoreDirectory = true;

			var result = dialog.ShowDialog();
			Activate();

			if (result != CommonFileDialogResult.Ok) {
				return;
			}

			var path = dialog.FileName;
			if (!Directory.Exists(path)) {
				return;
			}

			//

			var folder = GetSelectedFolderPath();

			Thread thread = new(() => ExtractFolder(folder, path));
			_taskThreads.Add(thread);
			thread.Start();
		}

		private void FoldersMenu_ExtractAssetsToStage_Click(object sender, RoutedEventArgs e) {
			var window = new StageSelector();
			window.ShowDialog();

			if (window.Stage == null) return;

			//

			var folder = GetSelectedFolderPath();

			Thread thread = new(() => ExtractFolderToStage(folder, window.Stage));
			_taskThreads.Add(thread);
			thread.Start();
		}

		private void FoldersMenu_CopyPath_Click(object sender, RoutedEventArgs e) {
			var path = GetSelectedFolderPath();
			SetClipboard(path);
		}

		private string GetSelectedFolderPath() {
			string path = "";
			var selection = Folders.SelectedItem;
			while (selection != null) {
				string name = (string)((TreeViewItem)selection).Header;

				if (path != "")
					path = name + "\\" + path;
				else
					path = name;

				selection = ((TreeViewItem)selection).Parent;
				if (selection is TreeView) break;
			}
			return path;
		}

		#endregion
		#region assets list

		private void AssetsList_ContextMenuOpening(object sender, ContextMenuEventArgs e) {
			var selected = AssetsList.SelectedItems.Count;
			AssetsListContextMenu.HandleContextMenuOpening(sender, e, selected);
			if (selected == 1 && AssetsList.SelectedItem is Asset asset) {
				if (asset.Name?.EndsWith(".config", StringComparison.OrdinalIgnoreCase) ?? false)
					AssetsListContextMenu.EditConfig.Visibility = Visibility.Visible;
				else
					AssetsListContextMenu.EditConfig.Visibility = Visibility.Collapsed;
			} else {
				AssetsListContextMenu.EditConfig.Visibility = Visibility.Collapsed;
			}
		}

		// command handlers

		private void ContextMenu_ExtractAsset(object sender, ExecutedRoutedEventArgs e) {
			AssetsListContextMenuClicked("ExtractAsset", AssetsList.SelectedItems);
		}

		private void ContextMenu_ExtractAssetToStage(object sender, ExecutedRoutedEventArgs e) {
			AssetsListContextMenuClicked("ExtractAssetToStage", AssetsList.SelectedItems);
		}

		private void ContextMenu_ReplaceAsset(object sender, ExecutedRoutedEventArgs e) {
			AssetsListContextMenuClicked("ReplaceAsset", AssetsList.SelectedItems);
		}

		private void ContextMenu_ReplaceAssets(object sender, ExecutedRoutedEventArgs e) {
			AssetsListContextMenuClicked("ReplaceAssets", AssetsList.SelectedItems);
		}

		private void ContextMenu_CopyPath(object sender, ExecutedRoutedEventArgs e) {
			AssetsListContextMenuClicked("CopyPath", AssetsList.SelectedItems);
		}

		private void ContextMenu_CopyRef(object sender, ExecutedRoutedEventArgs e) {
			AssetsListContextMenuClicked("CopyRef", AssetsList.SelectedItems);
		}

		private void ContextMenu_EditConfig(object sender, ExecutedRoutedEventArgs e) {
			AssetsListContextMenuClicked("EditConfig", AssetsList.SelectedItems);
		}

		// common handler (also used by SearchWindow)

		private void AssetsListContextMenuClicked(string item, System.Collections.IList selectedAssets) {
			switch (item) {
				case "ExtractAsset": ExtractAssets(selectedAssets); break;
				case "ExtractAssetToStage": ExtractAssetsToStage(selectedAssets); break;
				case "ReplaceAsset": ReplaceAsset(selectedAssets); break;
				case "ReplaceAssets": ReplaceAssets(selectedAssets); break;
				case "CopyPath": CopyPath(selectedAssets); break;
				case "CopyRef": CopyRef(selectedAssets); break;
				case "EditConfig": EditConfig(selectedAssets); break;
			}
		}

		// actual logic

		private void ExtractAssets(System.Collections.IList assets) {
			var selected = assets.Count;
			if (selected < 1) return;

			if (selected == 1) ExtractOneAssetDialog((Asset)assets[0]);
			else ExtractMultipleAssetsDialog(assets);
		}

		private void ExtractAssetsToStage(System.Collections.IList assets) {
			var selected = assets.Count;
			if (selected < 1) return;

			ExtractAssetsToStageDialog(assets);
		}

		private void ReplaceAsset(System.Collections.IList assets) {
			var selected = assets.Count;
			if (selected != 1) return;

			CommonOpenFileDialog dialog = new CommonOpenFileDialog();
			dialog.Title = "Select file to replace asset with...";
			dialog.Multiselect = false;
			dialog.RestoreDirectory = true;
			dialog.Filters.Add(new CommonFileDialogFilter("All files", "*") { ShowExtensions = true });

			if (dialog.ShowDialog() != CommonFileDialogResult.Ok) {
				return;
			}

			var asset = (Asset)assets[0];
			var path = dialog.FileName;
			_replacedAssets.Set(asset, path);
		}

		private void ReplaceAssets(System.Collections.IList assets) {
			var dialog = new CommonOpenFileDialog();
			dialog.Title = "Select file to replace assets with...";
			dialog.Multiselect = false;
			dialog.RestoreDirectory = true;
			dialog.Filters.Add(new CommonFileDialogFilter("All files", "*") { ShowExtensions = true });

			if (dialog.ShowDialog() != CommonFileDialogResult.Ok) {
				return;
			}

			var path = dialog.FileName;
			foreach (var asset in assets) {
				_replacedAssets.Set((Asset)asset, path);
			}
		}

		private static void CopyPath(System.Collections.IList assets) {
			var selected = assets.Count;
			if (selected < 1) return;

			var paths = "";
			foreach (var item in assets) {
				var asset = (Asset)item;
				var path = asset.FullPath ?? asset.RefPath;
				paths += $"{path}\n";
			}
			SetClipboard(paths);
		}

		private static void CopyRef(System.Collections.IList assets) {
			var selected = assets.Count;
			if (selected < 1) return;

			var refs = "";
			foreach (var asset in assets) {
				refs += $"{(asset as Asset).RefPath}\n";
			}
			SetClipboard(refs);
		}

		private async void EditConfig(System.Collections.IList assets)
		{
			if (assets.Count != 1) return;
			var asset = assets[0] as Asset;
			if (asset == null || !(asset.Name?.EndsWith(".config", StringComparison.OrdinalIgnoreCase) ?? false)) return;
			var path = asset.FullPath;
			if (string.IsNullOrEmpty(path) || !File.Exists(path))
			{

				string folder = null;
				foreach (var kvp in _assetsByPath)
				{
					if (kvp.Value.Contains(_assets.IndexOf(asset)))
					{
						folder = kvp.Key;
						break;
					}
				}
				if (!string.IsNullOrEmpty(folder))
				{
					path = System.IO.Path.Combine(folder, asset.Name);
				}
			}
			if (string.IsNullOrEmpty(path) || !File.Exists(path))
			{

				var tempDir = System.IO.Path.GetTempPath();
				var tempFile = System.IO.Path.Combine(tempDir, asset.Name);
				bool extracted = false;
				await Task.Run(() => {
					try
					{
						var bytes = _toc.GetAssetBytes(asset.Span, asset.Id);
						System.IO.File.WriteAllBytes(tempFile, bytes);
						extracted = true;
					}
					catch (Exception)
					{
						extracted = false;
					}
				});
				if (!extracted)
				{
					var winErr = new ModdingTool.Windows.ConfigEditorWindow(null, asset.Name, false, true);
					winErr.Dispatcher.Invoke(() => winErr.SetStatusText("Failed to extract config."));
					winErr.Show();
					return;
				}
				path = tempFile;
			}

			if (_configEditorWindow != null)
			{
				try { _configEditorWindow.Close(); } catch { }
				_configEditorWindow = null;
			}
			var win = new ModdingTool.Windows.ConfigEditorWindow(path, asset.Name, false, true);
			_configEditorWindow = win;
			win.Closed += (s, e) => { if (_configEditorWindow == win) _configEditorWindow = null; };
			win.AddToModButton.Click += async (s, e) =>
			{
				var tempConfigPath = System.IO.Path.Combine(System.IO.Path.GetTempPath(), asset.Name);
				await win.SaveConfigFileAsync(tempConfigPath);
				this.Dispatcher.Invoke(() => {
					_replacedAssets[asset] = tempConfigPath;
				});
				win.Dispatcher.Invoke(() => {
					win.SetStatusText("Asset added to .stage");
				});
			};
			win.Show();
		}

		#endregion

		private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e) {
			CloseSearchWindow();
			CloseHashToolWindow();
			if (_configEditorWindow != null)
			{
				try { _configEditorWindow.Close(); } catch { }
				_configEditorWindow = null;
			}
		}

		#endregion

		public void SaveProject(string folderPath, string modName, string author, Dictionary<Asset, string> replacedAssets)
		{
			var project = new ModProject {
				ModName = modName,
				Author = author,
				GameId = _gameId,
				GamePath = _gamePath,
				Replacements = replacedAssets.Select(kvp => new ModProject.ReplacementEntry {
					Span = kvp.Key.Span,
					Id = kvp.Key.Id,
					Name = kvp.Key.Name,
					FullPath = kvp.Key.FullPath,
					Replacement = Path.Combine("replacements", Path.GetFileName(kvp.Value))
				}).ToList()
			};
			var replacementsDir = Path.Combine(folderPath, "replacements");
			Directory.CreateDirectory(replacementsDir);
			foreach (var kvp in replacedAssets) {
				var dest = Path.Combine(replacementsDir, Path.GetFileName(kvp.Value));
				if (!File.Exists(dest))
					File.Copy(kvp.Value, dest, true);
			}
			var json = System.Text.Json.JsonSerializer.Serialize(project, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
			File.WriteAllText(Path.Combine(folderPath, "stage.json"), json);
		}

		public (string modName, string author, Dictionary<Asset, string> replacedAssets, string gameId, string gamePath) LoadProject(string folderPath)
		{
			var json = File.ReadAllText(Path.Combine(folderPath, "stage.json"));
			var project = System.Text.Json.JsonSerializer.Deserialize<ModProject>(json);
			var replacedAssets = new Dictionary<Asset, string>();
			foreach (var entry in project.Replacements) {
				replacedAssets.Add(new Asset {
					Span = (byte)entry.Span,
					Id = entry.Id,
					Name = entry.Name,
					FullPath = entry.FullPath
				}, Path.Combine(folderPath, entry.Replacement));
			}
			return (project.ModName, project.Author, replacedAssets, project.GameId, project.GamePath);
		}

		private void SetProjectDirty(bool dirty)
		{
			_projectDirty = dirty;
			UpdateWindowTitle();
		}

		private void UpdateWindowTitle()
		{
			string baseTitle = "DAT1 GUI";
			if (!string.IsNullOrEmpty(_currentModName))
				baseTitle += $" - {_currentModName}";
			if (_projectDirty)
				baseTitle = "*" + baseTitle;
			this.Title = baseTitle;
		}

		private void SaveProjectIfLoaded()
		{
			if (!string.IsNullOrEmpty(_currentProjectFolder))
			{
				SaveProject(_currentProjectFolder, _currentModName, _currentAuthor, _replacedAssets);
				SetProjectDirty(false);
			}
		}

		private bool ShowCustomMessageBox(string message, string title = "Message", bool showCancel = false)
		{
			var msgBox = new ModdingTool.Windows.CustomMessageBox(message, title, showCancel);
			msgBox.Owner = this;
			msgBox.ShowDialog();
			return msgBox.Result == true;
		}

		private void AddRecentProject(string folderPath)
		{
			_recentProjectFolders.Remove(folderPath);
			_recentProjectFolders.Insert(0, folderPath);
			if (_recentProjectFolders.Count > MaxRecentProjects)
				_recentProjectFolders.RemoveAt(_recentProjectFolders.Count - 1);
		}

		private void ProjectMenu_SubmenuOpened(object sender, RoutedEventArgs e)
		{
			OpenRecentProjectMenu.Items.Clear();
			if (_recentProjectFolders.Count == 0)
			{
				var noItem = new System.Windows.Controls.MenuItem
				{
					Header = "(No recent projects)",
					IsEnabled = false
				};
				OpenRecentProjectMenu.Items.Add(noItem);
			}
			else
			{
				foreach (var folder in _recentProjectFolders)
				{
					var item = new System.Windows.Controls.MenuItem
					{
						Header = System.IO.Path.GetFileName(folder),
						ToolTip = folder,
						Tag = folder
					};
					item.Click += OpenRecentProject_Click;
					OpenRecentProjectMenu.Items.Add(item);
				}
			}
		}

		private void OpenRecentProject_Click(object sender, RoutedEventArgs e)
		{
			if (sender is System.Windows.Controls.MenuItem item && item.Tag is string folderPath)
			{
				OpenProjectByPath(folderPath);
			}
		}

		private void OpenProjectByPath(string folderPath)
		{
			_currentProjectFolder = folderPath;
			try {
				var (modName, author, replacedAssets, gameId, gamePath) = LoadProject(_currentProjectFolder);
				if (string.IsNullOrEmpty(gamePath) || !Directory.Exists(gamePath))
				{
					var msgBox = new ModdingTool.Windows.CustomMessageBox($"Game folder not found: {gamePath}\nPlease locate the game folder.", "Game Not Found", true);
					msgBox.Owner = this;
					msgBox.ShowDialog();
					if (msgBox.Result != true) return;
					var dialog = new Microsoft.WindowsAPICodePack.Dialogs.CommonOpenFileDialog();
					dialog.IsFolderPicker = true;
					dialog.Title = "Select game folder";
					if (dialog.ShowDialog() == Microsoft.WindowsAPICodePack.Dialogs.CommonFileDialogResult.Ok)
					{
						gamePath = dialog.FileName;
						var json = File.ReadAllText(Path.Combine(folderPath, "stage.json"));
						var project = System.Text.Json.JsonSerializer.Deserialize<ModProject>(json);
						project.GamePath = gamePath;
						File.WriteAllText(Path.Combine(folderPath, "stage.json"), System.Text.Json.JsonSerializer.Serialize(project, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
					}
					else return;
				}
				_currentModName = modName;
				_currentAuthor = author;
				_replacedAssets = replacedAssets;
				_gameId = gameId;
				_gamePath = gamePath;
				string tocPath = Path.Combine(gamePath, "toc");
				if (_lastLoadedTocPath == null || !_lastLoadedTocPath.Equals(tocPath, StringComparison.OrdinalIgnoreCase))
				{
					StartLoadTOCThread(tocPath);
					_lastLoadedTocPath = tocPath;
				}
				SetProjectDirty(false);
				UpdateWindowTitle();
				ShowAssetsFromFolder("");
				AddRecentProject(folderPath);
				ShowCustomMessageBox($"Loaded project: {modName}\nAuthor: {author}", "Open Project");
			} catch (Exception ex) {
				ShowCustomMessageBox($"Failed to load project: {ex.Message}", "Error");
			}
		}
		private List<int> GetAssetIndices(string key)
		{
			return _assetsByPath.ContainsKey(key) ? _assetsByPath[key] : new List<int>();
		}

		private void NewProject_Click(object sender, RoutedEventArgs e)
		{
			var (folder, modName, author) = ModdingTool.Utils.ProjectHelper.CreateNewProject(this);
			if (!string.IsNullOrEmpty(folder) && !string.IsNullOrEmpty(modName) && !string.IsNullOrEmpty(author))
			{
				OpenProjectByPath(folder);
			}
		}

		private void OpenProject_Click(object sender, RoutedEventArgs e)
		{
			var folder = ModdingTool.Utils.ProjectHelper.LoadProject(this);
			if (!string.IsNullOrEmpty(folder))
			{
				OpenProjectByPath(folder);
			}
		}

		private void ProjectSettings_Click(object sender, RoutedEventArgs e)
		{
			var prompt = new ModdingTool.Windows.ModInfoPrompt(_currentModName ?? "", _currentAuthor ?? "");
			prompt.Owner = this;
			if (prompt.ShowDialog() == true)
			{
				_currentModName = prompt.ModName;
				_currentAuthor = prompt.Author;
				SaveProjectIfLoaded();
				UpdateWindowTitle();
				ShowCustomMessageBox("Project info updated!", "Project Settings");
			}
		}
	}
}
