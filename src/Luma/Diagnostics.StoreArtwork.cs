using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Luma.Models;
using Luma.Services;
using Luma.Themes;
using Luma.Views;

namespace Luma;

internal static partial class Diagnostics
{
    // Uses the actual app views with isolated fictional values, never the user's
    // sessions, settings, audio handlers, screen capture or display brightness.
    internal static async Task<int> CreateStoreArtworkAsync(string directory)
    {
        Directory.CreateDirectory(directory);
        Strings.SetLanguage("en");
        var settings = new AppSettings { Language = "en", Animations = false, WelcomeCompleted = true, Position = "TopRight", DurationSeconds = 8 };
        Palette.Apply(settings);
        var media = PreviewData.Media() with { AppName = "Sample player", IsPlaying = false };
        var window = new SettingsWindow(settings, () => { }) { AllowClose = true, Width = 1080, Height = 720 };
        var flyout = new FlyoutWindow(settings);
        try
        {
            window.UpdatePreviewAudio(new(true, 40, false, "Sample output"));
            window.UpdatePreviewMedia(media);
            window.Show(); window.Navigate("Appearance");
            flyout.ShowPreview(media);
            await Task.Delay(400);
            StoreComposition(directory, "01-overview.png", "Your controls.\nYour space.", "Volume, media and brightness,\nwith a lighter touch.", window, (FrameworkElement)flyout.Content, false);

            window.Navigate("Sound"); await Task.Delay(100);
            var audio = new AudioPanel { Width = 372 };
            audio.SetState(new(new[] { new AudioOutput("sample", "Sample speakers", true) }, new[]
            {
                new AudioApp("music", "Music player", "sample", Array.Empty<string>(), 72, false),
                new AudioApp("browser", "Browser", "sample", Array.Empty<string>(), 46, false),
                new AudioApp("system", "System sounds", "sample", Array.Empty<string>(), 28, false)
            }, ""));
            audio.Measure(new Size(372, double.PositiveInfinity)); audio.Arrange(new Rect(audio.DesiredSize)); audio.UpdateLayout();
            StoreComposition(directory, "02-audio.png", "Every sound,\nwithin reach.", "Choose an output.\nBalance individual app volumes.", window, audio, false);

            var themes = (ComboBox)window.FindName("ThemeCombo");
            themes.SelectedItem = themes.Items.Cast<ComboBoxItem>().Single(item => (string)item.Tag == "Light");
            window.Navigate("Appearance");
            flyout.ApplyPreferences(); await Task.Delay(180);
            StoreComposition(directory, "03-personalization.png", "Make room\nfor your style.", "Light and dark themes.\nColors, placement and size.", window, (FrameworkElement)flyout.Content, true);
            File.WriteAllText(Path.Combine(directory, "README.txt"), "1920x1080 PNG Store screenshots rendered from actual Luma views. All media and audio values are fictional. No live user sessions or desktop captures are used. These are listing artwork, not proof of Store certification or package deployment.\n");
            return 0;
        }
        finally { flyout.Close(); window.Close(); }
    }

    private static BitmapSource StoreSnapshot(FrameworkElement element, double scale = 1)
    {
        element.UpdateLayout();
        var result = new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth * scale), (int)Math.Ceiling(element.ActualHeight * scale), 96 * scale, 96 * scale, PixelFormats.Pbgra32);
        result.Render(element); result.Freeze(); return result;
    }

    private static void StoreComposition(string directory, string filename, string title, string subtitle, SettingsWindow window, FrameworkElement detail, bool light)
    {
        var canvas = new DrawingVisual();
        using (var drawing = canvas.RenderOpen())
        {
            drawing.DrawRectangle(new SolidColorBrush((Color)ColorConverter.ConvertFromString(light ? "#F3F5FC" : "#0C1120")), null, new Rect(0, 0, 1920, 1080));
            var glow = new RadialGradientBrush((Color)ColorConverter.ConvertFromString(light ? "#304A8BEB" : "#403D68D2"), Colors.Transparent);
            drawing.DrawEllipse(glow, null, new Point(1470, 540), 990, 820);
            drawing.DrawImage((ImageSource)Application.Current.FindResource("BrandSymbol"), new Rect(92, 64, 64, 64));
            void Text(string value, double size, double x, double y, bool muted = false, bool bold = false)
            {
                var color = light ? (muted ? "#566078" : "#182039") : (muted ? "#AAB7D0" : "#F3F6FF");
                var text = new FormattedText(value, CultureInfo.GetCultureInfo("en-US"), FlowDirection.LeftToRight,
                    new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, bold ? FontWeights.SemiBold : FontWeights.Normal, FontStretches.Normal),
                    size, new SolidColorBrush((Color)ColorConverter.ConvertFromString(color)), 1);
                drawing.DrawText(text, new Point(x, y));
            }
            Text("Luma", 42, 173, 65, bold: true);
            Text("VOLUME  /  MEDIA  /  BRIGHTNESS", 16, 95, 177, true);
            Text(title, 63, 91, 235, bold: true);
            Text(subtitle, 24, 96, 420, true);
            var detailBitmap = StoreSnapshot(detail, 2);
            var detailWidth = 470.0;
            drawing.DrawImage(detailBitmap, new Rect(96, 548, detailWidth, detailBitmap.PixelHeight * detailWidth / detailBitmap.PixelWidth));
            var app = StoreSnapshot((FrameworkElement)window.Content);
            var appRect = new Rect(733, 218, app.PixelWidth, app.PixelHeight);
            drawing.DrawRoundedRectangle(new SolidColorBrush(light ? Color.FromArgb(24, 25, 40, 90) : Color.FromArgb(90, 0, 0, 0)), null, new Rect(appRect.X - 15, appRect.Y - 15, appRect.Width + 30, appRect.Height + 30), 28, 28);
            drawing.PushClip(new RectangleGeometry(appRect, 14, 14));
            drawing.DrawImage(app, appRect); drawing.Pop();
            Text("Sample media and device values. Windows 11.", 17, 96, 1016, true);
        }
        var bitmap = new RenderTargetBitmap(1920, 1080, 96, 96, PixelFormats.Pbgra32); bitmap.Render(canvas);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var output = File.Create(Path.Combine(directory, filename)); encoder.Save(output);
    }
}
