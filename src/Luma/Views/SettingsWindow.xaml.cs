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
        foreach (var option in Palette.Options) AccentCombo.Items.Add(new ComboBoxItem { Content = option, Tag = option.Id });
        foreach (ComboBoxItem item in PositionCombo.Items) if ((string)item.Tag == settings.Position) PositionCombo.SelectedItem = item;
        foreach (ComboBoxItem item in AccentCombo.Items) if ((string)item.Tag == settings.Accent) AccentCombo.SelectedItem = item;
        ReloadMonitors();
        MonitorCatalog.Changed += OnDisplaysChanged;
        var sample = PreviewData.Artwork();
        PreviewBackdrop.Source = sample;
        PreviewCover.Background = new ImageBrush(sample) { Stretch = Stretch.UniformToFill };
        UpdatePreview();
        UpdateLabels();
        ready = true;
        SourceInitialized += (_, _) =>
        {
            var handle = new WindowInteropHelper(this).Handle;
            var dark = 1; DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
            var round = 2; DwmSetWindowAttribute(handle, 33, ref round, sizeof(int));
            var chrome = new System.Windows.Shell.WindowChrome { CaptionHeight = 46, ResizeBorderThickness = new Thickness(6), CornerRadius = new CornerRadius(12), GlassFrameThickness = new Thickness(0), UseAeroCaptionButtons = false };
            System.Windows.Shell.WindowChrome.SetWindowChrome(this, chrome);
        };
    }

    public void SetStatus(string text, bool healthy)
    {
        ServiceStatus.Text = text;
        StatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(healthy ? "#9BD6AD" : "#E2B676"));
    }

    internal void SetBrightness(BrightnessState state, bool writeFailed = false)
    {
        updatingBrightness = true;
        DisplayBrightnessSlider.IsEnabled = state.Available;
        DisplayBrightnessSlider.Value = state.Value;
        DisplayBrightnessText.Text = state.Available ? $"%{state.Value}" : "—";
        BrightnessStatusText.Text = writeFailed ? "Parlaklık değiştirilemedi. Ekran bağlantısını kontrol edip tekrar dene." : state.Status;
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
        settings.Accent = (string)item.Tag; Palette.Apply(settings.Accent); Save();
    }
    private void UpdatePreview()
    {
        MediaPreviewCard.Visibility = settings.ShowMedia ? Visibility.Visible : Visibility.Collapsed;
        PreviewBackdrop.Visibility = settings.ArtworkBackground ? Visibility.Visible : Visibility.Collapsed;
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
    private void UpdateLabels() { DurationText.Text = $"{settings.DurationSeconds:0.#} sn"; StepText.Text = $"%{settings.VolumeStep}"; ScaleText.Text = $"%{settings.FlyoutScale * 100:0}"; }
    private void Save()
    {
        try { save(); FeedbackText.Text = "Kaydedildi. Tercihlerin bir sonraki açılışta da burada."; }
        catch (Exception ex) when (ex is System.IO.IOException or UnauthorizedAccessException) { FeedbackText.Text = "Ayarlar kaydedilemedi: " + ex.Message; }
    }
    private void Startup_Changed(object sender, RoutedEventArgs e)
    {
        if (!ready) return;
        try { StartupService.SetEnabled(StartupToggle.IsChecked == true); FeedbackText.Text = "Başlangıç tercihin kaydedildi."; }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.IO.IOException or System.Security.SecurityException or InvalidOperationException)
        {
            ready = false; StartupToggle.IsChecked = StartupService.IsEnabled(); ready = true;
            FeedbackText.Text = "Başlangıç ayarı değiştirilemedi: " + ex.Message;
        }
    }
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Close_Click(object sender, RoutedEventArgs e) => Hide();
    protected override void OnClosing(CancelEventArgs e) { if (!AllowClose) { e.Cancel = true; Hide(); } base.OnClosing(e); }
    protected override void OnClosed(EventArgs e) { MonitorCatalog.Changed -= OnDisplaysChanged; base.OnClosed(e); }
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr handle, int attribute, ref int value, int size);
}
