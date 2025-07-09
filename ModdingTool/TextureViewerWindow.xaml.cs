using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Pfim;
using OverstrikeShared.STG.Files;
using DAT1.Files;
using System.Windows.Input;

namespace ModdingTool.Windows
{
    public partial class TextureViewerWindow : Window
    {
        private string currentTexturePath = "";

        private double _zoom = 1.0;
        private const double ZoomStep = 0.1;
        private const double ZoomMin = 0.1;
        private const double ZoomMax = 10.0;
        private Point? _lastDragPoint;
        private double _originalImageWidth = 0;
        private double _originalImageHeight = 0;

        public TextureViewerWindow()
        {
            InitializeComponent();
            TextureImage.MouseLeftButtonDown += TextureImage_MouseButtonDown;
            TextureImage.MouseLeftButtonUp += TextureImage_MouseButtonUp;
            TextureImage.MouseMove += TextureImage_MouseMove;
            TextureImage.MouseLeave += TextureImage_MouseLeave;
            this.PreviewKeyDown += TextureViewerWindow_PreviewKeyDown;
            this.PreviewMouseWheel += TextureViewerWindow_PreviewMouseWheel;
            TextureImage.Focusable = true;
            TextureImage.Focus();
        }

        private void TextureImage_MouseButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                var sv = ImageScrollViewer;
                _lastDragPoint = e.GetPosition(sv);
                TextureImage.CaptureMouse();
            }
        }

        private void TextureImage_MouseButtonUp(object sender, MouseButtonEventArgs e)
        {
            _lastDragPoint = null;
            TextureImage.ReleaseMouseCapture();
        }

        private void TextureImage_MouseMove(object sender, MouseEventArgs e)
        {
            if (_lastDragPoint.HasValue && e.LeftButton == MouseButtonState.Pressed)
            {
                var sv = ImageScrollViewer;
                Point posNow = e.GetPosition(sv);
                double dX = posNow.X - _lastDragPoint.Value.X;
                double dY = posNow.Y - _lastDragPoint.Value.Y;
                sv.ScrollToHorizontalOffset(sv.HorizontalOffset - dX);
                sv.ScrollToVerticalOffset(sv.VerticalOffset - dY);
                _lastDragPoint = posNow;
            }
        }

        private void TextureImage_MouseLeave(object sender, MouseEventArgs e)
        {
            _lastDragPoint = null;
            TextureImage.ReleaseMouseCapture();
        }

        private void TextureViewerWindow_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                if (e.Key == Key.Add || e.Key == Key.OemPlus)
                {
                    SetZoom(_zoom + ZoomStep);
                    e.Handled = true;
                }
                else if (e.Key == Key.Subtract || e.Key == Key.OemMinus)
                {
                    SetZoom(_zoom - ZoomStep);
                    e.Handled = true;
                }
                else if (e.Key == Key.Up)
                {
                    ImageScrollViewer.LineUp();
                    e.Handled = true;
                }
                else if (e.Key == Key.Down)
                {
                    ImageScrollViewer.LineDown();
                    e.Handled = true;
                }
                else if (e.Key == Key.Left)
                {
                    ImageScrollViewer.LineLeft();
                    e.Handled = true;
                }
                else if (e.Key == Key.Right)
                {
                    ImageScrollViewer.LineRight();
                    e.Handled = true;
                }
            }
        }

        private void TextureViewerWindow_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
                double zoomDelta = e.Delta > 0 ? ZoomStep : -ZoomStep;
                SetZoom(_zoom + zoomDelta);
                e.Handled = true;
            }
        }

        private void SetZoom(double zoom)
        {
            _zoom = Math.Max(ZoomMin, Math.Min(ZoomMax, zoom));
            if (_originalImageWidth > 0 && _originalImageHeight > 0)
            {
                TextureImage.Width = _originalImageWidth * _zoom;
                TextureImage.Height = _originalImageHeight * _zoom;
            }
        }

        private void OnBrowseTexture_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new OpenFileDialog
            {
                Filter = "Textures (.dds, .tga, .texture)|*.dds;*.tga;*.texture"
            };

            if (dlg.ShowDialog() == true)
            {
                string selectedPath = dlg.FileName;
                string ext = Path.GetExtension(selectedPath).ToLowerInvariant();
                if (ext == ".texture")
                {
                    string dir = Path.GetDirectoryName(selectedPath);
                    string baseName = Path.GetFileNameWithoutExtension(selectedPath);
                    // If user picked .hd.texture, baseName will be 'name.hd', so also check for .texture
                    string hdPath = Path.Combine(dir, baseName + ".hd.texture");
                    string sdBaseName = baseName.EndsWith(".hd", StringComparison.OrdinalIgnoreCase) ? baseName.Substring(0, baseName.Length - 3) : baseName;
                    string sdPath = Path.Combine(dir, sdBaseName + ".texture");
                    bool hdExists = File.Exists(hdPath);
                    bool sdExists = File.Exists(sdPath);

                    if (hdExists && sdExists)
                    {
                        var result = MessageBox.Show(
                            $"Both SD and HD textures were found:\nSD: {Path.GetFileName(sdPath)}\nHD: {Path.GetFileName(hdPath)}\n\nDo you want to view the HD texture instead?\n(Yes = HD, No = SD)",
                            "SD/HD Texture Found",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question
                        );
                        if (result == MessageBoxResult.Yes)
                        {
                            LoadTexture(hdPath);
                            return;
                        }
                        else
                        {
                            LoadTexture(sdPath);
                            return;
                        }
                    }
                    else if (hdExists)
                    {
                        LoadTexture(hdPath);
                        return;
                    }
                    else if (sdExists)
                    {
                        LoadTexture(sdPath);
                        return;
                    }
                }
                LoadTexture(selectedPath);
            }
        }

        private void LoadTexture(string path)
        {
            string ext = Path.GetExtension(path).ToLowerInvariant();
            string typeLabel = "";
            if (ext == ".texture")
            {
                if (path.EndsWith(".hd.texture", StringComparison.OrdinalIgnoreCase))
                    typeLabel = "HD";
                else
                    typeLabel = "SD";
            }

            // ✅ Block unsupported formats like .texture
            if (ext != ".dds" && ext != ".tga" && ext != ".texture")
            {
                MessageBox.Show(
                    "This viewer currently supports only .dds and .tga files.",
                    "Unsupported Format",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
                return;
            }

            try
            {
                IImage image;
                if (ext == ".texture")
                {
                    // Extract DDS from .texture using OverstrikeShared.STG.Files.Texture As I Tried To Understand It Lmao 
                    byte[] ddsData;
                    using (var fs = File.OpenRead(path))
                    using (var br = new BinaryReader(fs))
                    {
                        long start = br.BaseStream.Position;
                        uint magic = br.ReadUInt32();
                        br.BaseStream.Position = start; // Reset position

                        if (magic == Texture_I20.MAGIC)
                            ddsData = new Texture_I20(br).GetDDS();
                        else if (magic == Texture_I29.MAGIC)
                            ddsData = new Texture_I29(br).GetDDS();
                        else if (magic == Texture_I30.MAGIC)
                            ddsData = new Texture_I30(br).GetDDS();
                        else
                        {
                            // Not a DAT1 texture, try to load as plain DDS
                            br.BaseStream.Position = 0;
                            ddsData = br.ReadBytes((int)br.BaseStream.Length);
                        }
                    }

                    if (ddsData.Length < 4 || ddsData[0] != 0x44 || ddsData[1] != 0x44 || ddsData[2] != 0x53 || ddsData[3] != 0x20)
                    {
                        MessageBox.Show("This file does not contain a valid DDS header.", "Load Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }

                    using var ms = new MemoryStream(ddsData);
                    image = Pfimage.FromStream(ms);
                }
                else
                {
                    image = Pfimage.FromFile(path);
                }

                PixelFormat format = image.Format switch
                {
                    ImageFormat.Rgb24 => PixelFormats.Bgr24,
                    ImageFormat.Rgba32 => PixelFormats.Bgra32,
                    _ => throw new NotSupportedException($"Unsupported format: {image.Format}")
                };

                var bmp = BitmapSource.Create(
                    image.Width, image.Height,
                    96, 96,
                    format, null,
                    image.Data, image.Stride);

                TextureImage.Source = bmp;
                currentTexturePath = path;
                _originalImageWidth = bmp.PixelWidth;
                _originalImageHeight = bmp.PixelHeight;
                _zoom = 1.0;
                TextureImage.Width = double.NaN;
                TextureImage.Height = double.NaN;
                StatusText.Text = $"Loaded: {Path.GetFileName(path)} ({image.Width}x{image.Height})" + (string.IsNullOrEmpty(typeLabel) ? "" : $" [{typeLabel}]");
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"This file could not be loaded:\n\n{ex.Message}",
                    "Load Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning
                );
            }
        }

        private void OnExportPng_Click(object sender, RoutedEventArgs e)
        {
            if (TextureImage.Source is BitmapSource bmp)
            {
                var dlg = new SaveFileDialog
                {
                    Filter = "PNG File|*.png",
                    FileName = Path.GetFileNameWithoutExtension(currentTexturePath) + ".png"
                };

                if (dlg.ShowDialog() == true)
                {
                    using var stream = new FileStream(dlg.FileName, FileMode.Create);
                    var encoder = new PngBitmapEncoder();
                    encoder.Frames.Add(BitmapFrame.Create(bmp));
                    encoder.Save(stream);
                    StatusText.Text = $"Exported PNG to {dlg.FileName}";
                }
            }
        }

        private void OnImportPng_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "Import/replace functionality is not yet implemented.",
                "Texture Viewer",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }
    }
}
