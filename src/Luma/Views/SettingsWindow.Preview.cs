using System;
using System.Windows;
using System.Windows.Media;
using System.Windows.Threading;
using Luma.Models;
using Luma.Services;

namespace Luma.Views;

public partial class SettingsWindow
{
    private AudioState previewAudio = new(false, 0, false, "");
    private MediaState previewMedia = MediaState.Empty;
    private ImageSource? previewArtwork;
    private readonly DispatcherTimer previewClock = new(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(250) };
    internal bool PreviewClockRunning => previewClock.IsEnabled;

    internal void UpdatePreviewAudio(AudioState state)
    {
        previewAudio = state;
        if (IsVisible) PaintPreviewAudio();
    }

    internal void UpdatePreviewMedia(MediaState state)
    {
        previewMedia = state;
        if (IsVisible) RefreshLivePreview();
    }

    private void RefreshLivePreview()
    {
        var hasMedia = settings.ShowMedia && previewMedia.Available;
        MediaPreviewCard.Visibility = hasMedia ? Visibility.Visible : Visibility.Collapsed;
        PreviewEmptyMedia.Visibility = settings.ShowMedia && !previewMedia.Available ? Visibility.Visible : Visibility.Collapsed;
        PreviewBackdrop.Visibility = hasMedia && settings.ArtworkBackground ? Visibility.Visible : Visibility.Collapsed;
        PreviewChevronRotation.Angle = settings.Position.StartsWith("Top", StringComparison.Ordinal) ? 0 : 180;
        if (IsVisible && hasMedia && previewMedia.IsPlaying && previewMedia.End > previewMedia.Start) previewClock.Start(); else previewClock.Stop();
        if (!IsVisible) return;
        PaintPreviewAudio();
        PreviewAppName.Text = string.IsNullOrWhiteSpace(previewMedia.AppName) ? Strings.T("Medya") : previewMedia.AppName;
        PreviewTrackTitle.Text = string.IsNullOrWhiteSpace(previewMedia.Title) ? Strings.T("Başlık yok") : previewMedia.Title;
        PreviewTrackTitle.ToolTip = previewMedia.Title;
        PreviewArtist.Text = previewMedia.Artist;
        PreviewArtist.ToolTip = previewMedia.Artist;
        PreviewPlayIcon.Text = previewMedia.IsPlaying ? "\uE769" : "\uE768";
        if (!ReferenceEquals(previewArtwork, previewMedia.Artwork))
        {
            previewArtwork = previewMedia.Artwork;
            PreviewCover.Background = previewArtwork is null ? (Brush)FindResource("SelectionBrush") : new ImageBrush(previewArtwork) { Stretch = Stretch.UniformToFill };
            PreviewBackdrop.Source = previewArtwork;
        }
        else if (previewArtwork is null) PreviewCover.SetResourceReference(System.Windows.Controls.Border.BackgroundProperty, "SelectionBrush");
        PreviewArtworkPlaceholder.Visibility = previewArtwork is null ? Visibility.Visible : Visibility.Collapsed;
        PaintPreviewTimeline();
    }

    private void PaintPreviewAudio()
    {
        PreviewVolumeValue.Text = previewAudio.Available ? previewAudio.Volume.ToString() : "—";
        PreviewVolumeIcon.Text = previewAudio.Muted || previewAudio.Volume == 0 ? "\uE74F" : previewAudio.Volume < 35 ? "\uE993" : "\uE767";
        PreviewVolumeSlider.MotionEnabled = settings.Animations && SystemParameters.ClientAreaAnimation;
        PreviewVolumeSlider.Value = previewAudio.Available ? previewAudio.Volume : 0;
        PreviewVolumeSlider.Opacity = previewAudio.Available && !previewAudio.Muted ? 1 : 0.4;
        PreviewVolumeCard.ToolTip = previewAudio.Available ? previewAudio.DeviceName : Strings.T("Ses aygıtı bekleniyor");
    }

    private void PaintPreviewTimeline()
    {
        if (!IsVisible || !previewMedia.Available) return;
        PreviewTimeline.MotionEnabled = false;
        PreviewTimeline.Minimum = previewMedia.Start.TotalSeconds;
        PreviewTimeline.Maximum = Math.Max(previewMedia.Start.TotalSeconds + 0.001, previewMedia.End.TotalSeconds);
        PreviewTimeline.Value = previewMedia.CurrentPosition(DateTimeOffset.UtcNow).TotalSeconds;
        var timed = previewMedia.End > previewMedia.Start;
        PreviewTimeline.Visibility = timed ? Visibility.Visible : Visibility.Hidden;
        PreviewPosition.Text = timed ? PreviewTime(previewMedia.CurrentPosition(DateTimeOffset.UtcNow) - previewMedia.Start) : Strings.T("Canlı");
        PreviewDuration.Text = timed ? PreviewTime(previewMedia.End - previewMedia.Start) : "";
    }

    private static string PreviewTime(TimeSpan value) => value.TotalHours >= 1 ? value.ToString(@"h\:mm\:ss") : value.ToString(@"m\:ss");
}
