using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using Microsoft.Win32;
using NAudio.Wave;
using MahApps.Metro.Controls;
using MahApps.Metro.IconPacks;
using NAudio.Wave.SampleProviders;

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
        private string _tempWem;
        private bool _isTempWem;

        public WemPlayerWindow(string wemPath, bool isTemp) : this()
        {
            _tempWem = wemPath;
            _isTempWem = isTemp;
            if (!string.IsNullOrEmpty(wemPath))
            {
                FileNameLabel.Text = System.IO.Path.GetFileName(wemPath);
                BrowseButton.Visibility = Visibility.Collapsed;
                ExportButton.Visibility = Visibility.Visible;
                ExportButton.IsEnabled = true;
                if (System.IO.File.Exists(wemPath))
                    PlayPause_Click(null, null);
            }
            else
            {
                FileNameLabel.Text = "";
                BrowseButton.Visibility = Visibility.Visible;
                ExportButton.Visibility = Visibility.Visible;
                ExportButton.IsEnabled = false;
            }
        }

        public WemPlayerWindow() 
        {
            InitializeComponent();
            _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
            _timer.Tick += Timer_Tick;
            FileNameLabel.Text = "";
            BrowseButton.Visibility = Visibility.Visible;
            ExportButton.Visibility = Visibility.Visible;
            ExportButton.IsEnabled = false;
        }

        private void Browse_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog { Filter = "Wwise WEM (*.wem)|*.wem|All files (*.*)|*.*" };
            if (dlg.ShowDialog() == true)
            {
                FileNameLabel.Text = System.IO.Path.GetFileName(dlg.FileName);
                ExportButton.IsEnabled = true;
                _tempWem = dlg.FileName;
                _isTempWem = false;
                PlayPause_Click(null, null);
            }
        }

        private void PlayPause_Click(object sender, RoutedEventArgs e)
        {
            if (_output == null || _reader == null)
            {
                StopPlayback();
                var wemPath = _tempWem;
#if DEBUG
                new CustomMessageBox($"WEM path: {wemPath}\nExists: {System.IO.File.Exists(wemPath)}", "DEBUG").ShowDialog();
#endif
                if (!System.IO.File.Exists(wemPath)) return;
                _tempWav = System.IO.Path.GetTempFileName() + ".wav";
#if DEBUG
                var exePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "3rdparty", "vgmstream-cli.exe");
                var vgmArgs = $"\"{wemPath}\" -o \"{_tempWav}\"";
                new CustomMessageBox($"vgmstream-cli: {exePath}\nArgs: {vgmArgs}", "DEBUG").ShowDialog();
#endif
                if (!RunVgmstreamStatic(wemPath, _tempWav))
                {
                    new CustomMessageBox("Failed to decode WEM file.", "Error").ShowDialog();
                    return;
                }
#if DEBUG
                new CustomMessageBox($"WAV created: {_tempWav}\nExists: {System.IO.File.Exists(_tempWav)}", "DEBUG").ShowDialog();
#endif
                _reader = new WaveFileReader(_tempWav);
#if DEBUG
                var fmt = _reader.WaveFormat;
                new CustomMessageBox($"WAV format: {fmt.Encoding}, {fmt.SampleRate} Hz, {fmt.BitsPerSample} bits, {fmt.Channels} ch", "DEBUG").ShowDialog();
#endif
                IWaveProvider provider;
                if (_reader.WaveFormat.Channels > 2)
                {
                    var sampleProvider = _reader.ToSampleProvider();
                    var stereoProvider = new StereoDownmixSampleProvider(sampleProvider);
                    provider = stereoProvider.ToWaveProvider();
                }
                else
                {
                    provider = WaveFormatConversionStream.CreatePcmStream(_reader);
                }
                _output = new WaveOutEvent();
                _output.Init(provider);
                _output.PlaybackStopped += Output_PlaybackStopped;
                _output.Play();
                _isPlaying = true;
                SeekBar.IsEnabled = true;
                SeekBar.Minimum = 0;
                SeekBar.Maximum = _reader.TotalTime.TotalSeconds;
                SeekBar.Value = 0;
                _timer.Start();
                SetPlayPauseIcon();
            }
            else if (_isPlaying)
            {
                _output.Pause();
                _isPlaying = false;
                _timer.Stop();
                SetPlayPauseIcon();
            }
            else
            {
                if (_reader != null && Math.Abs(_reader.CurrentTime.TotalSeconds - _reader.TotalTime.TotalSeconds) < 0.1)
                {
                    _reader.CurrentTime = TimeSpan.Zero;
                    SeekBar.Value = 0;
                }
                _output.Play();
                _isPlaying = true;
                _timer.Start();
                SetPlayPauseIcon();
            }
        }

        private void SetPlayPauseIcon()
        {
            if (PlayPauseIcon == null) return;
            PlayPauseIcon.Kind = _isPlaying ? PackIconMaterialKind.Pause : PackIconMaterialKind.Play;
        }

        public void Convert_Click(object sender, RoutedEventArgs e)
        {
            var wemPath = _tempWem;
            if (!System.IO.File.Exists(wemPath)) return;

            var dlg = new SaveFileDialog { Filter = "WAV files (*.wav)|*.wav" };
            if (dlg.ShowDialog() == true)
            {
                if (!RunVgmstreamStatic(wemPath, dlg.FileName))
                {
                    new CustomMessageBox("Failed to decode WEM file.", "Error").ShowDialog();
                }
                else
                {
                    new CustomMessageBox("Conversion complete.", "Info").ShowDialog();
                }
            }
        }

        public static bool RunVgmstreamStatic(string wemPath, string wavPath)
        {
            try
            {
                var exePath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "3rdparty", "vgmstream-cli.exe");
                if (!System.IO.File.Exists(exePath))
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
                string commandLine = $"{exePath} {psi.Arguments}";
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
#if DEBUG
                new CustomMessageBox($"Command: {commandLine}\nExit code: {exitCode}\nStdOut:\n{stdOut}\nStdErr:\n{stdErr}", "vgmstream-cli output").ShowDialog();
#endif
                if (!System.IO.File.Exists(wavPath))
                {
                    new CustomMessageBox($"vgmstream-cli.exe failed to produce output WAV.", "Error").ShowDialog();
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
            SeekBar.IsEnabled = true; // Keep seekbar enabled
            TimeCounter.Text = "00:00 / 00:00";
            if (!string.IsNullOrEmpty(_tempWav) && System.IO.File.Exists(_tempWav))
            {
                try { System.IO.File.Delete(_tempWav); } catch { }
                _tempWav = null;
            }
            // ExportButton.Visibility = System.Windows.Visibility.Collapsed; // Do not hide
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
                SeekBar.IsEnabled = true; // Keep seekbar enabled
                SetPlayPauseIcon();
                // ExportButton.Visibility = System.Windows.Visibility.Collapsed; // Do not hide
            });
        }

        private string FormatTime(double seconds)
        {
            var t = TimeSpan.FromSeconds(seconds);
            return t.ToString(t.Hours > 0 ? "hh\\:mm\\:ss" : "mm\\:ss");
        }

        protected override void OnClosed(EventArgs e)
        {
            base.OnClosed(e);
            if (_isTempWem && !string.IsNullOrEmpty(_tempWem) && System.IO.File.Exists(_tempWem))
            {
                try { System.IO.File.Delete(_tempWem); } catch { }
                _tempWem = null;
            }
            if (!string.IsNullOrEmpty(_tempWav) && System.IO.File.Exists(_tempWav))
            {
                try { System.IO.File.Delete(_tempWav); } catch { }
                _tempWav = null;
            }
            ExportButton.Visibility = System.Windows.Visibility.Collapsed;
        }

        // Add this class inside WemPlayerWindow
        public class StereoDownmixSampleProvider : ISampleProvider
        {
            private readonly ISampleProvider source;
            private readonly int inChannels;
            public WaveFormat WaveFormat => WaveFormat.CreateIeeeFloatWaveFormat(source.WaveFormat.SampleRate, 2);

            public StereoDownmixSampleProvider(ISampleProvider source)
            {
                this.source = source;
                this.inChannels = source.WaveFormat.Channels;
            }

            public int Read(float[] buffer, int offset, int count)
            {
                int framesRequested = count / 2;
                float[] temp = new float[framesRequested * inChannels];
                int samplesRead = source.Read(temp, 0, temp.Length);
                int framesRead = samplesRead / inChannels;
                int outIndex = offset;
                for (int n = 0; n < framesRead; n++)
                {
                    float left = 0, right = 0;
                    for (int ch = 0; ch < inChannels; ch++)
                    {
                        if (ch % 2 == 0) left += temp[n * inChannels + ch];
                        else right += temp[n * inChannels + ch];
                    }
                    buffer[outIndex++] = left / inChannels;
                    buffer[outIndex++] = right / inChannels;
                }
                return framesRead * 2;
            }
        }
    }
}