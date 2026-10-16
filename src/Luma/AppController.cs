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

public sealed class AppController : IDisposable
{
    private readonly SettingsStore store;
    private readonly AppSettings settings;
    private readonly BackgroundAudioService audio;
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
        Palette.Apply(settings.Accent);
        flyout = new(settings);
        window = new(settings, SaveSettings);
        flyout.Prepare();
        audio = new(Application.Current.Dispatcher, audioFactory);
        audio.Changed += OnAudioChanged;
        nativeBrightnessOsd = new();
        brightness = new(Application.Current.Dispatcher, brightnessFactory);
        nativeBrightnessOsd.CandidateShown += brightness.RequestRefresh;
        brightness.Activity += nativeBrightnessOsd.NotifyBrightnessActivity;
        brightness.Changed += OnBrightnessChanged;
        flyout.BrightnessRequested += SetBrightness;
        window.BrightnessRequested += SetBrightness;
        media = new(Application.Current.Dispatcher);
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
            var color = Palette.Resolve(settings.Accent).Color;
            return Color.FromArgb(color.R, color.G, color.B);
        });
        menu.AddAction(TrayAction.Settings, "Luma ayarları", (_, _) => ShowSettings());
        menu.AddAction(TrayAction.Exit, "Çıkış", (_, _) => Application.Current.Shutdown());
        tray = new Forms.NotifyIcon { Text = "Luma · ses ve klavye göstergeleri", Icon = trayIcon, ContextMenuStrip = menu, Visible = true };
        tray.MouseClick += OnTrayMouseClick;
        _ = media.StartAsync();
    }

    public void ShowSettings()
    {
        window.Show();
        if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
        window.Activate();
    }
    internal void OnTrayMouseClick(object? sender, Forms.MouseEventArgs e)
    {
        if (e.Button == Forms.MouseButtons.Left) ShowSettings();
    }
    private void SaveSettings() { store.Save(settings); Palette.Apply(settings.Accent); ApplyPreferences(); flyout.ApplyPreferences(); flyout.PlaceOnActiveMonitor(); }
    private void ApplyPreferences()
    {
        nativeBrightnessOsd.SetEnabled(settings.ObserveBrightness && settings.HideNativeBrightness && lastBrightness.Available);
        if (keyboard is not null)
        {
            keyboard.HandleVolume = settings.VolumeKeys;
            keyboard.ShowLocks = settings.LockKeys;
            keyboard.AudioAvailable = lastState.Available;
        }
        window.SetStatus(hookError is not null ? "Tuş erişimi yok" : !lastState.Available ? "Ses aygıtı bekleniyor" : "Arka planda hazır", hookError is null && lastState.Available);
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
        keyboard?.Dispose();
        media.Dispose();
        tray.Visible = false;
        tray.ContextMenuStrip?.Dispose();
        tray.Dispose(); trayIcon.Dispose();
        flyout.Close();
        window.AllowClose = true; window.Close();
        audio.Dispose();
        brightness.Activity -= nativeBrightnessOsd.NotifyBrightnessActivity;
        brightness.Dispose();
        nativeBrightnessOsd.Dispose();
    }
}
