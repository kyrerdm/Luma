using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Windows;
using System.Windows.Threading;
using System.Threading.Tasks;
using Luma.Models;
using Luma.Services;
using Luma.Views;
using Luma.Themes;
using Forms = System.Windows.Forms;

namespace Luma;

public sealed partial class AppController : IDisposable
{
    private readonly SettingsStore store;
    private readonly AppSettings settings;
    private readonly BackgroundAudioService audio;
    private readonly BackgroundAudioRouting routing;
    private readonly DispatcherTimer routingTimer = new() { Interval = TimeSpan.FromSeconds(1.5) };
    private bool refreshingRouting;
    private readonly BackgroundBrightnessService brightness;
    private readonly NativeBrightnessOsdSuppressor nativeBrightnessOsd;
    private readonly MediaService media;
    private readonly KeyboardService? keyboard;
    private readonly FlyoutWindow flyout;
    private readonly SettingsWindow window;
    private readonly Forms.NotifyIcon tray;
    private readonly Icon trayIcon;
    private readonly string? hookError;
    private AudioState lastState = new(false, 0, false, "Ses aygıtı bekleniyor");
    private long pendingAudioSequence;
    private long pendingBrightnessSequence;
    private BrightnessState lastBrightness = BrightnessState.Unavailable("Parlaklık desteği kontrol ediliyor…");
    private bool disposed;
    internal Task<AudioState> AudioReady => audio.Ready;
    internal Task<BrightnessState> BrightnessReady => brightness.Ready;
    internal FlyoutWindow DiagnosticFlyout => flyout;
    internal SettingsWindow DiagnosticSettings => window;
    internal Forms.ContextMenuStrip DiagnosticTrayMenu => tray.ContextMenuStrip!;
    internal Task FlushAudioAsync() => audio.FlushAsync();
    internal Task FlushBrightnessAsync() => brightness.FlushAsync();

    public AppController(string dataDirectory) : this(dataDirectory, null) { }

    internal AppController(string dataDirectory, Func<IAudioDevice>? audioFactory, Func<IBrightnessDevice>? brightnessFactory = null)
    {
        store = new(dataDirectory);
        settings = store.Load();
        Strings.SetLanguage(settings.Language);
        Palette.Apply(settings);
        flyout = new(settings);
        window = new(settings, SaveSettings);
        flyout.Prepare();
        audio = new(Application.Current.Dispatcher, audioFactory);
        audio.Changed += OnAudioChanged;
        routing = new(Application.Current.Dispatcher);
        flyout.AudioPanelOpenChanged += async open => { if (open) { await RefreshRouting(); if (!disposed && flyout.AudioPanelOpen) routingTimer.Start(); } else routingTimer.Stop(); };
        routingTimer.Tick += async (_, _) => await RefreshRouting();
        flyout.AudioPanel.OutputRequested += async id =>
        {
            flyout.AudioPanel.SetBusy(true);
            var success = await routing.SelectAsync(id);
            if (disposed) return;
            flyout.AudioPanel.SetBusy(false); flyout.AudioPanel.SetState(routing.State); flyout.AudioPanel.Report(success);
            audio.RequestRefresh();
        };
        flyout.AudioPanel.AppVolumeRequested += async (app, value) => { var success = await routing.SetVolumeAsync(app, value); if (!disposed) { flyout.AudioPanel.SetState(routing.State); flyout.AudioPanel.Report(success); } };
        flyout.AudioPanel.AppMuteRequested += async (app, mute) => { var success = await routing.SetMuteAsync(app, mute); if (!disposed) { flyout.AudioPanel.SetState(routing.State); flyout.AudioPanel.Report(success); } };
        nativeBrightnessOsd = new();
        brightness = new(Application.Current.Dispatcher, brightnessFactory);
        nativeBrightnessOsd.CandidateShown += brightness.RequestRefresh;
        brightness.Activity += nativeBrightnessOsd.NotifyBrightnessActivity;
        brightness.Changed += OnBrightnessChanged;
        flyout.BrightnessRequested += value => { if (flyout.BrightnessTargetId.StartsWith("ddc:", StringComparison.Ordinal)) _ = SetExternal(flyout.BrightnessTargetId, value); else SetBrightness(value); };
        window.BrightnessRequested += SetBrightness;
        window.ExternalScanRequested += async () => await ScanExternal();
        window.ExternalBrightnessRequested += async (id, value) => await SetExternal(id, value);
        Microsoft.Win32.SystemEvents.UserPreferenceChanged += OnAppearanceChanged;
        Microsoft.Win32.SystemEvents.PowerModeChanged += OnResume;
        MonitorCatalog.Changed += OnMonitorChange;
        media = new(Application.Current.Dispatcher);
        window.CreateSupportReport = () => SupportReport.Create(settings, lastState.Available, lastBrightness.Available, routing.State.Outputs.Length, routing.State.Apps.Length, media.State.Sources.Count);
        media.Changed += flyout.UpdateMedia;
        flyout.MediaRequested += async (source, command) => flyout.ReportMediaResult(await media.ExecuteAsync(command, source));
        flyout.SeekRequested += async (source, position) => flyout.ReportMediaResult(await media.SeekAsync(position, source));
        flyout.SourceRequested += async source =>
        {
            var success = await media.SelectSourceAsync(source);
            flyout.UpdateMedia(media.State);
            flyout.ReportSourceSelection(source, success);
        };
        flyout.SettingsRequested += ShowSettings;
        flyout.VolumeRequested += SetVolume;
        flyout.MuteRequested += () => OnVolumePressed(VolumeCommand.Mute, Stopwatch.GetTimestamp());
        try
        {
            keyboard = new(Application.Current.Dispatcher);
            keyboard.VolumeInput += input => OnVolumePressed(input.Command, input.Timestamp);
            keyboard.LockPressed += (name, enabled) => flyout.ShowLock(name, enabled);
        }
        catch (Win32Exception ex) { hookError = ex.Message; }
        ApplyPreferences();

        trayIcon = BrandIcon.CreateTrayIcon();
        var menu = new DarkTrayMenu(() =>
        {
            var color = ((System.Windows.Media.SolidColorBrush)Application.Current.Resources["AccentBrush"]).Color;
            return Color.FromArgb(color.R, color.G, color.B);
        });
        menu.AddAction(TrayAction.Settings, Strings.T("Luma ayarları"), (_, _) => ShowSettings());
        menu.AddAction(TrayAction.Exit, Strings.T("Çıkış"), (_, _) => Application.Current.Shutdown());
        tray = new Forms.NotifyIcon { Text = "Luma · ses ve klavye göstergeleri", Icon = trayIcon, ContextMenuStrip = menu, Visible = true };
        tray.MouseClick += OnTrayMouseClick;
        Strings.Changed += OnLanguageChanged;
        _ = media.StartAsync();
    }

    public void ShowSettings()
    {
        window.Show();
        if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
        window.Activate();
        if (settings.ExternalBrightness) _ = ScanExternal();
    }
    public void ShowInitial()
    {
        if (!settings.WelcomeCompleted && window.ShowWelcome()) return;
        ShowSettings();
    }
    private void OnLanguageChanged()
    {
        if (disposed) return;
        foreach (Forms.ToolStripItem item in tray.ContextMenuStrip!.Items) item.Text = Strings.T(item.Tag is TrayAction.Settings ? "Luma ayarları" : "Çıkış");
        flyout.ApplyLanguage();
    }
    internal void OnTrayMouseClick(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button == Forms.MouseButtons.Left) ShowSettings();
    }
    private async Task RefreshRouting()
    {
        if (disposed || refreshingRouting || !flyout.AudioPanelOpen) return;
        refreshingRouting = true;
        try { var success = await routing.RefreshAsync(); if (!disposed && flyout.AudioPanelOpen) { flyout.AudioPanel.SetState(routing.State); flyout.AudioPanel.Report(success); } }
        finally { refreshingRouting = false; }
    }
    private void SaveSettings() { store.Save(settings); Palette.Apply(settings); window.ApplyAppearance(); ApplyPreferences(); flyout.ApplyPreferences(); flyout.PlaceOnActiveMonitor(); if (!settings.ExternalBrightness) { externalRevision++; external.Suspend(); } }
    private void ApplyPreferences()
    {
        nativeBrightnessOsd.SetEnabled(settings.ObserveBrightness && settings.HideNativeBrightness && lastBrightness.Available);
        if (keyboard is not null)
        {
            keyboard.HandleVolume = settings.VolumeKeys;
            keyboard.ShowLocks = settings.LockKeys;
            keyboard.AudioAvailable = lastState.Available;
        }
        window.SetStatus(Strings.T(hookError is not null ? "Tuş erişimi yok" : !lastState.Available ? "Ses aygıtı bekleniyor" : "Arka planda hazır"), hookError is null && lastState.Available);
    }
    private void OnVolumePressed(VolumeCommand command, long timestamp)
    {
        if (!lastState.Available) return;
        lastState = command == VolumeCommand.Mute ? lastState with { Muted = !lastState.Muted }
            : lastState with { Volume = Math.Clamp(lastState.Volume + (command == VolumeCommand.Up ? settings.VolumeStep : -settings.VolumeStep), 0, 100), Muted = false };
        pendingAudioSequence = audio.Adjust(command, settings.VolumeStep);
        flyout.TrackInput(timestamp);
        ShowCurrentVolume();
    }
    private void SetVolume(int value)
    {
        if (!lastState.Available) return;
        lastState = lastState with { Volume = Math.Clamp(value, 0, 100), Muted = false };
        pendingAudioSequence = audio.SetVolume(value);
        ShowCurrentVolume();
    }
    private void ShowCurrentVolume()
    {
        flyout.ShowVolume(lastState);
        flyout.UpdateMedia(media.State);
    }
    private void OnAudioChanged(AudioUpdate update)
    {
        if (disposed || update.Sequence < pendingAudioSequence) return;
        var current = update.State;
        var changed = current.Available && lastState.Available && (current.Volume != lastState.Volume || current.Muted != lastState.Muted);
        lastState = current;
        ApplyPreferences();
        if (changed && settings.ObserveVolume) { flyout.ShowVolume(current); flyout.UpdateMedia(media.State); }
        else flyout.RefreshVolume(current);
    }

    private void SetBrightness(int value)
    {
        if (!lastBrightness.Available) return;
        lastBrightness = lastBrightness with { Value = lastBrightness.Snap(value) };
        pendingBrightnessSequence = brightness.SetBrightness(lastBrightness.Value);
        nativeBrightnessOsd.NotifyBrightnessActivity();
        window.SetBrightness(lastBrightness);
        flyout.ShowBrightness(lastBrightness);
    }

    private void OnBrightnessChanged(BrightnessUpdate update)
    {
        if (disposed || update.Sequence < pendingBrightnessSequence) return;
        var current = update.State;
        var changed = current.Available && lastBrightness.Available && current.InstanceId == lastBrightness.InstanceId && current.Value != lastBrightness.Value;
        lastBrightness = current;
        nativeBrightnessOsd.SetEnabled(settings.ObserveBrightness && settings.HideNativeBrightness && current.Available);
        window.SetBrightness(current, update.WriteFailed);
        if ((changed || update.HardwareActivity) && settings.ObserveBrightness)
        {
            flyout.ShowBrightness(current);
        }
        else flyout.RefreshBrightness(current);
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        Strings.Changed -= OnLanguageChanged;
        Microsoft.Win32.SystemEvents.UserPreferenceChanged -= OnAppearanceChanged;
        Microsoft.Win32.SystemEvents.PowerModeChanged -= OnResume;
        MonitorCatalog.Changed -= OnMonitorChange;
        external.Dispose();
        keyboard?.Dispose();
        media.Dispose();
        tray.Visible = false;
        tray.ContextMenuStrip?.Dispose();
        tray.Dispose(); trayIcon.Dispose();
        flyout.Close();
        window.AllowClose = true; window.Close();
        audio.Dispose();
        routingTimer.Stop(); routing.Dispose();
        brightness.Activity -= nativeBrightnessOsd.NotifyBrightnessActivity;
        brightness.Dispose();
        nativeBrightnessOsd.Dispose();
    }
}
