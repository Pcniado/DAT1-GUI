using MahApps.Metro.Controls;
using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.IO;
using System.Collections.Generic;
using System.Windows.Forms;
using ModdingTool.Utils;
using ModdingTool.Windows;
using System.Threading;

namespace ModdingTool.Windows
{
    public partial class WelcomeWindow : MetroWindow
    {
        private DispatcherTimer _timer;
        private double _hoursWasted = 0;
        private System.DateTime _startTime;
        private double _persistedHours = 0;

        public WelcomeWindow()
        {
            InitializeComponent();
            LoadHoursWasted();
            _startTime = System.DateTime.Now;
            _timer = new DispatcherTimer();
            _timer.Interval = System.TimeSpan.FromSeconds(1);
            _timer.Tick += Timer_Tick;
            _timer.Start();
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
                var mainWindow = new ModdingTool.MainWindow(folder, modName, author, true);
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

        private void OpenProjectFromFolder(string folder)
        {
            var mainWindow = new ModdingTool.MainWindow();
            mainWindow.Show();
            mainWindow.OpenProjectByPath(folder);
            this.Close();
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
                try
                {
                    var mainWindow = new ModdingTool.MainWindow(false);
                    mainWindow.Show();
                    mainWindow.StartLoadTOCThread(dialog.FileName);

                    this.Close();
                }
                catch (System.Exception ex)
                {
                    new CustomMessageBox($"Failed to load TOC: {ex.Message}", "Error").ShowDialog();
                }
            }
        }

        protected override void OnClosed(System.EventArgs e)
        {
            base.OnClosed(e);
            SaveHoursWasted();
        }
    }
} 