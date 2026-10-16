using System;
using System.ComponentModel;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows;
using System.Windows.Threading;
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
    private readonly AudioService audio = new();
    private readonly MediaService media;
    private readonly KeyboardService? keyboard;
    private readonly FlyoutWindow flyout;
    private readonly SettingsWindow window;
    private readonly DispatcherTimer poll = new() { Interval = TimeSpan.FromMilliseconds(250) };
    private readonly Forms.NotifyIcon tray;
    private readonly Icon trayIcon;
    private readonly string? hookError;
    private AudioState lastState;
    private bool disposed;

    public AppController(string dataDirectory)
    {
        store = new(dataDirectory);
        settings = store.Load();
        Palette.Apply(settings.Accent);
        lastState = audio.Read();
        flyout = new(settings);
        window = new(settings, SaveSettings, Preview);
        media = new(Application.Current.Dispatcher);
        media.Changed += flyout.UpdateMedia;
        flyout.MediaRequested += async command => flyout.ReportMediaResult(await media.ExecuteAsync(command));
        flyout.SeekRequested += async position => flyout.ReportMediaResult(await media.SeekAsync(position));
        flyout.SettingsRequested += ShowSettings;
        flyout.VolumeRequested += value => { audio.SetVolume(value); UpdateAfterCommand(); };
        flyout.MuteRequested += () => { audio.ToggleMute(); UpdateAfterCommand(); };
        try
        {
            keyboard = new(Application.Current.Dispatcher);
            keyboard.VolumePressed += OnVolumePressed;
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
        poll.Tick += (_, _) => PollAudio();
        poll.Start();
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
    private void OnVolumePressed(VolumeCommand command)
    {
        var state = audio.Read();
        if (!state.Available) { UpdateAfterCommand(); return; }
        if (command == VolumeCommand.Mute) audio.ToggleMute();
        else audio.SetVolume(state.Volume + (command == VolumeCommand.Up ? settings.VolumeStep : -settings.VolumeStep));
        UpdateAfterCommand();
    }
    private void UpdateAfterCommand()
    {
        lastState = audio.Read();
        ApplyPreferences();
        flyout.ShowVolume(lastState);
        flyout.UpdateMedia(media.State);
    }
    private void PollAudio()
    {
        var current = audio.Read();
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
        poll.Stop();
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
