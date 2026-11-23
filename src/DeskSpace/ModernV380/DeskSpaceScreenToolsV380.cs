using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Effects;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Windows.Media.Ocr;
using Windows.Storage.Streams;
using Drawing = System.Drawing;
using DrawingImaging = System.Drawing.Imaging;
using Forms = System.Windows.Forms;
using WinRtImaging = Windows.Graphics.Imaging;
using WpfLine = System.Windows.Shapes.Line;
using WpfPath = System.Windows.Shapes.Path;
using WpfRectShape = System.Windows.Shapes.Rectangle;

namespace DeskSpace;

internal static class DeskSpaceScreenToolsV380
{
    [DllImport("gdi32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DeleteObject(IntPtr hObject);

    internal static async Task<string> RecognizeBitmapAsync(Drawing.Bitmap bmp)
    {
        ArgumentNullException.ThrowIfNull(bmp);
        byte[] pngBytes = EncodeBitmapToPngBytes(bmp);
        return await RecognizePngBytesAsync(pngBytes).ConfigureAwait(false);
    }

    internal static async Task<string> RecognizePngBytesAsync(byte[] pngBytes)
    {
        if (pngBytes == null || pngBytes.Length == 0)
            return string.Empty;

        try
        {
            using var ras = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(ras))
            {
                writer.WriteBytes(pngBytes);
                await writer.StoreAsync();
                writer.DetachStream();
            }
            ras.Seek(0);

            var decoder = await WinRtImaging.BitmapDecoder.CreateAsync(ras);
            using var softBmp = await decoder.GetSoftwareBitmapAsync();
            var engine = OcrEngine.TryCreateFromUserProfileLanguages();
            if (engine != null)
            {
                var ocrResult = await engine.RecognizeAsync(softBmp);
                if (ocrResult.Lines != null && ocrResult.Lines.Count > 0)
                    return string.Join(Environment.NewLine, ocrResult.Lines.Select(l => l.Text));
                return ocrResult.Text ?? string.Empty;
            }
        }
        catch
        {
            // Fall through to ImageOcr draft fallback.
        }

        string tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"deskspace_ocr_{Guid.NewGuid():N}.png");
        try
        {
            await File.WriteAllBytesAsync(tempFile, pngBytes).ConfigureAwait(false);
            var draft = await ImageOcr.RecognizeDraftAsync(tempFile).ConfigureAwait(false);
            return string.Join(Environment.NewLine, draft.Select(row => string.Join("\t", row)));
        }
        finally
        {
            try
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
            catch
            {
            }
        }
    }

    internal static async Task<List<List<string>>> RecognizeTableAsync(Drawing.Bitmap bmp)
    {
        ArgumentNullException.ThrowIfNull(bmp);
        byte[] pngBytes = EncodeBitmapToPngBytes(bmp);

        try
        {
            using var ras = new InMemoryRandomAccessStream();
            using (var writer = new DataWriter(ras))
            {
                writer.WriteBytes(pngBytes);
                await writer.StoreAsync();
                writer.DetachStream();
            }
            ras.Seek(0);

            var decoder = await WinRtImaging.BitmapDecoder.CreateAsync(ras);
            using var softBmp = await decoder.GetSoftwareBitmapAsync();
            var engine = OcrEngine.TryCreateFromUserProfileLanguages();
            if (engine != null)
            {
                var ocrResult = await engine.RecognizeAsync(softBmp);
                var words = new List<OcrWord>();
                if (ocrResult.Lines != null)
                {
                    foreach (var line in ocrResult.Lines)
                    {
                        foreach (var word in line.Words)
                        {
                            if (!string.IsNullOrWhiteSpace(word.Text))
                                words.Add(word);
                        }
                    }
                }

                if (words.Count == 0)
                    return new List<List<string>>();

                var heights = words.Select(w => w.BoundingRect.Height).Where(h => h > 0).OrderBy(h => h).ToArray();
                double medianHeight = heights.Length > 0 ? heights[heights.Length / 2] : 16.0;
                double yTolerance = Math.Max(4.0, medianHeight * 0.52);
                double colGap = Math.Max(14.0, medianHeight * 1.45);

                var rowGroups = new List<List<OcrWord>>();
                foreach (var w in words.OrderBy(x => x.BoundingRect.Y + x.BoundingRect.Height * 0.5).ThenBy(x => x.BoundingRect.X))
                {
                    double centerY = w.BoundingRect.Y + w.BoundingRect.Height * 0.5;
                    var targetRow = rowGroups.FirstOrDefault(g =>
                        Math.Abs(g.Average(x => x.BoundingRect.Y + x.BoundingRect.Height * 0.5) - centerY) <= yTolerance);

                    if (targetRow == null)
                        rowGroups.Add(new List<OcrWord> { w });
                    else
                        targetRow.Add(w);
                }

                var grid = new List<List<string>>();
                foreach (var rowGroup in rowGroups.OrderBy(g => g.Min(x => x.BoundingRect.Y)))
                {
                    var ordered = rowGroup.OrderBy(w => w.BoundingRect.X).ToArray();
                    var cells = new List<string>();
                    var cellText = new StringBuilder();
                    double prevRight = -1;

                    foreach (var w in ordered)
                    {
                        if (cellText.Length > 0 && prevRight >= 0 && (w.BoundingRect.X - prevRight) > colGap)
                        {
                            cells.Add(cellText.ToString().Trim());
                            cellText.Clear();
                        }
                        if (cellText.Length > 0)
                            cellText.Append(' ');
                        cellText.Append(w.Text.Trim());
                        prevRight = Math.Max(prevRight, w.BoundingRect.X + w.BoundingRect.Width);
                    }

                    if (cellText.Length > 0)
                        cells.Add(cellText.ToString().Trim());
                    if (cells.Count > 0)
                        grid.Add(cells);
                }

                return grid;
            }
        }
        catch
        {
            // Fall through to ImageOcr fallback.
        }

        string tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"deskspace_tbl_{Guid.NewGuid():N}.png");
        try
        {
            await File.WriteAllBytesAsync(tempFile, pngBytes).ConfigureAwait(false);
            return await ImageOcr.RecognizeDraftAsync(tempFile).ConfigureAwait(false);
        }
        finally
        {
            try
            {
                if (File.Exists(tempFile))
                    File.Delete(tempFile);
            }
            catch
            {
            }
        }
    }

    internal static async Task<List<List<string>>> RecognizeTableFromFileAsync(string imagePath)
    {
        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
            throw new FileNotFoundException("Image file was not found.", imagePath);

        byte[] bytes = await File.ReadAllBytesAsync(imagePath).ConfigureAwait(false);
        using var ms = new MemoryStream(bytes);
        using var bmp = new Drawing.Bitmap(ms);
        return await RecognizeTableAsync(bmp).ConfigureAwait(false);
    }

    internal static string GridToTsv(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        if (rows == null || rows.Count == 0)
            return string.Empty;

        var lines = new List<string>(rows.Count);
        foreach (var row in rows)
        {
            if (row == null)
            {
                lines.Add(string.Empty);
                continue;
            }
            lines.Add(string.Join("\t", row.Select(cell =>
                (cell ?? string.Empty)
                    .Replace('\r', ' ')
                    .Replace('\n', ' ')
                    .Replace('\t', ' ')
                    .Trim())));
        }
        return string.Join(Environment.NewLine, lines);
    }

    internal static void StartRegionSnip(
        Window? owner = null,
        Func<Workspace>? getWorkspace = null,
        Action? saveWorkspace = null,
        Action<Drawing.Bitmap>? onCaptured = null)
    {
        Drawing.Bitmap? screenBmp = null;
        BitmapSource screenSource;
        try
        {
            (screenBmp, screenSource) = CaptureVirtualScreen();
        }
        catch (Exception ex)
        {
            screenBmp?.Dispose();
            System.Windows.MessageBox.Show("Screen capture failed: " + ex.Message, "DeskSpace Snip", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        double virtLeft = SystemParameters.VirtualScreenLeft;
        double virtTop = SystemParameters.VirtualScreenTop;
        double virtWidth = SystemParameters.VirtualScreenWidth;
        double virtHeight = SystemParameters.VirtualScreenHeight;

        var overlay = new Window
        {
            Title = "DeskSpace · Region Snip",
            Left = virtLeft,
            Top = virtTop,
            Width = virtWidth,
            Height = virtHeight,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            Topmost = true,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Background = Brushes.Black,
            Cursor = Cursors.Cross
        };

        var rootCanvas = new Canvas
        {
            Width = virtWidth,
            Height = virtHeight,
            ClipToBounds = true
        };

        var bgImage = new Image
        {
            Source = screenSource,
            Width = virtWidth,
            Height = virtHeight,
            Stretch = Stretch.Fill,
            IsHitTestVisible = false
        };
        rootCanvas.Children.Add(bgImage);

        var dimMask = new WpfPath
        {
            Fill = new SolidColorBrush(Color.FromArgb(135, 6, 10, 22)),
            IsHitTestVisible = false
        };
        var fullScreenRectGeo = new RectangleGeometry(new Rect(0, 0, virtWidth, virtHeight));
        dimMask.Data = fullScreenRectGeo;
        rootCanvas.Children.Add(dimMask);

        var guideV = new WpfLine
        {
            Stroke = new SolidColorBrush(Color.FromArgb(160, 56, 189, 248)),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 4, 4 },
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
        };
        var guideH = new WpfLine
        {
            Stroke = new SolidColorBrush(Color.FromArgb(160, 56, 189, 248)),
            StrokeThickness = 1,
            StrokeDashArray = new DoubleCollection { 4, 4 },
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
        };
        rootCanvas.Children.Add(guideV);
        rootCanvas.Children.Add(guideH);

        var selectionRect = new WpfRectShape
        {
            Stroke = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
            StrokeThickness = 2,
            Fill = Brushes.Transparent,
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed
        };
        rootCanvas.Children.Add(selectionRect);

        var sizeLabel = new TextBlock
        {
            Foreground = Brushes.White,
            FontSize = 11.5,
            FontWeight = FontWeights.SemiBold,
            FontFamily = new FontFamily("Segoe UI Variable, Segoe UI")
        };
        var sizePill = new Border
        {
            CornerRadius = new CornerRadius(6),
            Background = new SolidColorBrush(Color.FromArgb(220, 12, 18, 35)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(56, 189, 248)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(8, 3, 8, 3),
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed,
            Child = sizeLabel
        };
        rootCanvas.Children.Add(sizePill);

        var hintBanner = new Border
        {
            CornerRadius = new CornerRadius(10),
            Background = new SolidColorBrush(Color.FromArgb(220, 12, 18, 35)),
            BorderBrush = new SolidColorBrush(Color.FromRgb(75, 94, 127)),
            BorderThickness = new Thickness(1),
            Padding = new Thickness(14, 7, 14, 7),
            IsHitTestVisible = false,
            Child = new TextBlock
            {
                Text = "Drag to select region · Esc or Right-click to cancel",
                Foreground = new SolidColorBrush(Color.FromRgb(236, 242, 255)),
                FontSize = 12.5,
                FontWeight = FontWeights.Medium,
                FontFamily = new FontFamily("Segoe UI Variable, Segoe UI")
            }
        };
        Canvas.SetLeft(hintBanner, Math.Max(20, (virtWidth - 360) * 0.5));
        Canvas.SetTop(hintBanner, 18);
        rootCanvas.Children.Add(hintBanner);

        var actionPanel = new WrapPanel { Orientation = Orientation.Horizontal };
        var p = DeskSpaceDesignV283.Current;
        var actionBar = new Border
        {
            CornerRadius = new CornerRadius(12),
            Background = DeskSpaceDesignV283.Solid(p.Canvas),
            BorderBrush = DeskSpaceDesignV283.Solid(p.Accent),
            BorderThickness = new Thickness(1.2),
            Padding = new Thickness(8, 6, 8, 6),
            Visibility = Visibility.Collapsed,
            Child = actionPanel,
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0, 0, 0),
                BlurRadius = 16,
                ShadowDepth = 4,
                Opacity = 0.55
            }
        };
        rootCanvas.Children.Add(actionBar);

        overlay.Content = rootCanvas;

        bool isDragging = false;
        bool hasSelection = false;
        bool screenDisposed = false;
        Point dragStart = default;
        Rect currentSelDip = Rect.Empty;

        void DisposeScreen()
        {
            if (screenDisposed)
                return;
            screenDisposed = true;
            try { screenBmp?.Dispose(); } catch { }
            screenBmp = null;
        }

        (double scaleX, double scaleY) GetPixelScale()
        {
            double w = rootCanvas.ActualWidth > 0 ? rootCanvas.ActualWidth : virtWidth;
            double h = rootCanvas.ActualHeight > 0 ? rootCanvas.ActualHeight : virtHeight;
            if (screenBmp != null && w > 0 && h > 0)
                return (screenBmp.Width / w, screenBmp.Height / h);

            var src = PresentationSource.FromVisual(overlay);
            if (src?.CompositionTarget != null)
                return (src.CompositionTarget.TransformToDevice.M11, src.CompositionTarget.TransformToDevice.M22);
            return (1.0, 1.0);
        }

        Drawing.Bitmap CropSelectedBitmap()
        {
            if (screenBmp == null || screenDisposed)
                throw new ObjectDisposedException(nameof(screenBmp));

            var (sx, sy) = GetPixelScale();
            int px = Math.Clamp((int)Math.Floor(currentSelDip.X * sx), 0, screenBmp.Width - 1);
            int py = Math.Clamp((int)Math.Floor(currentSelDip.Y * sy), 0, screenBmp.Height - 1);
            int pw = Math.Clamp((int)Math.Round(currentSelDip.Width * sx), 1, screenBmp.Width - px);
            int ph = Math.Clamp((int)Math.Round(currentSelDip.Height * sy), 1, screenBmp.Height - py);
            return screenBmp.Clone(new Drawing.Rectangle(px, py, pw, ph), DrawingImaging.PixelFormat.Format32bppArgb);
        }

        void UpdateSelectionVisuals(Rect selDip)
        {
            currentSelDip = selDip;
            if (selDip.Width < 1 || selDip.Height < 1)
            {
                dimMask.Data = fullScreenRectGeo;
                selectionRect.Visibility = Visibility.Collapsed;
                sizePill.Visibility = Visibility.Collapsed;
                return;
            }

            dimMask.Data = new CombinedGeometry(
                GeometryCombineMode.Exclude,
                fullScreenRectGeo,
                new RectangleGeometry(selDip));

            Canvas.SetLeft(selectionRect, selDip.X);
            Canvas.SetTop(selectionRect, selDip.Y);
            selectionRect.Width = selDip.Width;
            selectionRect.Height = selDip.Height;
            selectionRect.Visibility = Visibility.Visible;

            var (sx, sy) = GetPixelScale();
            int pw = Math.Max(1, (int)Math.Round(selDip.Width * sx));
            int ph = Math.Max(1, (int)Math.Round(selDip.Height * sy));
            sizeLabel.Text = $"{pw} × {ph} px";
            sizePill.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double pillLeft = Math.Clamp(selDip.X, 6, Math.Max(6, virtWidth - sizePill.DesiredSize.Width - 6));
            double pillTop = selDip.Y >= 32 ? selDip.Y - 28 : selDip.Bottom + 6;
            Canvas.SetLeft(sizePill, pillLeft);
            Canvas.SetTop(sizePill, Math.Clamp(pillTop, 6, Math.Max(6, virtHeight - 28)));
            sizePill.Visibility = Visibility.Visible;
        }

        void BuildActionBar()
        {
            actionPanel.Children.Clear();

            void AddToolBtn(string label, Action act, bool primary = false)
            {
                var btn = DeskSpaceFluentBackdropV380.CreatePillButton(label, act, primary);
                btn.Padding = new Thickness(11, 5, 11, 5);
                btn.MinHeight = 28;
                btn.FontSize = 12;
                btn.Margin = new Thickness(3);
                actionPanel.Children.Add(btn);
            }

            AddToolBtn("Copy Image", () =>
            {
                try
                {
                    using var crop = CropSelectedBitmap();
                    var bs = BitmapToBitmapSource(crop);
                    System.Windows.Clipboard.SetImage(bs);
                    DeskSpaceRichClipboardV380.CaptureNow();
                    overlay.Close();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(overlay, "Copy image failed: " + ex.Message, "DeskSpace Snip");
                }
            }, primary: true);

            AddToolBtn("Copy Text (OCR)", async () =>
            {
                actionBar.IsEnabled = false;
                try
                {
                    using var crop = CropSelectedBitmap();
                    string text = await RecognizeBitmapAsync(crop);
                    if (string.IsNullOrWhiteSpace(text))
                    {
                        actionBar.IsEnabled = true;
                        System.Windows.MessageBox.Show(overlay, "No readable text found in selected region.", "DeskSpace OCR");
                        return;
                    }
                    System.Windows.Clipboard.SetText(text);
                    DeskSpaceRichClipboardV380.AddTextEntry(text, "Screen Snip OCR");
                    overlay.Close();
                }
                catch (Exception ex)
                {
                    actionBar.IsEnabled = true;
                    System.Windows.MessageBox.Show(overlay, "OCR failed: " + ex.Message, "DeskSpace OCR");
                }
            });

            AddToolBtn("Table → Excel/Tab", async () =>
            {
                actionBar.IsEnabled = false;
                try
                {
                    using var crop = CropSelectedBitmap();
                    var grid = await RecognizeTableAsync(crop);
                    if (grid.Count == 0)
                    {
                        actionBar.IsEnabled = true;
                        System.Windows.MessageBox.Show(overlay, "No tabular text detected in selected region.", "DeskSpace Table OCR");
                        return;
                    }

                    string tsv = GridToTsv(grid);
                    System.Windows.Clipboard.SetText(tsv);
                    DeskSpaceRichClipboardV380.AddTextEntry(tsv, "Table OCR (TSV)");

                    if (getWorkspace != null)
                    {
                        var answer = System.Windows.MessageBox.Show(
                            overlay,
                            $"Copied {grid.Count} row(s) as Excel-ready TSV to clipboard.\n\nSave as a new Custom Data tab in DeskSpace?",
                            "Table → Excel/Tab",
                            MessageBoxButton.YesNo,
                            MessageBoxImage.Question);

                        if (answer == MessageBoxResult.Yes)
                        {
                            var ws = getWorkspace();
                            ws.DataTabs.Add(new DataTab
                            {
                                Name = $"Snip Table {DateTime.Now:HH:mm}",
                                Cells = grid,
                                OcrNeedsReview = true
                            });
                            saveWorkspace?.Invoke();
                        }
                    }

                    overlay.Close();
                }
                catch (Exception ex)
                {
                    actionBar.IsEnabled = true;
                    System.Windows.MessageBox.Show(overlay, "Table OCR failed: " + ex.Message, "DeskSpace Table OCR");
                }
            });

            AddToolBtn("Pin on Screen", () =>
            {
                try
                {
                    var crop = CropSelectedBitmap();
                    var posDip = new Point(virtLeft + currentSelDip.X, virtTop + currentSelDip.Y);
                    overlay.Close();
                    var pinned = new FloatingPinnedSnipV380(crop, posDip.X, posDip.Y, currentSelDip.Width, currentSelDip.Height);
                    pinned.Show();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(overlay, "Pin failed: " + ex.Message, "DeskSpace Snip");
                }
            });

            AddToolBtn("Save PNG", () =>
            {
                try
                {
                    using var crop = CropSelectedBitmap();
                    var dlg = new SaveFileDialog
                    {
                        Title = "Save Screen Snip",
                        Filter = "PNG Image|*.png",
                        FileName = $"DeskSpace_Snip_{DateTime.Now:yyyyMMdd_HHmmss}.png"
                    };
                    if (dlg.ShowDialog(overlay) == true)
                    {
                        crop.Save(dlg.FileName, DrawingImaging.ImageFormat.Png);
                        overlay.Close();
                    }
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(overlay, "Save failed: " + ex.Message, "DeskSpace Snip");
                }
            });

            AddToolBtn("Send to Device", () =>
            {
                try
                {
                    using var crop = CropSelectedBitmap();
                    string tempPng = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"deskspace_snip_{Guid.NewGuid():N}.png");
                    crop.Save(tempPng, DrawingImaging.ImageFormat.Png);

                    DeskSpaceDeskTouchBridgeV378.PollIfDueAsync(force: true);
                    var snap = DeskSpaceDeskTouchBridgeV378.Current;
                    var target = snap.Devices.FirstOrDefault(d => d.Id == snap.ActiveTargetId && !d.IsLocal && d.SupportsInput)
                                 ?? snap.Devices.FirstOrDefault(d => !d.IsLocal && d.SupportsInput);
                    int targetId = target?.Id ?? 0;

                    DeskSpaceDeskTouchBridgeV378.DropFilePathAsync(targetId, tempPng);
                    overlay.Close();
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(overlay, "Send to device failed: " + ex.Message, "DeskSpace Snip");
                }
            });

            AddToolBtn("×", () => overlay.Close());
        }

        void ShowActionBarNearSelection()
        {
            BuildActionBar();
            actionBar.Visibility = Visibility.Visible;
            actionBar.Measure(new Size(virtWidth - 24, virtHeight));
            double barW = actionBar.DesiredSize.Width;
            double barH = actionBar.DesiredSize.Height;

            double left = Math.Clamp(currentSelDip.X, 12, Math.Max(12, virtWidth - barW - 12));
            double top = currentSelDip.Bottom + 10;
            if (top + barH > virtHeight - 12)
                top = Math.Max(12, currentSelDip.Y - barH - 10);

            Canvas.SetLeft(actionBar, left);
            Canvas.SetTop(actionBar, top);
        }

        rootCanvas.MouseMove += (_, e) =>
        {
            Point pt = e.GetPosition(rootCanvas);
            if (!hasSelection)
            {
                guideV.X1 = guideV.X2 = pt.X;
                guideV.Y1 = 0;
                guideV.Y2 = virtHeight;
                guideV.Visibility = Visibility.Visible;

                guideH.Y1 = guideH.Y2 = pt.Y;
                guideH.X1 = 0;
                guideH.X2 = virtWidth;
                guideH.Visibility = Visibility.Visible;
            }

            if (isDragging)
            {
                double x = Math.Min(dragStart.X, pt.X);
                double y = Math.Min(dragStart.Y, pt.Y);
                double w = Math.Abs(pt.X - dragStart.X);
                double h = Math.Abs(pt.Y - dragStart.Y);
                UpdateSelectionVisuals(new Rect(x, y, w, h));
            }
        };

        rootCanvas.MouseLeftButtonDown += (_, e) =>
        {
            if (e.Source != rootCanvas && e.OriginalSource is DependencyObject dep && IsDescendantOf(dep, actionBar))
                return;

            hasSelection = false;
            actionBar.Visibility = Visibility.Collapsed;
            hintBanner.Visibility = Visibility.Collapsed;
            isDragging = true;
            dragStart = e.GetPosition(rootCanvas);
            UpdateSelectionVisuals(new Rect(dragStart.X, dragStart.Y, 0, 0));
            rootCanvas.CaptureMouse();
            e.Handled = true;
        };

        rootCanvas.MouseLeftButtonUp += (_, e) =>
        {
            if (!isDragging)
                return;
            isDragging = false;
            rootCanvas.ReleaseMouseCapture();

            Point end = e.GetPosition(rootCanvas);
            double x = Math.Min(dragStart.X, end.X);
            double y = Math.Min(dragStart.Y, end.Y);
            double w = Math.Abs(end.X - dragStart.X);
            double h = Math.Abs(end.Y - dragStart.Y);

            if (w < 6 || h < 6)
            {
                UpdateSelectionVisuals(Rect.Empty);
                hintBanner.Visibility = Visibility.Visible;
                return;
            }

            UpdateSelectionVisuals(new Rect(x, y, w, h));
            hasSelection = true;
            guideV.Visibility = Visibility.Collapsed;
            guideH.Visibility = Visibility.Collapsed;

            if (onCaptured != null)
            {
                try
                {
                    var capturedBmp = CropSelectedBitmap();
                    overlay.Close();
                    onCaptured(capturedBmp);
                }
                catch
                {
                    overlay.Close();
                }
                return;
            }

            ShowActionBarNearSelection();
        };

        overlay.MouseRightButtonDown += (_, e) =>
        {
            e.Handled = true;
            overlay.Close();
        };

        overlay.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                overlay.Close();
            }
        };

        overlay.Closed += (_, _) => DisposeScreen();
        overlay.Loaded += (_, _) => overlay.Activate();
        overlay.Show();
        overlay.Activate();
    }

    internal static void StartEyedropper(Window? owner = null)
    {
        Drawing.Bitmap? screenBmp = null;
        BitmapSource screenSource;
        try
        {
            (screenBmp, screenSource) = CaptureVirtualScreen();
        }
        catch (Exception ex)
        {
            screenBmp?.Dispose();
            System.Windows.MessageBox.Show("Eyedropper capture failed: " + ex.Message, "DeskSpace Eyedropper", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        double virtLeft = SystemParameters.VirtualScreenLeft;
        double virtTop = SystemParameters.VirtualScreenTop;
        double virtWidth = SystemParameters.VirtualScreenWidth;
        double virtHeight = SystemParameters.VirtualScreenHeight;

        var overlay = new Window
        {
            Title = "DeskSpace · Color Eyedropper",
            Left = virtLeft,
            Top = virtTop,
            Width = virtWidth,
            Height = virtHeight,
            WindowStyle = WindowStyle.None,
            ResizeMode = ResizeMode.NoResize,
            Topmost = true,
            ShowInTaskbar = false,
            WindowStartupLocation = WindowStartupLocation.Manual,
            Background = Brushes.Black,
            Cursor = Cursors.Cross
        };

        var rootCanvas = new Canvas
        {
            Width = virtWidth,
            Height = virtHeight,
            ClipToBounds = true
        };
        rootCanvas.Children.Add(new Image
        {
            Source = screenSource,
            Width = virtWidth,
            Height = virtHeight,
            Stretch = Stretch.Fill,
            IsHitTestVisible = false
        });

        var p = DeskSpaceDesignV283.Current;

        // Floating loupe & readout card
        var zoomImage = new Image
        {
            Width = 88,
            Height = 88,
            Stretch = Stretch.Fill
        };
        RenderOptions.SetBitmapScalingMode(zoomImage, BitmapScalingMode.NearestNeighbor);

        var loupeContainer = new Grid { Width = 88, Height = 88 };
        loupeContainer.Children.Add(zoomImage);
        loupeContainer.Children.Add(new Border
        {
            Width = 8,
            Height = 8,
            BorderBrush = Brushes.White,
            BorderThickness = new Thickness(1.5),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });
        loupeContainer.Children.Add(new Border
        {
            Width = 10,
            Height = 10,
            BorderBrush = Brushes.Black,
            BorderThickness = new Thickness(1),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        });

        var loupeBorder = new Border
        {
            Width = 90,
            Height = 90,
            CornerRadius = new CornerRadius(10),
            ClipToBounds = true,
            BorderBrush = DeskSpaceDesignV283.Solid(p.Border),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 10, 0),
            Child = loupeContainer
        };

        var swatchBox = new Border
        {
            Width = 28,
            Height = 28,
            CornerRadius = new CornerRadius(7),
            BorderBrush = DeskSpaceDesignV283.Solid(p.Border),
            BorderThickness = new Thickness(1),
            Margin = new Thickness(0, 0, 8, 0)
        };
        var hexText = new TextBlock
        {
            FontSize = 15,
            FontWeight = FontWeights.Bold,
            Foreground = DeskSpaceDesignV283.Solid(p.Text)
        };
        var rgbText = new TextBlock
        {
            FontSize = 11.5,
            Foreground = DeskSpaceDesignV283.Solid(p.Muted)
        };
        var hslText = new TextBlock
        {
            FontSize = 11.5,
            Foreground = DeskSpaceDesignV283.Solid(p.Muted)
        };

        var infoStack = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var topInfoRow = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 0, 0, 4) };
        topInfoRow.Children.Add(swatchBox);
        topInfoRow.Children.Add(hexText);
        infoStack.Children.Add(topInfoRow);
        infoStack.Children.Add(rgbText);
        infoStack.Children.Add(hslText);
        infoStack.Children.Add(new TextBlock
        {
            Text = "Click to copy · Esc cancel",
            FontSize = 10.5,
            Foreground = DeskSpaceDesignV283.Solid(p.Accent),
            Margin = new Thickness(0, 4, 0, 0)
        });

        var cardRow = new StackPanel { Orientation = Orientation.Horizontal };
        cardRow.Children.Add(loupeBorder);
        cardRow.Children.Add(infoStack);

        var floatingCard = new Border
        {
            CornerRadius = new CornerRadius(12),
            Background = DeskSpaceDesignV283.Solid(p.Canvas),
            BorderBrush = DeskSpaceDesignV283.Solid(p.Border),
            BorderThickness = new Thickness(1.2),
            Padding = new Thickness(10),
            IsHitTestVisible = false,
            Visibility = Visibility.Collapsed,
            Child = cardRow,
            Effect = new DropShadowEffect
            {
                Color = Color.FromRgb(0, 0, 0),
                BlurRadius = 18,
                ShadowDepth = 4,
                Opacity = 0.55
            }
        };
        rootCanvas.Children.Add(floatingCard);

        overlay.Content = rootCanvas;

        bool screenDisposed = false;
        string currentHex = "#000000";

        void DisposeScreen()
        {
            if (screenDisposed)
                return;
            screenDisposed = true;
            try { screenBmp?.Dispose(); } catch { }
            screenBmp = null;
        }

        void UpdateAtCursor(Point pt)
        {
            if (screenBmp == null || screenDisposed)
                return;

            double w = rootCanvas.ActualWidth > 0 ? rootCanvas.ActualWidth : virtWidth;
            double h = rootCanvas.ActualHeight > 0 ? rootCanvas.ActualHeight : virtHeight;
            double scaleX = screenBmp.Width / Math.Max(1.0, w);
            double scaleY = screenBmp.Height / Math.Max(1.0, h);

            int px = Math.Clamp((int)Math.Floor(pt.X * scaleX), 0, screenBmp.Width - 1);
            int py = Math.Clamp((int)Math.Floor(pt.Y * scaleY), 0, screenBmp.Height - 1);

            Drawing.Color c = screenBmp.GetPixel(px, py);
            currentHex = $"#{c.R:X2}{c.G:X2}{c.B:X2}";
            var (hue, sat, lig) = RgbToHsl(c.R, c.G, c.B);

            swatchBox.Background = new SolidColorBrush(Color.FromRgb(c.R, c.G, c.B));
            hexText.Text = currentHex;
            rgbText.Text = $"rgb({c.R}, {c.G}, {c.B})";
            hslText.Text = $"hsl({hue}°, {sat}%, {lig}%)";

            const int patchSize = 11;
            int half = patchSize / 2;
            int cropX = Math.Clamp(px - half, 0, Math.Max(0, screenSource.PixelWidth - patchSize));
            int cropY = Math.Clamp(py - half, 0, Math.Max(0, screenSource.PixelHeight - patchSize));
            int cropW = Math.Min(patchSize, screenSource.PixelWidth - cropX);
            int cropH = Math.Min(patchSize, screenSource.PixelHeight - cropY);
            if (cropW > 0 && cropH > 0)
            {
                zoomImage.Source = new CroppedBitmap(screenSource, new Int32Rect(cropX, cropY, cropW, cropH));
            }

            floatingCard.Visibility = Visibility.Visible;
            floatingCard.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
            double cardW = Math.Max(230, floatingCard.DesiredSize.Width);
            double cardH = Math.Max(110, floatingCard.DesiredSize.Height);

            double left = pt.X + 20;
            if (left + cardW > virtWidth - 10)
                left = Math.Max(10, pt.X - cardW - 18);

            double top = pt.Y + 20;
            if (top + cardH > virtHeight - 10)
                top = Math.Max(10, pt.Y - cardH - 18);

            Canvas.SetLeft(floatingCard, left);
            Canvas.SetTop(floatingCard, top);
        }

        rootCanvas.MouseMove += (_, e) => UpdateAtCursor(e.GetPosition(rootCanvas));

        rootCanvas.MouseLeftButtonDown += (_, e) =>
        {
            UpdateAtCursor(e.GetPosition(rootCanvas));
            try
            {
                System.Windows.Clipboard.SetText(currentHex);
                DeskSpaceRichClipboardV380.AddColorEntry(currentHex);
            }
            catch
            {
            }
            e.Handled = true;
            overlay.Close();
        };

        overlay.MouseRightButtonDown += (_, e) =>
        {
            e.Handled = true;
            overlay.Close();
        };

        overlay.PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                overlay.Close();
            }
        };

        overlay.Closed += (_, _) => DisposeScreen();
        overlay.Loaded += (_, _) =>
        {
            overlay.Activate();
            Point initial = Mouse.GetPosition(rootCanvas);
            if (initial.X >= 0 && initial.Y >= 0 && initial.X < virtWidth && initial.Y < virtHeight)
                UpdateAtCursor(initial);
        };
        overlay.Show();
        overlay.Activate();
    }

    private static (Drawing.Bitmap Bitmap, BitmapSource Source) CaptureVirtualScreen()
    {
        var physBounds = Forms.SystemInformation.VirtualScreen;
        int w = Math.Max(1, physBounds.Width);
        int h = Math.Max(1, physBounds.Height);
        var bmp = new Drawing.Bitmap(w, h, DrawingImaging.PixelFormat.Format32bppArgb);
        try
        {
            using (var g = Drawing.Graphics.FromImage(bmp))
            {
                g.CopyFromScreen(physBounds.Left, physBounds.Top, 0, 0, new Drawing.Size(w, h), Drawing.CopyPixelOperation.SourceCopy);
            }
            var bs = BitmapToBitmapSource(bmp);
            return (bmp, bs);
        }
        catch
        {
            bmp.Dispose();
            throw;
        }
    }

    internal static BitmapSource BitmapToBitmapSource(Drawing.Bitmap bmp)
    {
        IntPtr hBitmap = bmp.GetHbitmap();
        try
        {
            var src = Imaging.CreateBitmapSourceFromHBitmap(
                hBitmap,
                IntPtr.Zero,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            src.Freeze();
            return src;
        }
        finally
        {
            DeleteObject(hBitmap);
        }
    }

    private static byte[] EncodeBitmapToPngBytes(Drawing.Bitmap bmp)
    {
        using var ms = new MemoryStream();
        bmp.Save(ms, DrawingImaging.ImageFormat.Png);
        return ms.ToArray();
    }

    private static bool IsDescendantOf(DependencyObject? node, DependencyObject ancestor)
    {
        while (node != null)
        {
            if (ReferenceEquals(node, ancestor))
                return true;
            node = VisualTreeHelper.GetParent(node);
        }
        return false;
    }

    private static (int H, int S, int L) RgbToHsl(byte rByte, byte gByte, byte bByte)
    {
        double r = rByte / 255.0;
        double g = gByte / 255.0;
        double b = bByte / 255.0;
        double max = Math.Max(r, Math.Max(g, b));
        double min = Math.Min(r, Math.Min(g, b));
        double l = (max + min) * 0.5;
        double h = 0;
        double s = 0;

        double delta = max - min;
        if (delta > 0.00001)
        {
            s = l > 0.5 ? delta / (2.0 - max - min) : delta / (max + min);
            if (Math.Abs(max - r) < 0.00001)
                h = ((g - b) / delta) + (g < b ? 6 : 0);
            else if (Math.Abs(max - g) < 0.00001)
                h = ((b - r) / delta) + 2;
            else
                h = ((r - g) / delta) + 4;
            h *= 60.0;
        }

        return ((int)Math.Round(h), (int)Math.Round(s * 100.0), (int)Math.Round(l * 100.0));
    }
}

internal sealed class FloatingPinnedSnipV380 : Window
{
    private readonly Drawing.Bitmap _bitmap;
    private readonly double _baseWidth;
    private readonly double _baseHeight;
    private readonly Image _image;
    private double _zoom = 1.0;

    internal FloatingPinnedSnipV380(Drawing.Bitmap bitmap, double left, double top, double initialWidth, double initialHeight)
    {
        _bitmap = bitmap ?? throw new ArgumentNullException(nameof(bitmap));
        _baseWidth = Math.Max(80, initialWidth);
        _baseHeight = Math.Max(60, initialHeight);

        Title = "DeskSpace · Pinned Snip";
        WindowStyle = WindowStyle.None;
        AllowsTransparency = true;
        Background = Brushes.Transparent;
        Topmost = true;
        ShowInTaskbar = false;
        ResizeMode = ResizeMode.NoResize;
        WindowStartupLocation = WindowStartupLocation.Manual;
        Left = left;
        Top = top;
        SizeToContent = SizeToContent.WidthAndHeight;

        var p = DeskSpaceDesignV283.Current;
        var bmpSrc = DeskSpaceScreenToolsV380.BitmapToBitmapSource(_bitmap);

        _image = new Image
        {
            Source = bmpSrc,
            Width = _baseWidth,
            Height = _baseHeight,
            Stretch = Stretch.Uniform
        };

        var chrome = new Border
        {
            CornerRadius = new CornerRadius(10),
            Background = DeskSpaceDesignV283.Solid(p.Canvas),
            BorderBrush = DeskSpaceDesignV283.Solid(p.Accent),
            BorderThickness = new Thickness(1.5),
            Padding = new Thickness(4),
            Effect = p.HighContrast ? null : new DropShadowEffect
            {
                Color = Color.FromRgb(0, 0, 0),
                BlurRadius = 16,
                ShadowDepth = 4,
                Opacity = 0.5
            }
        };

        var root = new DockPanel { LastChildFill = true };

        var toolbar = new DockPanel
        {
            LastChildFill = false,
            Margin = new Thickness(4, 2, 4, 4)
        };

        var zoomBadge = new TextBlock
        {
            Text = "100%",
            FontSize = 11,
            FontWeight = FontWeights.SemiBold,
            Foreground = DeskSpaceDesignV283.Solid(p.Muted),
            VerticalAlignment = VerticalAlignment.Center,
            Margin = new Thickness(4, 0, 8, 0)
        };
        DockPanel.SetDock(zoomBadge, Dock.Left);
        toolbar.Children.Add(zoomBadge);

        var actions = new StackPanel { Orientation = Orientation.Horizontal };

        Button MakeMiniBtn(string text, string tip, Action click)
        {
            var b = DeskSpaceFluentBackdropV380.CreatePillButton(text, click, false, tip);
            b.FontSize = 10.5;
            b.Padding = new Thickness(7, 2, 7, 2);
            b.MinHeight = 22;
            b.Margin = new Thickness(2, 0, 2, 0);
            return b;
        }

        double[] opacitySteps = { 1.0, 0.80, 0.55 };
        int opacityIdx = 0;
        Button? opBtn = null;
        opBtn = MakeMiniBtn("100% α", "Cycle opacity (100% / 80% / 55%)", () =>
        {
            opacityIdx = (opacityIdx + 1) % opacitySteps.Length;
            Opacity = opacitySteps[opacityIdx];
            if (opBtn != null)
                opBtn.Content = $"{(int)(Opacity * 100)}% α";
        });
        actions.Children.Add(opBtn);

        actions.Children.Add(MakeMiniBtn("Copy", "Copy image to clipboard", () =>
        {
            try
            {
                System.Windows.Clipboard.SetImage(bmpSrc);
            }
            catch
            {
            }
        }));

        actions.Children.Add(MakeMiniBtn("OCR", "Extract text via OCR", async () =>
        {
            try
            {
                string txt = await DeskSpaceScreenToolsV380.RecognizeBitmapAsync(_bitmap);
                if (!string.IsNullOrWhiteSpace(txt))
                {
                    System.Windows.Clipboard.SetText(txt);
                    DeskSpaceRichClipboardV380.AddTextEntry(txt, "Pinned Snip OCR");
                }
            }
            catch
            {
            }
        }));

        actions.Children.Add(MakeMiniBtn("×", "Close pinned snip", Close));

        DockPanel.SetDock(actions, Dock.Right);
        toolbar.Children.Add(actions);

        DockPanel.SetDock(toolbar, Dock.Top);
        root.Children.Add(toolbar);
        root.Children.Add(new Border
        {
            CornerRadius = new CornerRadius(6),
            ClipToBounds = true,
            Child = _image
        });

        chrome.Child = root;
        Content = chrome;

        _image.MouseLeftButtonDown += (_, e) =>
        {
            if (e.ButtonState == MouseButtonState.Pressed)
            {
                try { DragMove(); } catch { }
            }
        };

        PreviewMouseWheel += (_, e) =>
        {
            double step = e.Delta > 0 ? 0.1 : -0.1;
            _zoom = Math.Clamp(_zoom + step, 0.30, 3.00);
            _image.Width = _baseWidth * _zoom;
            _image.Height = _baseHeight * _zoom;
            zoomBadge.Text = $"{Math.Round(_zoom * 100)}%";
            e.Handled = true;
        };

        PreviewKeyDown += (_, e) =>
        {
            if (e.Key == Key.Escape)
            {
                e.Handled = true;
                Close();
            }
        };

        Closed += (_, _) =>
        {
            try { _bitmap.Dispose(); } catch { }
        };
    }
}
