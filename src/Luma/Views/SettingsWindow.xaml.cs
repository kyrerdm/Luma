using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Media;
using Luma.Models;
using Luma.Services;
using Luma.Themes;

namespace Luma.Views;

public partial class SettingsWindow : Window
{
    private readonly AppSettings settings;
    private readonly Action save;
    private bool ready;
    private bool updatingBrightness;
    public event Action<int>? BrightnessRequested;
    public bool AllowClose { get; set; }

    public SettingsWindow(AppSettings settings, Action save)
    {
        this.settings = settings;
        this.save = save;
        InitializeComponent();
        VersionText.Text = AppVersion.Current.ToString(3);
        if (string.Equals(Environment.ProcessPath, InstallService.Plan(InstallService.DefaultRoot, AppVersion.Current).Executable, StringComparison.OrdinalIgnoreCase)) InstallButton.Content = Strings.T("Kurulumu onar");
        foreach (var option in Palette.Options) AccentCombo.Items.Add(new ComboBoxItem { Content = option, Tag = option.Id });
        AccentCombo.Items.Add(new ComboBoxItem { Content = "Özel renk", Tag = "Custom" });
        ReloadPreferences();
        GeneralNav.IsChecked = true;
        MonitorCatalog.Changed += OnDisplaysChanged;
        Strings.Changed += OnLanguageChanged;
        Loaded += (_, _) => { Strings.Apply(this); RefreshLivePreview(); };
        IsVisibleChanged += (_, _) => RefreshLivePreview();
        previewClock.Tick += (_, _) => PaintPreviewTimeline();
        ready = true;
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            var dark = Appearance.IsLight ? 0 : 1; DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
            var round = 2; DwmSetWindowAttribute(handle, 33, ref round, sizeof(int));
            var chrome = new System.Windows.Shell.WindowChrome { CaptionHeight = 46, ResizeBorderThickness = new Thickness(6), CornerRadius = new CornerRadius(12), GlassFrameThickness = new Thickness(0), UseAeroCaptionButtons = false };
            System.Windows.Shell.WindowChrome.SetWindowChrome(this, chrome);
        };
    }

    private void ReloadPreferences()
    {
        var previous = ready;
        ready = false;
        CustomColorText.Text = settings.CustomAccent;
        OpacitySlider.Value = settings.SurfaceOpacity;
        FullscreenToggle.IsChecked = settings.HideInFullscreen;
        ExternalToggle.IsChecked = settings.ExternalBrightness;
        foreach (ComboBoxItem item in ThemeCombo.Items) if ((string)item.Tag == settings.Theme) ThemeCombo.SelectedItem = item;
        VolumeToggle.IsChecked = settings.VolumeKeys;
        LocksToggle.IsChecked = settings.LockKeys;
        ObserveToggle.IsChecked = settings.ObserveVolume;
        BrightnessToggle.IsChecked = settings.ObserveBrightness;
        NativeBrightnessToggle.IsChecked = settings.HideNativeBrightness;
        AnimationsToggle.IsChecked = settings.Animations;
        MediaToggle.IsChecked = settings.ShowMedia;
        ArtworkToggle.IsChecked = settings.ArtworkBackground;
        StartupToggle.IsChecked = StartupService.IsEnabled();
        DurationSlider.Value = settings.DurationSeconds;
        StepSlider.Value = settings.VolumeStep;
        ScaleSlider.Value = settings.FlyoutScale;
        foreach (ComboBoxItem item in PositionCombo.Items) if ((string)item.Tag == settings.Position) PositionCombo.SelectedItem = item;
        foreach (ComboBoxItem item in AccentCombo.Items) if ((string)item.Tag == settings.Accent) AccentCombo.SelectedItem = item;
        ReloadMonitors();
        foreach (ComboBoxItem item in LanguageCombo.Items) if ((string)item.Tag == settings.Language) LanguageCombo.SelectedItem = item;
        UpdatePreview();
        UpdateLabels();
        ready = previous;
    }

    public void SetStatus(string text, bool healthy)
    {
        ServiceStatus.Text = text;
        StatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(healthy ? "#9BD6AD" : "#E2B676"));
    }
    internal void ApplyAppearance() { var handle = new WindowInteropHelper(this).Handle; if (handle != IntPtr.Zero) { var dark = Appearance.IsLight ? 0 : 1; DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int)); } }

    internal void SetBrightness(BrightnessState state, bool writeFailed = false)
    {
        updatingBrightness = true;
        DisplayBrightnessSlider.IsEnabled = state.Available;
        DisplayBrightnessSlider.Value = state.Value;
        DisplayBrightnessText.Text = state.Available ? $"%{state.Value}" : "—";
        BrightnessStatusText.Text = Strings.T(writeFailed ? "Parlaklık değiştirilemedi. Ekran bağlantısını kontrol edip tekrar dene." : state.Status);
        updatingBrightness = false;
    }

    private void DisplayBrightness_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!ready || updatingBrightness || !DisplayBrightnessSlider.IsEnabled) return;
        DisplayBrightnessText.Text = $"%{e.NewValue:0}";
        BrightnessRequested?.Invoke((int)Math.Round(e.NewValue));
    }

    private void Preference_Changed(object sender, RoutedEventArgs e)
    {
        if (!ready) return;
        settings.VolumeKeys = VolumeToggle.IsChecked == true;
        settings.LockKeys = LocksToggle.IsChecked == true;
        settings.ObserveVolume = ObserveToggle.IsChecked == true;
        settings.ObserveBrightness = BrightnessToggle.IsChecked == true;
        settings.HideNativeBrightness = NativeBrightnessToggle.IsChecked == true;
        settings.Animations = AnimationsToggle.IsChecked == true;
        settings.ShowMedia = MediaToggle.IsChecked == true;
        settings.ArtworkBackground = ArtworkToggle.IsChecked == true;
        settings.HideInFullscreen = FullscreenToggle.IsChecked == true;
        UpdatePreview();
        Save();
    }
    private void Position_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!ready || PositionCombo.SelectedItem is not ComboBoxItem item) return;
        settings.Position = (string)item.Tag; Save();
    }
    private void Accent_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!ready || AccentCombo.SelectedItem is not ComboBoxItem item) return;
        settings.Accent = (string)item.Tag; Palette.Apply(settings); ApplyAppearance(); Save();
    }
    private void Theme_Changed(object sender, SelectionChangedEventArgs e) { if (ready && ThemeCombo.SelectedItem is ComboBoxItem item) { settings.Theme = (string)item.Tag; Palette.Apply(settings); ApplyAppearance(); Save(); } }
    private void CustomColor_Click(object sender, RoutedEventArgs e)
    {
        var color = CustomColorText.Text.Trim();
        if (!Palette.ValidCustomColor(color)) { ColorStatusText.Text = Strings.T("# ile başlayan altı haneli renk kodu gir."); return; }
        settings.CustomAccent = color.ToUpperInvariant(); settings.Accent = "Custom";
        foreach (ComboBoxItem item in AccentCombo.Items) if ((string)item.Tag == "Custom") AccentCombo.SelectedItem = item;
        Palette.Apply(settings); Save(); ColorStatusText.Text = Strings.T("Özel rengin uygulandı.");
    }
    private void Opacity_Changed(object sender, RoutedPropertyChangedEventArgs<double> e) { if (ready) { settings.SurfaceOpacity = e.NewValue; Palette.Apply(settings); UpdateLabels(); Save(); } }
    private void UpdatePreview()
    {
        RefreshLivePreview();
    }
    private void PreviewCard_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (MediaPreviewCard.Child is Grid surface) surface.Clip = new RectangleGeometry(new Rect(0, 0, Math.Max(0, e.NewSize.Width - 2), Math.Max(0, e.NewSize.Height - 2)), 9, 9);
    }
    private void ReloadMonitors()
    {
        var previous = ready; ready = false;
        MonitorCombo.Items.Clear();
        MonitorCombo.Items.Add(new ComboBoxItem { Content = "İmlecin olduğu ekran", Tag = "Cursor" });
        MonitorCombo.Items.Add(new ComboBoxItem { Content = "Ana ekran", Tag = "Primary" });
        foreach (var display in MonitorCatalog.Current) MonitorCombo.Items.Add(new ComboBoxItem { Content = display.Label, Tag = display.Id, ToolTip = display.Description });
        ComboBoxItem? selected = null;
        foreach (ComboBoxItem item in MonitorCombo.Items) if ((string)item.Tag == settings.TargetDisplay) selected = item;
        if (selected is null)
        {
            selected = new ComboBoxItem { Content = "Ekran bağlı değil", Tag = settings.TargetDisplay, ToolTip = "Seçtiğin ekran yeniden bağlanana kadar ana ekran kullanılır." };
            MonitorCombo.Items.Add(selected);
        }
        MonitorCombo.SelectedItem = selected;
        Strings.Apply(MonitorCombo);
        ready = previous;
    }
    private void OnDisplaysChanged()
    {
        if (!Dispatcher.HasShutdownStarted) Dispatcher.BeginInvoke(new Action(ReloadMonitors));
    }
    private void Monitor_Opened(object? sender, EventArgs e) => MonitorCatalog.Refresh();
    private void Monitor_Changed(object sender, SelectionChangedEventArgs e)
    {
        if (!ready || MonitorCombo.SelectedItem is not ComboBoxItem item) return;
        settings.TargetDisplay = (string)item.Tag; Save();
    }
    private void Duration_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!ready) return;
        settings.DurationSeconds = e.NewValue; UpdateLabels(); Save();
    }
    private void Step_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!ready) return;
        settings.VolumeStep = (int)Math.Round(e.NewValue); UpdateLabels(); Save();
    }
    private void Scale_Changed(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (!ready) return;
        settings.FlyoutScale = e.NewValue; UpdateLabels(); Save();
    }
    private void UpdateLabels() { DurationText.Text = $"{settings.DurationSeconds:0.#} " + (Strings.Language == "en" ? "s" : "sn"); StepText.Text = $"%{settings.VolumeStep}"; ScaleText.Text = $"%{settings.FlyoutScale * 100:0}"; OpacityText.Text = $"%{settings.SurfaceOpacity * 100:0}"; }
    private void Save()
    {
        try { save(); RefreshLivePreview(); FeedbackText.Text = Strings.T("Kaydedildi. Tercihlerin bir sonraki açılışta da burada."); }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException) { FeedbackText.Text = "Ayarlar kaydedilemedi: " + ex.Message; }
    }
    private void Startup_Changed(object sender, RoutedEventArgs e)
    {
        if (!ready) return;
        try { StartupService.SetEnabled(StartupToggle.IsChecked == true); FeedbackText.Text = Strings.T("Başlangıç tercihin kaydedildi."); }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.IO.IOException or System.Security.SecurityException or InvalidOperationException)
        {
            ready = false; StartupToggle.IsChecked = StartupService.IsEnabled(); ready = true;
            FeedbackText.Text = "Başlangıç ayarı değiştirilemedi: " + ex.Message;
        }
    }
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Close_Click(object sender, RoutedEventArgs e) => Hide();
    protected override void OnClosing(CancelEventArgs e) { if (!AllowClose) { e.Cancel = true; Hide(); } base.OnClosing(e); }
    protected override void OnClosed(EventArgs e) { previewClock.Stop(); updateCancellation?.Cancel(); MonitorCatalog.Changed -= OnDisplaysChanged; Strings.Changed -= OnLanguageChanged; base.OnClosed(e); }
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr handle, int attribute, ref int value, int size);
}
