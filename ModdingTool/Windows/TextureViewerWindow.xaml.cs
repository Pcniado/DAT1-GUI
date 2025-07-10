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
using MahApps.Metro.Controls;
using System.Text;
using System.Linq;
using System.Windows.Threading;

namespace ModdingTool.Windows
{
    public partial class TextureViewerWindow : MetroWindow
    {
        private string currentTexturePath = "";

        private double _zoom = 1.0;
        private const double ZoomStep = 0.1;
        private const double ZoomMin = 0.1;
        private const double ZoomMax = 10.0;
        private Point? _lastDragPoint;
        private double _originalImageWidth = 0;
        private double _originalImageHeight = 0;
        private BitmapSource _originalBitmapSource = null;
        private byte[] _currentDdsData = null;
        private bool _showImportButton = true;

        public TextureViewerWindow(bool showImportButton = true)
        {
            InitializeComponent();
            _showImportButton = showImportButton;
            TextureImage.MouseLeftButtonDown += TextureImage_MouseButtonDown;
            TextureImage.MouseLeftButtonUp += TextureImage_MouseButtonUp;
            TextureImage.MouseMove += TextureImage_MouseMove;
            TextureImage.MouseLeave += TextureImage_MouseLeave;
            this.PreviewKeyDown += TextureViewerWindow_PreviewKeyDown;
            this.PreviewMouseWheel += TextureViewerWindow_PreviewMouseWheel;
            TextureImage.Focusable = true;
            TextureImage.Focus();
            ImportTextureButton.Visibility = _showImportButton ? Visibility.Visible : Visibility.Collapsed;
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
                Filter = "Textures (.dds, .tga, .texture, .hd.texture)|*.dds;*.tga;*.texture;*.hd.texture"
            };

            if (dlg.ShowDialog() == true)
            {
                string selectedPath = dlg.FileName;
                string ext = Path.GetExtension(selectedPath).ToLowerInvariant();
                // Remove SD/HD prompt: always auto-select best available
                LoadTexture(selectedPath);
            }
        }

        public void LoadTexture(string filePath)
        {
            string ext = Path.GetExtension(filePath).ToLowerInvariant();
            bool isTexture = ext == ".texture" || filePath.EndsWith(".hd.texture", StringComparison.OrdinalIgnoreCase);
            if (!isTexture && ext != ".dds" && ext != ".tga")
            {
                MessageBox.Show(
                    "This viewer currently supports only .dds, .tga, .texture, and .hd.texture files.",
                    "Unsupported Format",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information
                );
                return;
            }

            IImage image = null;
            string formatString = "-";
            int mipmaps = 1;
            int width = 0, height = 0;
            string dxgiName = "-";
            byte[] ddsData = null;
            bool triedDDSExport = false;
            try
            {
                if (isTexture)
                {
                    string headerPath = filePath;
                    string hdPath = null;
                    if (filePath.EndsWith(".hd.texture", StringComparison.OrdinalIgnoreCase))
                    {
                        // If opening .hd.texture, use the corresponding .texture for header if it exists
                        headerPath = filePath.Substring(0, filePath.Length - ".hd.texture".Length) + ".texture";
                        hdPath = filePath;
                    }
                    // Extract info using robust logic
                    var info = TextureFileParser.ExtractTextureInfo(headerPath);
                    if (info != null)
                    {
                        width = info.Width;
                        height = info.Height;
                        mipmaps = info.Mipmaps;
                        dxgiName = TextureFileParser.GetDXGIFormatName(info.Format);
                        formatString = dxgiName;
                    }
                    int spanIdx;
                    byte[] ddsBuffer = null;
                    if (hdPath != null && File.Exists(headerPath))
                    {
                        // Use SD header + HD data
                        ddsBuffer = TextureFileParser.GetBestDDS(headerPath, out spanIdx);
                    }
                    else
                    {
                        ddsBuffer = TextureFileParser.GetBestDDS(filePath, out spanIdx);
                    }
                    ddsData = ddsBuffer;
                    _currentDdsData = ddsData;
                    if (ddsData == null)
                        throw new Exception("Could not extract DDS from .texture");
                    try
                    {
                        using var ms = new MemoryStream(ddsData);
                        image = Pfim.Pfimage.FromStream(ms);
                        // If info extraction failed, fallback to Pfim info
                        if (width == 0 || height == 0)
                        {
                            width = image.Width;
                            height = image.Height;
                        }
                        if (mipmaps == 1 && image.MipMaps != null)
                            mipmaps = image.MipMaps.Length;
                        if (formatString == "-" || formatString.Contains("Unknown"))
                            formatString = image.Format.ToString();
                    }
                    catch (Exception ex)
                    {
                        if (!triedDDSExport)
                        {
                            triedDDSExport = true;
                            if (MessageBox.Show($"Failed to decode DDS: {ex.Message}\nWould you like to export the DDS for external viewing?", "Error", MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.Yes)
                            {
                                InsomniacTextureDecoder.SaveDDSForExternalTool(ddsData);
                            }
                        }
                        StatusText.Text = $"Failed to decode DDS: {ex.Message}";
                        return;
                    }
                }
                else if (ext == ".dds")
                {
                    try
                    {
                        image = Pfim.Pfimage.FromFile(filePath);
                        width = image.Width;
                        height = image.Height;
                        mipmaps = image.MipMaps?.Length ?? 1;
                        formatString = image.Format.ToString();
                        dxgiName = formatString;
                    }
                    catch (Exception ex)
                    {
                        if (!triedDDSExport)
                        {
                            triedDDSExport = true;
                            byte[] fileBytes = File.ReadAllBytes(filePath);
                            if (MessageBox.Show($"Failed to decode DDS: {ex.Message}\nWould you like to export the DDS for external viewing?", "Error", MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.Yes)
                            {
                                InsomniacTextureDecoder.SaveDDSForExternalTool(fileBytes);
                            }
                        }
                        StatusText.Text = $"Failed to decode DDS: {ex.Message}";
                        return;
                    }
                }
                else // .tga
                {
                    try
                    {
                        image = Pfim.Pfimage.FromFile(filePath);
                        width = image.Width;
                        height = image.Height;
                        mipmaps = image.MipMaps?.Length ?? 1;
                        formatString = image.Format.ToString();
                        dxgiName = formatString;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show($"This file could not be loaded:\n\n{ex.Message}", "Load Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                PixelFormat format;
                try
                {
                    format = image.Format switch
                    {
                        ImageFormat.Rgb24 => PixelFormats.Bgr24,
                        ImageFormat.Rgba32 => PixelFormats.Bgra32,
                        _ => throw new Exception($"Unsupported format: {image.Format}")
                    };
                }
                catch (Exception ex)
                {
                    if (!triedDDSExport && ddsData != null)
                    {
                        triedDDSExport = true;
                        if (MessageBox.Show($"Failed to decode DDS: {ex.Message}\nWould you like to export the DDS for external viewing?", "Error", MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.Yes)
                        {
                            InsomniacTextureDecoder.SaveDDSForExternalTool(ddsData);
                        }
                        StatusText.Text = $"Failed to decode DDS: {ex.Message}";
                        return;
                    }
                    else if (!triedDDSExport && ext == ".dds")
                    {
                        triedDDSExport = true;
                        byte[] fileBytes = File.ReadAllBytes(filePath);
                        if (MessageBox.Show($"Failed to decode DDS: {ex.Message}\nWould you like to export the DDS for external viewing?", "Error", MessageBoxButton.YesNo, MessageBoxImage.Error) == MessageBoxResult.Yes)
                        {
                            InsomniacTextureDecoder.SaveDDSForExternalTool(fileBytes);
                        }
                        StatusText.Text = $"Failed to decode DDS: {ex.Message}";
                        return;
                    }
                    else
                    {
                        MessageBox.Show($"This file could not be loaded:\n\n{ex.Message}", "Load Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        return;
                    }
                }

                var bmp = BitmapSource.Create(
                    width, height,
                    96, 96,
                    format, null,
                    image.Data, image.Stride);

                _originalBitmapSource = bmp;
                UpdateChannelPreview();
                currentTexturePath = filePath;
                _originalImageWidth = bmp.PixelWidth;
                _originalImageHeight = bmp.PixelHeight;

                // Fit image to viewer after layout is updated (only on first load)
                Dispatcher.BeginInvoke(new Action(() =>
                {
                    _zoom = GetFitZoom();
                    SetZoom(_zoom);
                }), System.Windows.Threading.DispatcherPriority.Loaded);

                TextureImage.Width = double.NaN;
                TextureImage.Height = double.NaN;
                StatusText.Text = $"Loaded: {Path.GetFileName(filePath)}";

                // Update info sidebar with robust info
                InfoFormat.Text = formatString;
                InfoDimensions.Text = $"{width} x {height}";
                InfoMipmaps.Text = mipmaps.ToString();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"This file could not be loaded:\n\n{ex.Message}", "Load Error", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        // Helper to extract DXGI format from DDS header
        private string GetDdsFormatString(byte[] ddsData)
        {
            // DDS header: DXGI format is at offset 0x54 (for DX10 header), otherwise FourCC at 0x54
            if (ddsData.Length < 0x80) return "-";
            // Check for DX10 header
            if (System.Text.Encoding.ASCII.GetString(ddsData, 0x54, 4) == "DX10")
            {
                int dxgiFormat = BitConverter.ToInt32(ddsData, 0x58);
                return $"DXGI {dxgiFormat}";
            }
            else
            {
                string fourcc = System.Text.Encoding.ASCII.GetString(ddsData, 0x54, 4);
                return fourcc;
            }
        }

        // Helper to extract mipmap count from DDS header
        private int GetDdsMipmapCount(byte[] ddsData)
        {
            if (ddsData.Length < 0x1C) return 1;
            return BitConverter.ToInt32(ddsData, 0x1C);
        }

        private void OnExportPng_Click(object sender, RoutedEventArgs e)
        {
            if (TextureImage.Source is BitmapSource bmp)
            {
                var dlg = new Microsoft.Win32.SaveFileDialog
                {
                    Filter = "PNG File (*.png)|*.png|DDS File (*.dds)|*.dds",
                    FileName = System.IO.Path.GetFileNameWithoutExtension(currentTexturePath)
                };

                if (dlg.ShowDialog() == true)
                {
                    string ext = System.IO.Path.GetExtension(dlg.FileName).ToLowerInvariant();
                    if (ext == ".png")
                    {
                        using var stream = new FileStream(dlg.FileName, FileMode.Create);
                        var encoder = new PngBitmapEncoder();
                        encoder.Frames.Add(BitmapFrame.Create(bmp));
                        encoder.Save(stream);
                        StatusText.Text = $"Exported PNG to {dlg.FileName}";
                    }
                    else if (ext == ".dds")
                    {
                        // Export DDS: If the current file is DDS, just copy the original file
                        if (!string.IsNullOrEmpty(currentTexturePath) && currentTexturePath.EndsWith(".dds", StringComparison.OrdinalIgnoreCase))
                        {
                            File.Copy(currentTexturePath, dlg.FileName, overwrite: true);
                            StatusText.Text = $"Exported DDS to {dlg.FileName}";
                        }
                        else if (!string.IsNullOrEmpty(currentTexturePath) && currentTexturePath.EndsWith(".texture", StringComparison.OrdinalIgnoreCase))
                        {
                            // Export DDS: If the current file is .texture, write the full DDS buffer used for display
                            if (_currentDdsData != null)
                            {
                                File.WriteAllBytes(dlg.FileName, _currentDdsData);
                                StatusText.Text = $"Exported DDS to {dlg.FileName}";
                            }
                            else
                            {
                                MessageBox.Show("DDS buffer is not available for export.", "Export Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                            }
                        }
                        else
                        {
                            MessageBox.Show("DDS export is only supported for DDS or .texture source files.", "Export Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                        }
                    }
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

        private void OnChannelComboBox_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            UpdateChannelPreview();
        }

        private void UpdateChannelPreview()
        {
            if (_originalBitmapSource == null) return;
            int channel = ChannelComboBox.SelectedIndex; // 0: RGBA, 1: R, 2: G, 3: B, 4: A
            if (channel == 0)
            {
                TextureImage.Source = _originalBitmapSource;
                return;
            }
            // Extract channel
            var wb = new WriteableBitmap(_originalBitmapSource);
            int width = wb.PixelWidth;
            int height = wb.PixelHeight;
            int stride = width * 4;
            byte[] pixels = new byte[height * stride];
            wb.CopyPixels(pixels, stride, 0);
            for (int i = 0; i < pixels.Length; i += 4)
            {
                byte r = pixels[i + 2];
                byte g = pixels[i + 1];
                byte b = pixels[i + 0];
                byte a = pixels[i + 3];
                switch (channel)
                {
                    case 1: // R
                        pixels[i + 0] = r;
                        pixels[i + 1] = r;
                        pixels[i + 2] = r;
                        break;
                    case 2: // G
                        pixels[i + 0] = g;
                        pixels[i + 1] = g;
                        pixels[i + 2] = g;
                        break;
                    case 3: // B
                        pixels[i + 0] = b;
                        pixels[i + 1] = b;
                        pixels[i + 2] = b;
                        break;
                    case 4: // A
                        pixels[i + 0] = a;
                        pixels[i + 1] = a;
                        pixels[i + 2] = a;
                        break;
                }
            }
            var filtered = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, pixels, stride);
            TextureImage.Source = filtered;
        }

        private double GetFitZoom()
        {
            // Get the size of the ScrollViewer viewport
            double viewportWidth = ImageScrollViewer.ViewportWidth;
            double viewportHeight = ImageScrollViewer.ViewportHeight;
            if (double.IsNaN(viewportWidth) || double.IsNaN(viewportHeight) || viewportWidth == 0 || viewportHeight == 0)
            {
                // Fallback: use actual control size
                viewportWidth = ImageScrollViewer.ActualWidth;
                viewportHeight = ImageScrollViewer.ActualHeight;
            }
            if (_originalImageWidth == 0 || _originalImageHeight == 0)
                return 1.0;
            double zoomX = viewportWidth / _originalImageWidth;
            double zoomY = viewportHeight / _originalImageHeight;
            return Math.Min(zoomX, zoomY);
        }
    }
}
