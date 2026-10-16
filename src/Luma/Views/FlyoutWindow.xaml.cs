using System;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using Luma.Models;
using Luma.Services;

namespace Luma.Views;

public partial class FlyoutWindow : Window
{
    private readonly AppSettings settings;
    private readonly DispatcherTimer hideTimer = new();
    private readonly DispatcherTimer timelineTimer = new() { Interval = TimeSpan.FromMilliseconds(120) };
    private readonly DispatcherTimer hintTimer = new() { Interval = TimeSpan.FromSeconds(2) };
    private bool updating;
    private bool preview;
    private bool volumeMode;
    private bool pinned;
    private bool mediaExpanded;
    private bool seekDirty;
    private bool dismissing;
    private int revealRevision;
    private int layoutRevision;
    private AudioState audioState;
    private MediaState media = MediaState.Empty;
    private bool Motion => settings.Animations && SystemParameters.ClientAreaAnimation;
    public event Action<int>? VolumeRequested;
    public event Action? MuteRequested;
    public event Action? SettingsRequested;
    public event Action<MediaCommand>? MediaRequested;
    public event Action<TimeSpan>? SeekRequested;

    public FlyoutWindow(AppSettings settings)
    {
        this.settings = settings;
        InitializeComponent();
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            var style = GetWindowLongPtr(handle, -20).ToInt64();
            SetWindowLongPtr(handle, -20, (IntPtr)(style | 0x08000000 | 0x80));
        };
        hideTimer.Tick += (_, _) => HideAnimated();
        hintTimer.Tick += (_, _) => { hintTimer.Stop(); CommandHint.Visibility = Visibility.Collapsed; };
        timelineTimer.Tick += (_, _) => RefreshTimeline();
        MouseEnter += (_, _) => { hideTimer.Stop(); AnimateUtilities(1); };
        MouseLeave += (_, _) => { RestartTimer(); AnimateUtilities(0.45); };
        LostMouseCapture += (_, _) => RestartTimer();
        TimelineSlider.LostMouseCapture += (_, _) => { if (seekDirty) { seekDirty = false; SendSeek(); } };
        CardStack.ContextMenuOpening += (_, _) => hideTimer.Stop();
        CardStack.ContextMenuClosing += (_, _) => RestartTimer();
        SizeChanged += (_, _) => PlaceCore();
        PreviewMouseWheel += (_, e) =>
        {
            if (!volumeMode || !audioState.Available || !VolumeCard.IsMouseOver) return;
            var value = Math.Clamp(audioState.Volume + (e.Delta > 0 ? settings.VolumeStep : -settings.VolumeStep), 0, 100);
            if (preview) { audioState = audioState with { Volume = value }; PaintVolume(); }
            else VolumeRequested?.Invoke(value);
            e.Handled = true;
            RestartTimer();
        };
    }

    public void ShowVolume(AudioState state, bool isPreview = false)
    {
        if (preview != isPreview) media = MediaState.Empty;
        preview = isPreview; volumeMode = true; audioState = state;
        VolumeCard.Visibility = Visibility.Visible; LockCard.Visibility = Visibility.Collapsed;
        PaintVolume(); UpdateMediaLayout(!IsVisible); Reveal();
    }

    public void ShowPreview(MediaState sample)
    {
        ShowVolume(new(true, 40, false, "Önizleme · sistem sesi değişmez"), true);
        media = sample;
        PaintMedia(); UpdateMediaLayout(false);
    }

    public void RefreshVolume(AudioState state)
    {
        if (!IsVisible || !volumeMode || preview) return;
        audioState = state; PaintVolume();
    }

    private void PaintVolume()
    {
        updating = true;
        ValueText.Text = !audioState.Available ? "—" : audioState.Volume.ToString();
        VolumeIcon.Text = audioState.Muted || audioState.Volume == 0 ? "\uE74F" : audioState.Volume < 35 ? "\uE993" : "\uE767";
        VolumeSlider.MotionEnabled = Motion;
        VolumeSlider.IsEnabled = audioState.Available;
        VolumeSlider.Value = audioState.Volume;
        VolumeSlider.Opacity = audioState.Muted ? 0.4 : 1;
        VolumeCard.ToolTip = preview ? "Önizleme · sistem sesi değişmez" : audioState.DeviceName + " · Ayarlar için sağ tıkla";
        MuteButton.IsEnabled = audioState.Available;
        updating = false;
    }

    public void UpdateMedia(MediaState state)
    {
        if (preview) return;
        var changedTrack = media.SessionId != state.SessionId || media.Title != state.Title;
        media = state;
        PaintMedia();
        UpdateMediaLayout(!IsVisible);
        if (changedTrack && IsVisible && mediaExpanded && Motion)
        {
            TrackTitleText.BeginAnimation(OpacityProperty, new DoubleAnimation(0.25, 1, TimeSpan.FromMilliseconds(240)));
            ArtworkCover.BeginAnimation(OpacityProperty, new DoubleAnimation(0.3, 1, TimeSpan.FromMilliseconds(300)));
        }
    }

    public void ApplyPreferences()
    {
        VolumeSlider.MotionEnabled = Motion;
        TimelineSlider.MotionEnabled = false;
        ArtworkBackdrop.Visibility = settings.ArtworkBackground ? Visibility.Visible : Visibility.Collapsed;
        UpdateMediaLayout(!IsVisible);
        RestartTimer();
    }

    private void PaintMedia()
    {
        updating = true;
        AppNameText.Text = media.AppName;
        TrackTitleText.Text = media.Title; TrackTitleText.ToolTip = media.Title;
        ArtistText.Text = media.Artist;
        ArtworkCover.Background = media.Artwork is null ? null : new ImageBrush(media.Artwork) { Stretch = Stretch.UniformToFill };
        ArtworkPlaceholder.Visibility = media.Artwork is null ? Visibility.Visible : Visibility.Collapsed;
        ArtworkBackdrop.Source = media.Artwork;
        ArtworkBackdrop.Visibility = settings.ArtworkBackground ? Visibility.Visible : Visibility.Collapsed;
        PlayIcon.Text = media.IsPlaying ? "\uE769" : "\uE768";
        PreviousButton.IsEnabled = media.CanPrevious;
        NextButton.IsEnabled = media.CanNext;
        PlayButton.IsEnabled = media.CanToggle;
        TimelineSlider.IsEnabled = media.CanSeek;
        TimelineSlider.MotionEnabled = false;
        TimelineSlider.Minimum = media.Start.TotalSeconds;
        TimelineSlider.Maximum = Math.Max(media.Start.TotalSeconds + 0.001, media.End.TotalSeconds);
        EndText.Text = FormatTime(media.End - media.Start);
        updating = false;
        RefreshTimeline();
    }

    private void RefreshTimeline()
    {
        if (TimelineSlider.IsMouseCaptureWithin || seekDirty) return;
        updating = true;
        var position = media.CurrentPosition(DateTimeOffset.UtcNow);
        TimelineSlider.Value = position.TotalSeconds;
        PositionText.Text = FormatTime(position - media.Start);
        updating = false;
    }

    private void UpdateMediaLayout(bool immediate)
    {
        var expanded = volumeMode && settings.ShowMedia && media.Available;
        if (expanded && IsVisible) timelineTimer.Start(); else timelineTimer.Stop();
        if (expanded == mediaExpanded) return;
        mediaExpanded = expanded;
        var revision = ++layoutRevision;
        var target = expanded ? 286d : 80d;
        var from = ActualHeight > 0 ? ActualHeight : Height;
        BeginAnimation(HeightProperty, null);
        Height = target;
        if (expanded) MediaCard.Visibility = Visibility.Visible;
        if (!IsVisible || immediate || !Motion)
        {
            MediaCard.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
            MediaCard.BeginAnimation(OpacityProperty, null); MediaCard.Opacity = 1;
            PlaceOnActiveMonitor(); return;
        }
        var resize = new DoubleAnimation(from, target, TimeSpan.FromMilliseconds(240)) { EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut } };
        resize.Completed += (_, _) => { if (revision == layoutRevision && !mediaExpanded) MediaCard.Visibility = Visibility.Collapsed; };
        BeginAnimation(HeightProperty, resize);
        if (expanded) AnimateCard(MediaCard, 40);
        else MediaCard.BeginAnimation(OpacityProperty, new DoubleAnimation(0, TimeSpan.FromMilliseconds(110)));
    }

    public void ShowLock(string name, bool enabled)
    {
        preview = false; volumeMode = false;
        VolumeCard.Visibility = Visibility.Collapsed; LockCard.Visibility = Visibility.Visible;
        StatusText.Text = name; LockStateText.Text = enabled ? "Açık" : "Kapalı";
        LockIcon.Text = enabled ? "\uE72E" : "\uE785";
        UpdateMediaLayout(!IsVisible); Reveal();
    }

    private void Reveal()
    {
        var entering = !IsVisible || dismissing;
        dismissing = false; ++revealRevision;
        CardStack.BeginAnimation(OpacityProperty, null); CardStack.Opacity = 1;
        if (!IsVisible) Show();
        PlaceOnActiveMonitor();
        if (entering && Motion)
        {
            AnimateCard(volumeMode ? VolumeCard : LockCard, 0);
            if (mediaExpanded) AnimateCard(MediaCard, 45);
        }
        if (mediaExpanded) timelineTimer.Start(); else timelineTimer.Stop();
        RestartTimer();
    }

    private void AnimateCard(FrameworkElement card, int delay)
    {
        var scale = new ScaleTransform(0.985, 0.985);
        var translate = new TranslateTransform(0, settings.Position.StartsWith("Top", StringComparison.Ordinal) ? -7 : 7);
        var transforms = new TransformGroup(); transforms.Children.Add(scale); transforms.Children.Add(translate);
        card.RenderTransform = transforms;
        var duration = TimeSpan.FromMilliseconds(260);
        var easing = new CubicEase { EasingMode = EasingMode.EaseOut };
        var begin = TimeSpan.FromMilliseconds(delay);
        card.Opacity = 0;
        card.BeginAnimation(OpacityProperty, new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(190)) { BeginTime = begin });
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, duration) { BeginTime = begin, EasingFunction = easing });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, duration) { BeginTime = begin, EasingFunction = easing });
        translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, duration) { BeginTime = begin, EasingFunction = easing });
    }

    private void AnimateUtilities(double opacity)
    {
        if (!Motion) { MediaSettingsButton.Opacity = PinButton.Opacity = opacity; return; }
        MediaSettingsButton.BeginAnimation(OpacityProperty, new DoubleAnimation(opacity, TimeSpan.FromMilliseconds(140)));
        PinButton.BeginAnimation(OpacityProperty, new DoubleAnimation(pinned ? 1 : opacity, TimeSpan.FromMilliseconds(140)));
    }

    private void RestartTimer()
    {
        hideTimer.Stop();
        if (!IsVisible || pinned || IsMouseOver || IsMouseCaptureWithin || CardStack.ContextMenu.IsOpen) return;
        hideTimer.Interval = TimeSpan.FromSeconds(settings.DurationSeconds); hideTimer.Start();
    }

    private void HideAnimated()
    {
        hideTimer.Stop();
        if (pinned || IsMouseOver || IsMouseCaptureWithin || CardStack.ContextMenu.IsOpen) { RestartTimer(); return; }
        Dismiss();
    }

    public void Dismiss()
    {
        hideTimer.Stop(); timelineTimer.Stop();
        if (!Motion) { Hide(); return; }
        dismissing = true;
        var revision = ++revealRevision;
        var fade = new DoubleAnimation(0, TimeSpan.FromMilliseconds(150));
        fade.Completed += (_, _) => { if (revision == revealRevision && dismissing) { Hide(); dismissing = false; } };
        CardStack.BeginAnimation(OpacityProperty, fade);
    }

    public void ReportMediaResult(bool success)
    {
        if (success) return;
        CommandHint.Visibility = Visibility.Visible; hintTimer.Stop(); hintTimer.Start();
    }

    public void PlaceOnActiveMonitor()
    {
        PlaceCore();
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(PlaceCore));
    }
    private void PlaceCore()
    {
        if (!IsVisible) return;
        GetCursorPos(out var cursor);
        var monitor = MonitorFromPoint(cursor, 2);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info)) return;
        var handle = new WindowInteropHelper(this).Handle;
        var dpi = GetDpiForWindow(handle); var scale = (dpi == 0 ? 96 : dpi) / 96.0;
        if (!GetWindowRect(handle, out var rectangle)) return;
        var width = rectangle.Right - rectangle.Left; var height = rectangle.Bottom - rectangle.Top;
        var gap = (int)Math.Round(16 * scale);
        var right = settings.Position.EndsWith("Right", StringComparison.Ordinal);
        var top = settings.Position.StartsWith("Top", StringComparison.Ordinal);
        var x = right ? info.Work.Right - width - gap : info.Work.Left + (info.Work.Right - info.Work.Left - width) / 2;
        var y = top ? info.Work.Top + gap : info.Work.Bottom - height - gap;
        SetWindowPos(handle, (IntPtr)(-1), x, y, 0, 0, 0x0001 | 0x0010 | 0x0200);
    }

    private void Volume_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (updating || !volumeMode) return;
        if (preview) { audioState = audioState with { Volume = (int)Math.Round(e.NewValue), Muted = false }; PaintVolume(); }
        else VolumeRequested?.Invoke((int)Math.Round(e.NewValue));
        RestartTimer();
    }
    private void Mute_Click(object sender, RoutedEventArgs e)
    {
        if (!volumeMode) return;
        if (preview) { audioState = audioState with { Muted = !audioState.Muted }; PaintVolume(); }
        else MuteRequested?.Invoke();
        RestartTimer();
    }
    private void Timeline_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (updating || !media.CanSeek) return;
        PositionText.Text = FormatTime(TimeSpan.FromSeconds(e.NewValue) - media.Start);
        if (TimelineSlider.IsMouseCaptureWithin) seekDirty = true;
        else SendSeek();
    }
    private void Timeline_Released(object sender, MouseButtonEventArgs e) { if (seekDirty) { seekDirty = false; SendSeek(); } }
    private void SendSeek()
    {
        var position = TimeSpan.FromSeconds(TimelineSlider.Value);
        if (preview) media = media with { Position = position, SampledAt = DateTimeOffset.UtcNow };
        else SeekRequested?.Invoke(position);
        RestartTimer();
    }
    private void SendCommand(MediaCommand command)
    {
        if (preview)
        {
            var position = media.CurrentPosition(DateTimeOffset.UtcNow);
            media = command == MediaCommand.TogglePlayback ? media with { IsPlaying = !media.IsPlaying, Position = position, SampledAt = DateTimeOffset.UtcNow }
                : media with { Position = TimeSpan.Zero, SampledAt = DateTimeOffset.UtcNow };
            PaintMedia();
        }
        else MediaRequested?.Invoke(command);
        RestartTimer();
    }
    private void Play_Click(object sender, RoutedEventArgs e) => SendCommand(MediaCommand.TogglePlayback);
    private void Previous_Click(object sender, RoutedEventArgs e) => SendCommand(MediaCommand.Previous);
    private void Next_Click(object sender, RoutedEventArgs e) => SendCommand(MediaCommand.Next);
    private void Pin_Click(object sender, RoutedEventArgs e)
    {
        SetPinned(!pinned);
    }
    private void SetPinned(bool value)
    {
        pinned = value; PinMenu.IsChecked = pinned;
        PinIcon.Text = pinned ? "\uE77A" : "\uE718";
        PinButton.ToolTip = pinned ? "Otomatik gizle" : "Açık tut";
        PinButton.Opacity = pinned ? 1 : 0.45; RestartTimer();
    }
    private void Dismiss_Click(object sender, RoutedEventArgs e) { SetPinned(false); Dismiss(); }
    private void Settings_Click(object sender, RoutedEventArgs e) { Hide(); hideTimer.Stop(); timelineTimer.Stop(); SettingsRequested?.Invoke(); }
    protected override void OnClosed(EventArgs e) { hideTimer.Stop(); timelineTimer.Stop(); hintTimer.Stop(); base.OnClosed(e); }
    private static string FormatTime(TimeSpan time) => time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"m\:ss");

    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left; public int Top; public int Right; public int Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public Rect Monitor; public Rect Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromPoint(Point point, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr handle);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr handle, out Rect rectangle);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);
}
