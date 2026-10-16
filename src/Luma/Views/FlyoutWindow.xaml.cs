using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Luma.Models;
using Luma.Services;
using MediaState = Luma.Models.MediaState;

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
    private bool brightnessMode;
    private bool pinned;
    private bool restorePinnedVolume;
    private bool mediaExpanded;
    private bool seekDirty;
    private bool dismissing;
    private int revealRevision;
    private int layoutRevision;
    private double layoutScale;
    private ImageSource? paintedArtwork;
    private long inputTimestamp;
    private AudioState audioState;
    private BrightnessState brightnessState = BrightnessState.Unavailable();
    private MediaState media = MediaState.Empty;
    private MediaState[] previewSources = Array.Empty<MediaState>();
    private bool selectingSource;
    private string? pendingSourceId;
    private bool Motion => settings.Animations && SystemParameters.ClientAreaAnimation;
    public event Action<int>? VolumeRequested;
    public event Action<int>? BrightnessRequested;
    public event Action? MuteRequested;
    public event Action? SettingsRequested;
    public event Action<string, MediaCommand>? MediaRequested;
    public event Action<string, TimeSpan>? SeekRequested;
    public event Action<string?>? SourceRequested;
    internal event Action<double>? ResponseMeasured;

    public FlyoutWindow(AppSettings settings)
    {
        this.settings = settings;
        InitializeComponent();
        MonitorCatalog.Changed += OnDisplaysChanged;
        layoutScale = settings.FlyoutScale;
        FlyoutTransform.ScaleX = FlyoutTransform.ScaleY = layoutScale;
        Width = 396 * layoutScale; Height = 80 * layoutScale;
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
            if (brightnessMode && brightnessState.Available && BrightnessCard.IsMouseOver)
            {
                RequestBrightness(brightnessState.Value + (e.Delta > 0 ? 5 : -5));
                e.Handled = true; RestartTimer(); return;
            }
            if (!volumeMode || !audioState.Available || !VolumeCard.IsMouseOver) return;
            var value = Math.Clamp(audioState.Volume + (e.Delta > 0 ? settings.VolumeStep : -settings.VolumeStep), 0, 100);
            if (preview) { audioState = audioState with { Volume = value }; PaintVolume(); }
            else VolumeRequested?.Invoke(value);
            e.Handled = true;
            RestartTimer();
        };
    }

    public void Prepare()
    {
        // Build the native window, templates, glyphs and effects before the first key press.
        new WindowInteropHelper(this).EnsureHandle();
        var content = (FrameworkElement)Content;
        var previous = MediaCard.Visibility;
        MediaCard.Visibility = Visibility.Visible;
        var size = new Size(396 * settings.FlyoutScale, 310 * settings.FlyoutScale);
        content.Measure(size); content.Arrange(new System.Windows.Rect(size)); content.UpdateLayout();
        var cache = new RenderTargetBitmap((int)Math.Ceiling(size.Width), (int)Math.Ceiling(size.Height), 96, 96, PixelFormats.Pbgra32);
        cache.Render(content);
        MediaCard.Visibility = previous;
        content.Measure(new Size(Width, Height)); content.Arrange(new System.Windows.Rect(0, 0, Width, Height)); content.UpdateLayout();
    }

    internal void TrackInput(long timestamp)
    {
        if (ResponseMeasured is null) return;
        inputTimestamp = timestamp;
        CompositionTarget.Rendering -= MeasureResponse;
        CompositionTarget.Rendering += MeasureResponse;
    }
    private void MeasureResponse(object? sender, EventArgs e)
    {
        // Show() can run a layout/render pass before the window becomes visible.
        if (!IsVisible || inputTimestamp <= 0) return;
        CompositionTarget.Rendering -= MeasureResponse;
        ResponseMeasured?.Invoke(Stopwatch.GetElapsedTime(inputTimestamp).TotalMilliseconds);
        inputTimestamp = 0;
    }

    public void ShowVolume(AudioState state, bool isPreview = false)
    {
        restorePinnedVolume = false;
        if (preview != isPreview) { SourceMenu.IsOpen = false; media = MediaState.Empty; previewSources = Array.Empty<MediaState>(); selectingSource = false; }
        preview = isPreview; volumeMode = true; brightnessMode = false; audioState = state;
        PinMenu.IsEnabled = true;
        VolumeCard.Visibility = Visibility.Visible; LockCard.Visibility = BrightnessCard.Visibility = Visibility.Collapsed;
        PaintVolume(); UpdateMediaLayout(!IsVisible); Reveal();
    }

    internal void ShowPreview(MediaState sample)
    {
        ShowVolume(new(true, 40, false, "Önizleme · sistem sesi değişmez"), true);
        previewSources = new[] { sample, sample with { SessionId = sample.SessionId + "-alt", Title = "Afterglow", AppName = "Önizleme 2", IsPlaying = false, Position = TimeSpan.FromSeconds(32), End = TimeSpan.FromSeconds(180) } };
        media = sample with { Sources = PreviewSourceInfo() };
        PaintMedia(); UpdateMediaLayout(false);
    }

    public void RefreshVolume(AudioState state)
    {
        if (preview) return;
        audioState = state;
        if (IsVisible && volumeMode) PaintVolume();
    }

    internal void ShowBrightness(BrightnessState state, bool isPreview = false)
    {
        SourceMenu.IsOpen = false;
        if (volumeMode) restorePinnedVolume = pinned && IsVisible;
        volumeMode = false; brightnessMode = true; brightnessState = state;
        PinMenu.IsEnabled = false;
        // Preserve a pinned volume card's preview mode for its later return.
        brightnessPreview = isPreview;
        VolumeCard.Visibility = LockCard.Visibility = Visibility.Collapsed;
        BrightnessCard.Visibility = Visibility.Visible;
        PaintBrightness(); UpdateMediaLayout(!IsVisible); Reveal();
    }

    private bool brightnessPreview;
    internal void RefreshBrightness(BrightnessState state)
    {
        if (!brightnessMode || brightnessPreview) return;
        brightnessState = state; PaintBrightness();
    }

    private void PaintBrightness()
    {
        updating = true;
        BrightnessSlider.MotionEnabled = Motion;
        BrightnessSlider.IsEnabled = brightnessState.Available;
        BrightnessSlider.Value = brightnessState.Value;
        BrightnessValueText.Text = brightnessState.Available ? brightnessState.Value.ToString() : "—";
        BrightnessCard.ToolTip = brightnessPreview ? "Önizleme · ekran parlaklığı değişmez" : brightnessState.Status;
        updating = false;
    }

    private void RequestBrightness(int value)
    {
        value = brightnessState.Snap(value);
        if (brightnessPreview) { brightnessState = brightnessState with { Value = value }; PaintBrightness(); }
        else BrightnessRequested?.Invoke(value);
    }

    private void Brightness_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (updating || !brightnessMode || !brightnessState.Available) return;
        RequestBrightness((int)Math.Round(e.NewValue)); RestartTimer();
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
        BrightnessSlider.MotionEnabled = Motion;
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
        if (!ReferenceEquals(paintedArtwork, media.Artwork))
        {
            paintedArtwork = media.Artwork;
            ArtworkCover.Background = media.Artwork is null ? null : new ImageBrush(media.Artwork) { Stretch = Stretch.UniformToFill };
            ArtworkBackdrop.Source = media.Artwork;
        }
        ArtworkPlaceholder.Visibility = media.Artwork is null ? Visibility.Visible : Visibility.Collapsed;
        ArtworkBackdrop.Visibility = settings.ArtworkBackground ? Visibility.Visible : Visibility.Collapsed;
        PlayIcon.Text = media.IsPlaying ? "\uE769" : "\uE768";
        PreviousButton.IsEnabled = media.CanPrevious && !selectingSource;
        NextButton.IsEnabled = media.CanNext && !selectingSource;
        PlayButton.IsEnabled = media.CanToggle && !selectingSource;
        TimelineSlider.IsEnabled = media.CanSeek && !selectingSource;
        SourceButton.Visibility = media.Sources.Count > 1 || !media.AutomaticSource ? Visibility.Visible : Visibility.Collapsed;
        SourceButton.IsEnabled = !selectingSource;
        SourceButton.ToolTip = "Medya kaynağı · " + (media.AutomaticSource ? "Otomatik" : media.AppName);
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
        if (expanded == mediaExpanded && Math.Abs(layoutScale - settings.FlyoutScale) < 0.001) return;
        mediaExpanded = expanded;
        layoutScale = settings.FlyoutScale;
        FlyoutTransform.ScaleX = FlyoutTransform.ScaleY = layoutScale;
        Width = 396 * layoutScale;
        var revision = ++layoutRevision;
        var target = (expanded ? 310d : 80d) * layoutScale;
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
        SourceMenu.IsOpen = false;
        if (volumeMode) restorePinnedVolume = pinned && IsVisible;
        volumeMode = false;
        brightnessMode = false;
        PinMenu.IsEnabled = false;
        VolumeCard.Visibility = BrightnessCard.Visibility = Visibility.Collapsed; LockCard.Visibility = Visibility.Visible;
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
            AnimateCard(volumeMode ? VolumeCard : brightnessMode ? BrightnessCard : LockCard, 0);
            if (mediaExpanded) AnimateCard(MediaCard, 25);
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
        card.Opacity = 0.18;
        card.BeginAnimation(OpacityProperty, new DoubleAnimation(0.18, 1, TimeSpan.FromMilliseconds(160)) { BeginTime = begin });
        scale.BeginAnimation(ScaleTransform.ScaleXProperty, new DoubleAnimation(1, duration) { BeginTime = begin, EasingFunction = easing });
        scale.BeginAnimation(ScaleTransform.ScaleYProperty, new DoubleAnimation(1, duration) { BeginTime = begin, EasingFunction = easing });
        translate.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, duration) { BeginTime = begin, EasingFunction = easing });
    }

    private void AnimateUtilities(double opacity)
    {
        if (!Motion) { PinButton.Opacity = pinned ? 1 : opacity; SourceButton.Opacity = opacity; return; }
        PinButton.BeginAnimation(OpacityProperty, new DoubleAnimation(pinned ? 1 : opacity, TimeSpan.FromMilliseconds(140)));
        SourceButton.BeginAnimation(OpacityProperty, new DoubleAnimation(opacity, TimeSpan.FromMilliseconds(140)));
    }

    private void RestartTimer()
    {
        hideTimer.Stop();
        if (!IsVisible || (selectingSource && volumeMode) || (pinned && volumeMode) || IsMouseOver || IsMouseCaptureWithin || CardStack.ContextMenu.IsOpen || SourceMenu.IsOpen) return;
        hideTimer.Interval = TimeSpan.FromSeconds(settings.DurationSeconds); hideTimer.Start();
    }

    private void HideAnimated()
    {
        hideTimer.Stop();
        if ((selectingSource && volumeMode) || (pinned && volumeMode) || IsMouseOver || IsMouseCaptureWithin || CardStack.ContextMenu.IsOpen || SourceMenu.IsOpen) { RestartTimer(); return; }
        if (!volumeMode && restorePinnedVolume && pinned) { ShowVolume(audioState, preview); return; }
        Dismiss();
    }

    public void Dismiss()
    {
        SourceMenu.IsOpen = false;
        restorePinnedVolume = false;
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
        CommandHintText.Text = "Bu işlem şu anda kullanılamıyor.";
        CommandHint.Visibility = Visibility.Visible; hintTimer.Stop(); hintTimer.Start();
    }

    public void PlaceOnActiveMonitor()
    {
        PlaceCore();
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(PlaceCore));
    }
    private void OnDisplaysChanged()
    {
        if (!Dispatcher.HasShutdownStarted) Dispatcher.BeginInvoke(new Action(PlaceOnActiveMonitor));
    }
    private void PlaceCore()
    {
        if (!IsVisible) return;
        GetCursorPos(out var cursor);
        var monitor = MonitorCatalog.Resolve(settings.TargetDisplay, cursor.X, cursor.Y);
        var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
        if (!GetMonitorInfo(monitor, ref info)) return;
        var handle = new WindowInteropHelper(this).Handle;
        var dpi = GetDpiForWindow(handle); var scale = (dpi == 0 ? 96 : dpi) / 96.0;
        if (!GetWindowRect(handle, out var rectangle)) return;
        var width = rectangle.Right - rectangle.Left; var height = rectangle.Bottom - rectangle.Top;
        var gap = (int)Math.Round(12 * scale);
        var location = CalculateLocation(new System.Windows.Rect(info.Work.Left, info.Work.Top, info.Work.Right - info.Work.Left, info.Work.Bottom - info.Work.Top), new Size(width, height), gap, settings.Position);
        SetWindowPos(handle, (IntPtr)(-1), (int)location.X, (int)location.Y, 0, 0, 0x0001 | 0x0010 | 0x0200);
    }

    internal static System.Windows.Point CalculateLocation(System.Windows.Rect work, Size size, double gap, string position)
    {
        var x = position.EndsWith("Left", StringComparison.Ordinal) ? work.Left + gap : position.EndsWith("Right", StringComparison.Ordinal) ? work.Right - size.Width - gap : work.Left + (work.Width - size.Width) / 2;
        var y = position.StartsWith("Top", StringComparison.Ordinal) ? work.Top + gap : work.Bottom - size.Height - gap;
        return new System.Windows.Point(Math.Clamp(x, work.Left, Math.Max(work.Left, work.Right - size.Width)), Math.Clamp(y, work.Top, Math.Max(work.Top, work.Bottom - size.Height)));
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
        else SeekRequested?.Invoke(media.SessionId, position);
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
        else MediaRequested?.Invoke(media.SessionId, command);
        RestartTimer();
    }
    private void Play_Click(object sender, RoutedEventArgs e) => SendCommand(MediaCommand.TogglePlayback);
    private void Previous_Click(object sender, RoutedEventArgs e) => SendCommand(MediaCommand.Previous);
    private void Next_Click(object sender, RoutedEventArgs e) => SendCommand(MediaCommand.Next);

    private System.Collections.Generic.IReadOnlyList<MediaSourceInfo> PreviewSourceInfo() => Array.AsReadOnly(previewSources.Select(s => new MediaSourceInfo(s.SessionId, s.AppName, s.Title, s.Artist, s.IsPlaying)).ToArray());

    private void BuildSourceMenu()
    {
        SourceMenu.Items.Clear();
        var automatic = new MenuItem { Header = SourceHeader("Otomatik", "Çalan kaynağı takip et"), IsChecked = media.AutomaticSource };
        automatic.Click += (_, _) => SelectSource(null);
        SourceMenu.Items.Add(automatic);
        SourceMenu.Items.Add(new Separator());
        foreach (var source in media.Sources.OrderBy(s => s.AppName).ThenBy(s => s.Title))
        {
            var item = new MenuItem
            {
                Tag = source.Id, Header = SourceHeader(source.Title, source.AppName + (source.IsPlaying ? " · Çalıyor" : " · Duraklatıldı")),
                IsChecked = !media.AutomaticSource && media.SessionId == source.Id,
                ToolTip = source.AppName + " · " + source.Title + (string.IsNullOrWhiteSpace(source.Artist) ? "" : "\n" + source.Artist)
            };
            System.Windows.Automation.AutomationProperties.SetName(item, source.AppName + ": " + source.Title);
            item.Click += (_, _) => SelectSource(source.Id);
            SourceMenu.Items.Add(item);
        }
    }

    private static StackPanel SourceHeader(string title, string detail)
    {
        var header = new StackPanel { Width = 208 };
        header.Children.Add(new TextBlock { Text = title, FontSize = 12, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis });
        header.Children.Add(new TextBlock { Text = detail, FontSize = 10, Foreground = new SolidColorBrush(Color.FromRgb(165, 165, 176)), Margin = new Thickness(0, 4, 0, 0), TextTrimming = TextTrimming.CharacterEllipsis });
        return header;
    }

    private void Source_Click(object sender, RoutedEventArgs e)
    {
        BuildSourceMenu();
        SourceMenu.PlacementTarget = SourceButton;
        SourceMenu.Placement = settings.Position.StartsWith("Top", StringComparison.Ordinal) ? PlacementMode.Bottom : PlacementMode.Top;
        SourceMenu.IsOpen = true;
    }

    private void Sources_Opening(object sender, ContextMenuEventArgs e) => BuildSourceMenu();
    private void Sources_Opened(object sender, RoutedEventArgs e) { hideTimer.Stop(); RotateSourceArrow(90); }
    private void Sources_Closed(object sender, RoutedEventArgs e) { RotateSourceArrow(0); RestartTimer(); }
    private void RotateSourceArrow(double angle)
    {
        if (Motion) SourceArrowRotation.BeginAnimation(RotateTransform.AngleProperty, new DoubleAnimation(angle, TimeSpan.FromMilliseconds(140)));
        else { SourceArrowRotation.BeginAnimation(RotateTransform.AngleProperty, null); SourceArrowRotation.Angle = angle; }
    }

    private void SelectSource(string? id)
    {
        SourceMenu.IsOpen = false;
        if (preview)
        {
            var index = Array.FindIndex(previewSources, s => s.SessionId == media.SessionId);
            if (index >= 0) previewSources[index] = media;
            var target = id is null ? MediaService.ChooseSource(previewSources, null, null) : previewSources.FirstOrDefault(s => s.SessionId == id);
            if (target is null) return;
            media = target with { Sources = PreviewSourceInfo(), AutomaticSource = id is null, SampledAt = DateTimeOffset.UtcNow };
            PaintMedia(); RestartTimer();
            if (Motion) TrackTitleText.BeginAnimation(OpacityProperty, new DoubleAnimation(0.25, 1, TimeSpan.FromMilliseconds(240)));
            return;
        }
        selectingSource = true; pendingSourceId = id;
        PaintMedia(); hideTimer.Stop();
        SourceRequested?.Invoke(id);
    }

    public void ReportSourceSelection(string? id, bool success)
    {
        if (!selectingSource || id != pendingSourceId) return;
        selectingSource = false; pendingSourceId = null;
        PaintMedia(); RestartTimer();
        if (!success)
        {
            CommandHintText.Text = "Bu kaynak artık kullanılamıyor.";
            CommandHint.Visibility = Visibility.Visible; hintTimer.Stop(); hintTimer.Start();
        }
    }

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
    private void Settings_Click(object sender, RoutedEventArgs e) { SourceMenu.IsOpen = false; restorePinnedVolume = false; Hide(); hideTimer.Stop(); timelineTimer.Stop(); SettingsRequested?.Invoke(); }
    protected override void OnClosed(EventArgs e) { SourceMenu.IsOpen = false; MonitorCatalog.Changed -= OnDisplaysChanged; CompositionTarget.Rendering -= MeasureResponse; hideTimer.Stop(); timelineTimer.Stop(); hintTimer.Stop(); base.OnClosed(e); }
    private static string FormatTime(TimeSpan time) => time.TotalHours >= 1 ? time.ToString(@"h\:mm\:ss") : time.ToString(@"m\:ss");

    [StructLayout(LayoutKind.Sequential)] private struct Point { public int X; public int Y; }
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left; public int Top; public int Right; public int Bottom; }
    [StructLayout(LayoutKind.Sequential)] private struct MonitorInfo { public int Size; public Rect Monitor; public Rect Work; public uint Flags; }
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out Point point);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll")] private static extern uint GetDpiForWindow(IntPtr handle);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr handle, out Rect rectangle);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr window, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr window, int index, IntPtr value);
}
