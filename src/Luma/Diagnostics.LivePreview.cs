using System;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Luma.Models;
using Luma.Services;
using Luma.Themes;
using Luma.Views;
using MediaState = Luma.Models.MediaState;

namespace Luma;

internal static partial class Diagnostics
{
    private static void SeedSettingsPreview(SettingsWindow window)
    {
        // Public screenshots only use fictional media and audio, never a real session.
        window.UpdatePreviewAudio(new(true, 40, false, "Luma preview"));
        window.UpdatePreviewMedia(PreviewData.Media());
    }

    private static async Task VerifyLivePreviewAsync(string directory, Action<string, bool, string> check)
    {
        Strings.SetLanguage("tr");
        var prefs = new AppSettings { Animations = false, Position = "TopRight", WelcomeCompleted = true };
        Palette.Apply(prefs);
        var saves = 0;
        var window = new SettingsWindow(prefs, () => saves++) { AllowClose = true };
        var sample = PreviewData.Media();
        window.UpdatePreviewAudio(new(true, 64, false, "Sample output"));
        window.UpdatePreviewMedia(sample);
        window.Show(); await Task.Delay(80);
        var volume = (TextBlock)window.FindName("PreviewVolumeValue");
        var title = (TextBlock)window.FindName("PreviewTrackTitle");
        var cover = (Image)window.FindName("PreviewBackdrop");
        var play = (TextBlock)window.FindName("PreviewPlayIcon");
        var timeline = (Luma.Controls.SmoothSlider)window.FindName("PreviewTimeline");
        check("Settings preview shows current volume, selected media, artwork and playback state", volume.Text == "64" && title.Text == sample.Title && cover.Source == sample.Artwork && play.Text == "\uE769" && window.PreviewClockRunning, "");
        var start = timeline.Value; await Task.Delay(400);
        check("The settings playback clock advances between media service snapshots", timeline.Value > start + 0.15, "");
        window.UpdatePreviewAudio(new(true, 78, true, "Second output"));
        var paused = sample with { SessionId = "second", AppName = "Sample player", Title = "Afterglow", Artist = "Luma Sessions", Artwork = null, IsPlaying = false, Position = TimeSpan.FromSeconds(25) };
        window.UpdatePreviewMedia(paused);
        check("Volume, mute and source changes immediately replace the settings preview", volume.Text == "78" && ((TextBlock)window.FindName("PreviewVolumeIcon")).Text == "\uE74F" && title.Text == "Afterglow" && cover.Source is null && play.Text == "\uE768" && !window.PreviewClockRunning && timeline.Value == 25, "");
        window.UpdatePreviewMedia(paused with { End = TimeSpan.Zero });
        check("Unknown media duration does not show a fictional playback range", timeline.Visibility == Visibility.Hidden && ((TextBlock)window.FindName("PreviewDuration")).Text.Length == 0, "");
        window.UpdatePreviewMedia(MediaState.Empty);
        window.UpdatePreviewAudio(new(false, 0, false, ""));
        check("Closed media and disconnected audio clear stale preview content", ((FrameworkElement)window.FindName("MediaPreviewCard")).Visibility == Visibility.Collapsed && ((FrameworkElement)window.FindName("PreviewEmptyMedia")).Visibility == Visibility.Visible && cover.Source is null && volume.Text == "—", "");
        Render(window, Path.Combine(directory, "luma-live-preview-empty.png"));
        window.UpdatePreviewMedia(sample); window.Hide();
        var frozen = timeline.Value;
        window.UpdatePreviewMedia(sample with { Title = "Night drive", Position = TimeSpan.FromSeconds(90), SampledAt = DateTimeOffset.UtcNow });
        window.UpdatePreviewAudio(new(true, 32, false, "Sample output"));
        await Task.Delay(300);
        var stopped = !window.PreviewClockRunning && timeline.Value == frozen;
        window.Show(); await Task.Delay(40);
        check("Hidden settings stop their playback clock and catch up from cached state on reopening", stopped && window.PreviewClockRunning && title.Text == "Night drive" && volume.Text == "32" && timeline.Value >= 90, "");
        var volumeSlider = (Luma.Controls.SmoothSlider)window.FindName("PreviewVolumeSlider");
        check("Live preview updates are observational and do not save preferences or issue commands", saves == 0 && !volumeSlider.IsHitTestVisible && !volumeSlider.Focusable && !timeline.IsHitTestVisible && !timeline.Focusable, "");
        ((CheckBox)window.FindName("ArtworkToggle")).IsChecked = false;
        ((CheckBox)window.FindName("MediaToggle")).IsChecked = false;
        check("Media and artwork preferences apply to the live preview and stop unnecessary animation", ((FrameworkElement)window.FindName("MediaPreviewCard")).Visibility == Visibility.Collapsed && ((FrameworkElement)window.FindName("PreviewEmptyMedia")).Visibility == Visibility.Collapsed && ((Image)window.FindName("PreviewBackdrop")).Visibility == Visibility.Collapsed && !window.PreviewClockRunning, "");
        window.Close();

        var flyout = new FlyoutWindow(prefs);
        flyout.AudioPanel.SetState(new(new[] { new AudioOutput("sample", "Sample output", true) }, new[] { new AudioApp("sample", "Sample player", "sample", new[] { "sample-session" }, 55, false) }, "Seçili çıkıştaki uygulamalar"));
        var popup = (Popup)flyout.FindName("AudioPopup");
        var card = (FrameworkElement)flyout.FindName("VolumeCard");
        var button = (Button)flyout.FindName("OutputButton");
        var align = true; var inBounds = true; var arrows = true; var maxError = 0d;
        foreach (var position in new[] { "TopLeft", "TopRight", "BottomLeft", "BottomRight" })
        foreach (var scale in new[] { 0.75, 1d, 1.4 })
        {
            prefs.Position = position; prefs.FlyoutScale = scale;
            flyout.ApplyPreferences(); flyout.ShowVolume(new(true, 28, false, "Sample output"));
            button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); await Task.Delay(70);
            var error = Math.Abs(card.PointToScreen(new Point(card.ActualWidth, 0)).X - flyout.AudioPanel.PointToScreen(new Point(flyout.AudioPanel.ActualWidth, 0)).X);
            maxError = Math.Max(maxError, error); align &= popup.IsOpen && error <= 2;
            var buttonPosition = button.TransformToAncestor(card).Transform(new Point());
            inBounds &= buttonPosition.X >= 0 && buttonPosition.X + button.ActualWidth <= card.ActualWidth - 10 && Math.Abs(buttonPosition.Y + button.ActualHeight / 2 - card.ActualHeight / 2) <= 1;
            arrows &= ((RotateTransform)flyout.FindName("OutputChevronRotation")).Angle == (position.StartsWith("Top", StringComparison.Ordinal) ? 180 : 0);
            popup.IsOpen = false;
        }
        check("Audio popup right edges align with the volume card at every corner and scale", align, $"Maximum physical alignment error: {maxError:0.##} px");
        check("The disclosure arrow fits inside the card with balanced vertical alignment", inBounds && arrows, "");
        prefs.Position = "TopLeft"; prefs.FlyoutScale = 1;
        flyout.ApplyPreferences(); flyout.ShowVolume(new(true, 28, false, "Sample output")); button.RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent)); await Task.Delay(50);
        Palette.Apply(new AppSettings { Theme = "Light" }); flyout.ApplyPreferences();
        check("Appearance updates preserve an open audio panel when placement is unchanged", popup.IsOpen, "");
        Render(flyout, Path.Combine(directory, "luma-aligned-volume.png"));
        RenderElement(flyout.AudioPanel, Path.Combine(directory, "luma-aligned-audio-panel.png"));
        popup.IsOpen = false; flyout.Close();
        Palette.Apply(new AppSettings());
    }
}
