using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using NAudio.Wave;
using MahApps.Metro.Controls;

namespace ModdingTool.Windows
{
    public partial class WemPlayerWindow : MetroWindow
    {
        private WaveOutEvent _output;
        private WaveFileReader _reader;
        private string _tempWav;
        private DispatcherTimer _timer;
        private bool _isDragging;
        private bool _isPlaying;

        public WemPlayerWindow(string wemPath) : this()
        {
            if (!string.IsNullOrEmpty(wemPath))
            {
                FilePathBox.Text = wemPath;
                FilePathBox.Visibility = Visibility.Collapsed;
                BrowseButton.Visibility = Visibility.Collapsed;
                if (File.Exists(wemPath))
                    Play_Click(null, null);
            }
            else
            {
                FilePathBox.Visibility = Visibility.Visible;
                BrowseButton.Visibility = Visibility.Visible;
            }
        }

        public WemPlayerWindow() // default for Tools menu
        {
            InitializeComponent();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _timer.Tick += Timer_Tick;
            FilePathBox.Visibility = Visibility.Visible;
            BrowseButton.Visibility = Visibility.Visible;
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "Wwise WEM (*.wem)|*.wem|All files (*.*)|*.*" };
            if (dlg.ShowDialog() == true)
            {
                FilePathBox.Text = dlg.FileName;
            }
        }

        private void Play_Click(object sender, RoutedEventArgs e)
        {
            StopPlayback();
            var wemPath = FilePathBox.Text;
#if DEBUG
            new CustomMessageBox($"WEM path: {wemPath}\nExists: {File.Exists(wemPath)}", "DEBUG").ShowDialog();
#endif
            if (!File.Exists(wemPath)) return;

            _tempWav = Path.GetTempFileName() + ".wav";
#if DEBUG
            var exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "3rdparty", "vgmstream-cli.exe");
            var vgmArgs = $"\"{wemPath}\" -o \"{_tempWav}\"";
            new CustomMessageBox($"vgmstream-cli: {exePath}\nArgs: {vgmArgs}", "DEBUG").ShowDialog();
#endif
            if (!RunVgmstream(wemPath, _tempWav))
            {
                new CustomMessageBox("Failed to decode WEM file.", "Error").ShowDialog();
                return;
            }
#if DEBUG
            new CustomMessageBox($"WAV created: {_tempWav}\nExists: {File.Exists(_tempWav)}", "DEBUG").ShowDialog();
#endif
            _reader = new WaveFileReader(_tempWav);
            _output = new WaveOutEvent();
            _output.Init(_reader);
            _output.PlaybackStopped += Output_PlaybackStopped;
            _output.Play();
            _isPlaying = true;
            SeekBar.IsEnabled = true;
            SeekBar.Minimum = 0;
            SeekBar.Maximum = _reader.TotalTime.TotalSeconds;
            SeekBar.Value = 0;
            _timer.Start();
        }

        private void Stop_Click(object sender, RoutedEventArgs e)
        {
            StopPlayback();
        }

        public void Convert_Click(object sender, RoutedEventArgs e)
        {
            var wemPath = FilePathBox.Text;
            if (!File.Exists(wemPath)) return;

            var dlg = new SaveFileDialog { Filter = "WAV files (*.wav)|*.wav" };
            if (dlg.ShowDialog() == true)
            {
                if (!RunVgmstream(wemPath, dlg.FileName))
                {
                    new CustomMessageBox("Failed to decode WEM file.", "Error").ShowDialog();
                }
                else
                {
                    new CustomMessageBox("Conversion complete.", "Info").ShowDialog();
                }
            }
        }

        private bool RunVgmstream(string wemPath, string wavPath)
        {
            try
            {
                var exePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "3rdparty", "vgmstream-cli.exe");
                if (!File.Exists(exePath))
                {
                    new CustomMessageBox($"vgmstream-cli.exe not found in build output 3rdparty folder: {exePath}", "Error").ShowDialog();
                    return false;
                }
                var psi = new ProcessStartInfo
                {
                    FileName = exePath,
                    Arguments = $"\"{wemPath}\" -o \"{wavPath}\"",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardError = true,
                    RedirectStandardOutput = true
                };
                string stdOut = string.Empty;
                string stdErr = string.Empty;
                int exitCode = -1;
                try
                {
                    using (var proc = Process.Start(psi))
                    {
                        stdOut = proc.StandardOutput.ReadToEnd();
                        stdErr = proc.StandardError.ReadToEnd();
                        proc.WaitForExit();
                        exitCode = proc.ExitCode;
                    }
                }
                catch (Exception ex)
                {
                    new CustomMessageBox($"Failed to start vgmstream-cli.exe:\n{ex}", "Error").ShowDialog();
                    return false;
                }
                if (!File.Exists(wavPath))
                {
                    new CustomMessageBox($"vgmstream-cli.exe failed.\nExit code: {exitCode}\nStdOut: {stdOut}\nStdErr: {stdErr}", "Error").ShowDialog();
                    return false;
                }
                return true;
            }
            catch (Exception ex)
            {
                new CustomMessageBox($"Exception in RunVgmstream: {ex}", "Error").ShowDialog();
                return false;
            }
        }

        private void StopPlayback()
        {
            _timer.Stop();
            _output?.Stop();
            _output?.Dispose();
            _output = null;
            _reader?.Dispose();
            _reader = null;
            _isPlaying = false;
            SeekBar.Value = 0;
            SeekBar.IsEnabled = false;
            TimeCounter.Text = "00:00 / 00:00";
            if (!string.IsNullOrEmpty(_tempWav) && File.Exists(_tempWav))
            {
                try { File.Delete(_tempWav); } catch { }
                _tempWav = null;
            }
        }

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (_reader == null || _isDragging) return;
            var pos = _reader.CurrentTime.TotalSeconds;
            var len = _reader.TotalTime.TotalSeconds;
            SeekBar.Value = pos;
            TimeCounter.Text = $"{FormatTime(pos)} / {FormatTime(len)}";
        }

        private void SeekBar_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_reader == null || !_isDragging) return;
            var newTime = TimeSpan.FromSeconds(SeekBar.Value);
            _reader.CurrentTime = newTime;
            TimeCounter.Text = $"{FormatTime(_reader.CurrentTime.TotalSeconds)} / {FormatTime(_reader.TotalTime.TotalSeconds)}";
        }

        private void SeekBar_PreviewMouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            _isDragging = true;
        }

        private void SeekBar_PreviewMouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            if (_reader != null)
            {
                var newTime = TimeSpan.FromSeconds(SeekBar.Value);
                _reader.CurrentTime = newTime;
            }
            _isDragging = false;
        }

        private void Output_PlaybackStopped(object sender, StoppedEventArgs e)
        {
            Dispatcher.Invoke(() =>
            {
                _isPlaying = false;
                SeekBar.Value = SeekBar.Maximum;
                TimeCounter.Text = $"{FormatTime(SeekBar.Maximum)} / {FormatTime(SeekBar.Maximum)}";
            });
        }

        private string FormatTime(double seconds)
        {
            var t = TimeSpan.FromSeconds(seconds);
            return t.ToString(t.Hours > 0 ? "hh\\:mm\\:ss" : "mm\\:ss");
        }

        protected override void OnClosed(EventArgs e)
        {
            StopPlayback();
            base.OnClosed(e);
        }
    }
}