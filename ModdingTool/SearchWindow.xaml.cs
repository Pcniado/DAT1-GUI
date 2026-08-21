//
// This program is free software, and can be redistributed and/or modified by you. It is provided 'as-is', without any warranty.
// For more details, terms and conditions, see GNU General Public License.
// A copy of the that license should come with this program (LICENSE.txt). If not, see <http://www.gnu.org/licenses/>.

using System;
using ModdingTool.Structs;
using ModdingTool.Utils;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using MahApps.Metro.Controls;

namespace ModdingTool;

public partial class SearchWindow: MetroWindow {
	private List<Asset> _assets;
	private Dictionary<string, List<int>> _assetsByPath;
	private System.Action<string> _callback;
	private System.Action<string, System.Collections.IList> _contextMenuCallback;
	private ObservableCollection<SearchResult> _displayedResults = new();

	class SearchResult {
		public int AssetIndex { get; set; }
		public byte Span { get; set; }
		public ulong Id;
		public uint Size { get; set; }
		public string SizeFormatted { get => SizeFormat.FormatSize(Size); }

		public string Path { get; set; }
		public string Archive { get; set; }
		public string RefPath { get => $"{Span}/{Id:X016}"; }
	}

	private string? _gameId;

	public SearchWindow(List<Asset> assets, Dictionary<string, List<int>> assetsByPath, System.Action<string> callback, System.Action<string, System.Collections.IList> contextMenuCallback, string? gameId = null) {
		InitializeComponent();
		this.Activated += OnActivated;
		this.Deactivated += OnDeactivated;
		_assets = assets;
		_assetsByPath = assetsByPath;
		_callback = callback;
		_contextMenuCallback = contextMenuCallback;
		_gameId = gameId;

		CommandBindings.Add(new CommandBinding(AssetsListContextMenu.ExtractAssetCommand, ContextMenu_ExtractAsset));
		CommandBindings.Add(new CommandBinding(AssetsListContextMenu.ExtractAssetToStageCommand, ContextMenu_ExtractAssetToStage));
		CommandBindings.Add(new CommandBinding(AssetsListContextMenu.ReplaceAssetCommand, ContextMenu_ReplaceAsset));
		CommandBindings.Add(new CommandBinding(AssetsListContextMenu.ReplaceAssetsCommand, ContextMenu_ReplaceAssets));
		CommandBindings.Add(new CommandBinding(AssetsListContextMenu.CopyPathCommand, ContextMenu_CopyPath));
		CommandBindings.Add(new CommandBinding(AssetsListContextMenu.CopyRefCommand, ContextMenu_CopyRef));
		CommandBindings.Add(new CommandBinding(AssetsListContextMenu.EditConfigCommand, ContextMenu_EditConfig));
		CommandBindings.Add(new CommandBinding(AssetsListContextMenu.PlayWemCommand, ContextMenu_PlayWem));
		CommandBindings.Add(new CommandBinding(AssetsListContextMenu.ExportWemToWavCommand, ContextMenu_ExportWemToWav));
		CommandBindings.Add(new CommandBinding(AssetsListContextMenu.ViewTextureCommand, ContextMenu_ViewTexture));
		CommandBindings.Add(new CommandBinding(AssetsListContextMenu.ExportTextureCommand, ContextMenu_ExportTexture));

//populate asset type
		var types = new HashSet<string>(_assets.ConvertAll(a => a.AssetType));
		var typeList = new List<string>(types);
		typeList.Sort();
		typeList.Insert(0, "All");
		AssetTypeComboBox.ItemsSource = typeList;
		AssetTypeComboBox.SelectedIndex = 0;
		AssetTypeComboBox.SelectionChanged += (s, e) => Search();

		SearchTextBox.Text = "";
		Search();
	}

	private void OnActivated(object sender, EventArgs e) {
		this.WindowTitleBrush = (System.Windows.Media.LinearGradientBrush)FindResource("AppTitleBarGradient");
	}

	private void OnDeactivated(object sender, EventArgs e) {
		this.WindowTitleBrush = (System.Windows.Media.LinearGradientBrush)FindResource("AppTitleBarGradient");
	}

	private void SearchTextBox_KeyUp(object sender, KeyEventArgs e) {
		if (e.Key == Key.Enter) {
			Search();
		}
	}

	private void SearchButton_Click(object sender, RoutedEventArgs e) {
		Search();
	}

	private void SearchResults_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
		if (SearchResults.SelectedItems.Count != 1) return;
		if (SearchResults.SelectedItem == null) return;

		_callback((SearchResults.SelectedItem as SearchResult).RefPath);
	}

private void Search() {
		_displayedResults.Clear();

		var search = Normalize(SearchTextBox.Text.Trim());
		var words = search.Split(' ', System.StringSplitOptions.RemoveEmptyEntries);
		string selectedType = AssetTypeComboBox.SelectedItem as string ?? "All";

		if (words.Length > 0 || selectedType != "All") {
			// search in fullpath
			var i = 0;
			foreach (var asset in _assets) {
				if (selectedType != "All" && asset.AssetType != selectedType) {
					++i;
					continue;
				}

				if (asset.FullPath != null) {
					var hexId = asset.Id.ToString("X016").ToLowerInvariant();
					// Include normalized path, 16-character hex ID, 0x hex ID, and ref path (e.g. 0/a5a7d5bf77c9fbe3)
					var searchableText = $"{Normalize(asset.FullPath)} {hexId} 0x{hexId} {asset.Span}/{hexId}";

					if (MatchesWords(searchableText, words)) {
						_displayedResults.Add(new SearchResult {
							AssetIndex = i,
							Span = asset.Span,
							Id = asset.Id,
							Size = asset.Size,
							Path = asset.FullPath,
							Archive = asset.Archive
						});
					}
				}
				++i;
			}

			// search in fake paths (dirname + name)
			foreach (var path in _assetsByPath.Keys) {
				foreach (var assetIndex in _assetsByPath[path]) {
					var asset = _assets[assetIndex];
					if (asset.FullPath != null) continue;
					if (selectedType != "All" && asset.AssetType != selectedType) continue;

					var fakepath = System.IO.Path.Combine(path, asset.Name);
					var hexId = asset.Id.ToString("X016").ToLowerInvariant();
					var searchableText = $"{Normalize(fakepath)} {hexId} 0x{hexId} {asset.Span}/{hexId}";

					if (MatchesWords(searchableText, words)) {
						_displayedResults.Add(new SearchResult {
							AssetIndex = assetIndex,
							Span = asset.Span,
							Id = asset.Id,
							Size = asset.Size,
							Path = fakepath,
							Archive = asset.Archive
						});
					}
				}
			}
		}

		ResultsCount.Text = $"{_displayedResults.Count} results";
		SearchResults.ItemsSource = _displayedResults;
	}
	
    private static string Normalize(string text) {
		return text.Replace('\\', '/').ToLower();
	}

	private static bool MatchesWords(string path, IEnumerable<string> words) {
		foreach (var word in words) {
			if (!path.Contains(word)) return false;
		}
		return true;
	}

	#region context menu

	private void SearchResults_ContextMenuOpening(object sender, ContextMenuEventArgs e) {
		var selected = SearchResults.SelectedItems.Count;
		AssetsListContextMenu.HandleContextMenuOpening(sender, e, selected);
		
		if (selected == 1 && SearchResults.SelectedItem is SearchResult result)
		{
			// Handle config files
			if (result.Path?.EndsWith(".config", System.StringComparison.OrdinalIgnoreCase) ?? false)
			{
				bool isI29OrHigher = IsGameVersionI29OrHigher();
				AssetsListContextMenu.EditConfig.Visibility = Visibility.Visible;
				AssetsListContextMenu.EditConfig.IsEnabled = isI29OrHigher;
			}
			else
			{
				AssetsListContextMenu.EditConfig.Visibility = Visibility.Collapsed;
			}
			

			bool isWem = result.Path?.EndsWith(".wem", System.StringComparison.OrdinalIgnoreCase) ?? false;
			if (isWem)
			{
				AssetsListContextMenu.PlayWem.Visibility = Visibility.Visible;
				AssetsListContextMenu.ExportWemToWav.Visibility = Visibility.Visible;
				AssetsListContextMenu.PlayWem.IsEnabled = true;
				AssetsListContextMenu.ExportWemToWav.IsEnabled = true;
			}
			else
			{
				AssetsListContextMenu.PlayWem.Visibility = Visibility.Collapsed;
				AssetsListContextMenu.ExportWemToWav.Visibility = Visibility.Collapsed;
			}
			
			// Handle texture files
			bool isTexture = result.Path?.EndsWith(".texture", System.StringComparison.OrdinalIgnoreCase) ?? false;
			if (isTexture)
			{
				bool textureSupported = IsTextureViewerSupported();
				AssetsListContextMenu.ViewTexture.Visibility = Visibility.Visible;
				AssetsListContextMenu.ExportTexture.Visibility = Visibility.Visible;
				AssetsListContextMenu.ViewTexture.IsEnabled = textureSupported;
				AssetsListContextMenu.ExportTexture.IsEnabled = textureSupported;
			}
			else
			{
				AssetsListContextMenu.ViewTexture.Visibility = Visibility.Collapsed;
				AssetsListContextMenu.ExportTexture.Visibility = Visibility.Collapsed;
			}
		}
		else
		{
			AssetsListContextMenu.EditConfig.Visibility = Visibility.Collapsed;
			AssetsListContextMenu.PlayWem.Visibility = Visibility.Collapsed;
			AssetsListContextMenu.ExportWemToWav.Visibility = Visibility.Collapsed;
			AssetsListContextMenu.ViewTexture.Visibility = Visibility.Collapsed;
			AssetsListContextMenu.ExportTexture.Visibility = Visibility.Collapsed;
		}
	}

	private bool IsGameVersionI29OrHigher()
	{// we do not include i31 because its closer to i20 than i30
		if (string.IsNullOrEmpty(_gameId)) return false;
		return _gameId == "i29" || _gameId == "i30" || _gameId == "i33" || _gameId == "MSM2" || _gameId == "RCRA";
	}

	// texture viewer supports legacy games too now
	private bool IsTextureViewerSupported()
	{
		if (string.IsNullOrEmpty(_gameId)) return false;
		return _gameId == "i29" || _gameId == "i30" || _gameId == "i33" || _gameId == "MSM2" || _gameId == "RCRA" || _gameId == "MSMR" || _gameId == "MM";
	}

	private bool IsGameVersionI30()
	{
		return _gameId == "i30" || _gameId == "MSM2";
	}

	private void ContextMenu_ExtractAsset(object sender, ExecutedRoutedEventArgs e) {
		_contextMenuCallback("ExtractAsset", GetSelectedAssets());
	}

	private void ContextMenu_ExtractAssetToStage(object sender, ExecutedRoutedEventArgs e) {
		_contextMenuCallback("ExtractAssetToStage", GetSelectedAssets());
	}

	private void ContextMenu_ReplaceAsset(object sender, ExecutedRoutedEventArgs e) {
		_contextMenuCallback("ReplaceAsset", GetSelectedAssets());
	}

	private void ContextMenu_ReplaceAssets(object sender, ExecutedRoutedEventArgs e) {
		_contextMenuCallback("ReplaceAssets", GetSelectedAssets());
	}

	private void ContextMenu_CopyPath(object sender, ExecutedRoutedEventArgs e) {
		_contextMenuCallback("CopyPath", GetSelectedAssets());
	}

	private void ContextMenu_CopyRef(object sender, ExecutedRoutedEventArgs e) {
		_contextMenuCallback("CopyRef", GetSelectedAssets());
	}

	private void ContextMenu_EditConfig(object sender, ExecutedRoutedEventArgs e) {
		_contextMenuCallback("EditConfig", GetSelectedAssets());
	}

	private void ContextMenu_PlayWem(object sender, ExecutedRoutedEventArgs e) {
		_contextMenuCallback("PlayWem", GetSelectedAssets());
	}

	private void ContextMenu_ExportWemToWav(object sender, ExecutedRoutedEventArgs e) {
		_contextMenuCallback("ExportWemToWav", GetSelectedAssets());
	}

	private void ContextMenu_ViewTexture(object sender, ExecutedRoutedEventArgs e) {
		_contextMenuCallback("ViewTexture", GetSelectedAssets());
	}

	private void ContextMenu_ExportTexture(object sender, ExecutedRoutedEventArgs e) {
		_contextMenuCallback("ExportTexture", GetSelectedAssets());
	}

	private List<Asset> GetSelectedAssets() {
		var result = new List<Asset>();
		foreach (var item in SearchResults.SelectedItems) {
			result.Add(_assets[(item as SearchResult).AssetIndex]);
		}
		return result;
	}

	#endregion

	// Helper to format Name/ID for WEMs and others
	private string GetNameId(Asset asset) {
		// WEM heuristic: high bits of ID
		bool isWem = (asset.Id & 0xFFFFFFFF00000000) == 0xE000000000000000;
		if (isWem) {
			string wemName = asset.Name ?? "(unknown)";
			return $"{wemName} [{asset.Id:X016}]";
		} else {
			string name = asset.Name ?? asset.FullPath ?? "(unknown)";
			return $"{name} [{asset.Id:X016}]";
		}
	}
}
