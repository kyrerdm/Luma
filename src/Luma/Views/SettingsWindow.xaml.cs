using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
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
    private readonly Action preview;
    private bool ready;
    public bool AllowClose { get; set; }

    public SettingsWindow(AppSettings settings, Action save, Action preview)
    {
        this.settings = settings;
        this.save = save;
        this.preview = preview;
        InitializeComponent();
        VolumeToggle.IsChecked = settings.VolumeKeys;
        LocksToggle.IsChecked = settings.LockKeys;
        ObserveToggle.IsChecked = settings.ObserveVolume;
        AnimationsToggle.IsChecked = settings.Animations;
        MediaToggle.IsChecked = settings.ShowMedia;
        ArtworkToggle.IsChecked = settings.ArtworkBackground;
        StartupToggle.IsChecked = StartupService.IsEnabled();
        DurationSlider.Value = settings.DurationSeconds;
        StepSlider.Value = settings.VolumeStep;
        foreach (ComboBoxItem item in PositionCombo.Items) if ((string)item.Tag == settings.Position) PositionCombo.SelectedItem = item;
        foreach (ComboBoxItem item in AccentCombo.Items) if ((string)item.Tag == settings.Accent) AccentCombo.SelectedItem = item;
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
            var chrome = new System.Windows.Shell.WindowChrome { CaptionHeight = 0, ResizeBorderThickness = new Thickness(6), CornerRadius = new CornerRadius(12), GlassFrameThickness = new Thickness(0) };
            System.Windows.Shell.WindowChrome.SetWindowChrome(this, chrome);
        };
    }

    public void SetStatus(string text, bool healthy)
    {
        ServiceStatus.Text = text;
        StatusDot.Fill = new SolidColorBrush((Color)ColorConverter.ConvertFromString(healthy ? "#9BD6AD" : "#E2B676"));
    }

    private void Preference_Changed(object sender, RoutedEventArgs e)
    {
        if (!ready) return;
        settings.VolumeKeys = VolumeToggle.IsChecked == true;
        settings.LockKeys = LocksToggle.IsChecked == true;
        settings.ObserveVolume = ObserveToggle.IsChecked == true;
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
    private void UpdateLabels() { DurationText.Text = $"{settings.DurationSeconds:0.#} sn"; StepText.Text = $"%{settings.VolumeStep}"; }
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
    private void Preview_Click(object sender, RoutedEventArgs e) => preview();
    private void Title_Drag(object sender, MouseButtonEventArgs e) { if (e.ChangedButton == MouseButton.Left) DragMove(); }
    private void Minimize_Click(object sender, RoutedEventArgs e) => WindowState = WindowState.Minimized;
    private void Close_Click(object sender, RoutedEventArgs e) => Hide();
    protected override void OnClosing(CancelEventArgs e) { if (!AllowClose) { e.Cancel = true; Hide(); } base.OnClosing(e); }
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr handle, int attribute, ref int value, int size);
}
