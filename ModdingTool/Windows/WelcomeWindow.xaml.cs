using MahApps.Metro.Controls;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.IO;
using System.Collections.Generic;
using System.Windows.Forms;
using ModdingTool.Utils;

namespace ModdingTool.Windows
{
    public partial class WelcomeWindow : MetroWindow
    {
        private List<string> _recentProjects = new();
        private DispatcherTimer _timer;
        private double _hoursWasted = 0;
        private System.DateTime _startTime;
        private double _persistedHours = 0;

        public WelcomeWindow()
        {
            InitializeComponent();
            LoadRecentProjects();
            LoadHoursWasted();
            _startTime = System.DateTime.Now;
            _timer = new DispatcherTimer();
            _timer.Interval = System.TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void LoadRecentProjects()
        {
            _recentProjects.Clear();
            var fn = "recent.txt";
            if (File.Exists(fn))
            {
                foreach (var line in File.ReadLines(fn))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    _recentProjects.Add(line.Trim());
                }
            }
            RecentProjectsList.ItemsSource = _recentProjects;
        }

        private void LoadHoursWasted()
        {
            var fn = "hours.txt";
            if (File.Exists(fn))
            {
                var txt = File.ReadAllText(fn);
                if (double.TryParse(txt, out var val))
                    _persistedHours = val;
            }
            else
            {
                _persistedHours = 0;
            }
        }

        private void SaveHoursWasted()
        {
            var fn = "hours.txt";
            var total = _persistedHours + (System.DateTime.Now - _startTime).TotalHours;
            File.WriteAllText(fn, total.ToString("F6"));
        }

        private void Timer_Tick(object sender, System.EventArgs e)
        {
            _hoursWasted = _persistedHours + (System.DateTime.Now - _startTime).TotalHours;
            HoursWastedText.Text = $"Hours wasted: {_hoursWasted:F2}";
        }

        private void NewProjectButton_Click(object sender, RoutedEventArgs e)
        {
            var (folder, modName, author) = ProjectHelper.CreateNewProject(this);
            if (!string.IsNullOrEmpty(folder) && !string.IsNullOrEmpty(modName) && !string.IsNullOrEmpty(author))
            {
                var mainWindow = new ModdingTool.MainWindow(folder, modName, author);
                mainWindow.Show();
                this.Close();
            }
        }

        private void LoadProjectButton_Click(object sender, RoutedEventArgs e)
        {
            var folder = ProjectHelper.LoadProject(this);
            if (!string.IsNullOrEmpty(folder))
            {
                OpenProjectFromFolder(folder);
            }
        }

        private void RecentProjectsList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (RecentProjectsList.SelectedItem is string path)
            {
                OpenProjectFromFolder(path);
            }
        }

        private void OpenProjectFromFolder(string folder)
        {
            var stageJson = Path.Combine(folder, "stage.json");
            if (File.Exists(stageJson))
            {
                try {
                    var json = File.ReadAllText(stageJson);
                    var doc = System.Text.Json.JsonDocument.Parse(json);
                    string modName = doc.RootElement.TryGetProperty("ModName", out var mn) ? mn.GetString() ?? "" : "";
                    string author = doc.RootElement.TryGetProperty("Author", out var au) ? au.GetString() ?? "" : "";
                    var mainWindow = new ModdingTool.MainWindow(folder, modName, author);
                    mainWindow.Show();
                    this.Close();
                } catch {}
            }
        }

        private void GitHubButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://github.com/pcniado/DAT1-GUI",
                UseShellExecute = true
            });
        }

        private void BrowseAssetsButton_Click(object sender, RoutedEventArgs e)
        {
            var mainWindow = new ModdingTool.MainWindow();
            mainWindow.Show();
            this.Close();
        }

        private void LoadTocButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog();
            dialog.Title = "Select TOC file";
            dialog.Filter = "TOC file (toc)|toc|All files (*.*)|*.*";
            if (dialog.ShowDialog() == true)
            {
                var mainWindow = new ModdingTool.MainWindow();
                mainWindow.Show();
                mainWindow.Dispatcher.InvokeAsync(() => {
                    var mi = mainWindow.GetType().GetMethod("StartLoadTOCThread", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
                    if (mi != null)
                        mi.Invoke(mainWindow, new object[] { dialog.FileName });
                });
                this.Close();
            }
        }

        protected override void OnClosed(System.EventArgs e)
        {
            base.OnClosed(e);
            SaveHoursWasted();
        }
    }
} 