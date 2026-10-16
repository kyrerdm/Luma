using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
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
    private readonly MediaService media;
    private readonly KeyboardService? keyboard;
    private readonly FlyoutWindow flyout;
    private readonly SettingsWindow window;
    private readonly Forms.NotifyIcon tray;
    private readonly Icon trayIcon;
    private readonly string? hookError;
    private AudioState lastState = new(false, 0, false, "Ses aygıtı bekleniyor");
    private long pendingAudioSequence;
    private bool disposed;
    internal Task<AudioState> AudioReady => audio.Ready;
    internal FlyoutWindow DiagnosticFlyout => flyout;
    internal Task FlushAudioAsync() => audio.FlushAsync();

    public AppController(string dataDirectory) : this(dataDirectory, null) { }

    internal AppController(string dataDirectory, Func<IAudioDevice>? audioFactory)
    {
        store = new(dataDirectory);
        settings = store.Load();
        Palette.Apply(settings.Accent);
        flyout = new(settings);
        window = new(settings, SaveSettings, Preview);
        flyout.Prepare();
        audio = new(Application.Current.Dispatcher, audioFactory);
        audio.Changed += OnAudioChanged;
        media = new(Application.Current.Dispatcher);
        media.Changed += flyout.UpdateMedia;
        flyout.MediaRequested += async command => flyout.ReportMediaResult(await media.ExecuteAsync(command));
        flyout.SeekRequested += async position => flyout.ReportMediaResult(await media.SeekAsync(position));
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

        trayIcon = CreateTrayIcon();
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Luma ayarları", null, (_, _) => ShowSettings());
        menu.Items.Add("Göstergeyi dene", null, (_, _) => Preview());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Çıkış", null, (_, _) => Application.Current.Shutdown());
        tray = new Forms.NotifyIcon { Text = "Luma · ses ve klavye göstergeleri", Icon = trayIcon, ContextMenuStrip = menu, Visible = true };
        tray.DoubleClick += (_, _) => ShowSettings();
        _ = media.StartAsync();
    }

    public void ShowSettings()
    {
        window.Show();
        if (window.WindowState == WindowState.Minimized) window.WindowState = WindowState.Normal;
        window.Activate();
    }
    private void Preview() => flyout.ShowPreview(PreviewData.Media());
    private void SaveSettings() { store.Save(settings); Palette.Apply(settings.Accent); ApplyPreferences(); flyout.ApplyPreferences(); flyout.PlaceOnActiveMonitor(); }
    private void ApplyPreferences()
    {
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

    private static Icon CreateTrayIcon()
    {
        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);
        using var fill = new SolidBrush(Color.FromArgb(129, 183, 219));
        graphics.FillEllipse(fill, 1, 1, 30, 30);
        using var pen = new Pen(Color.FromArgb(29, 23, 43), 3.2f) { StartCap = LineCap.Round, EndCap = LineCap.Round, LineJoin = LineJoin.Round };
        graphics.DrawLines(pen, new[] { new System.Drawing.Point(12, 9), new System.Drawing.Point(12, 22), new System.Drawing.Point(21, 22) });
        var handle = bitmap.GetHicon();
        try { return (Icon)Icon.FromHandle(handle).Clone(); }
        finally { DestroyIcon(handle); }
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
    }
    [System.Runtime.InteropServices.DllImport("user32.dll")] private static extern bool DestroyIcon(IntPtr handle);
}
