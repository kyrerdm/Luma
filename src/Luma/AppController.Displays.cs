using System;
using System.Threading.Tasks;
using Luma.Services;
using Luma.Themes;
using Microsoft.Win32;
namespace Luma;

public sealed partial class AppController
{
    private readonly BackgroundExternalBrightness external = new();
    private bool scanningExternal;
    private bool rescanExternal;
    private int externalRevision;
    private async Task ScanExternal()
    {
        if (disposed || !settings.ExternalBrightness) return;
        if (scanningExternal) { rescanExternal = true; return; }
        rescanExternal = false; scanningExternal = true; var revision = ++externalRevision; window.SetExternalBusy(true);
        try { var displays = await external.ScanAsync(); if (!disposed && settings.ExternalBrightness && revision == externalRevision) window.SetExternal(displays); }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or ObjectDisposedException) { if (!disposed) window.SetExternal(Array.Empty<ExternalBrightness>()); }
        finally { scanningExternal = false; if (!disposed) { window.SetExternalBusy(false); if (rescanExternal && settings.ExternalBrightness) { rescanExternal = false; _ = ScanExternal(); } } }
    }
    private async Task SetExternal(string id, int value)
    {
        if (disposed || !settings.ExternalBrightness) return;
        var revision = externalRevision;
        try
        {
            var state = await external.SetAsync(id, value);
            if (disposed || state is null || revision != externalRevision || !settings.ExternalBrightness) return;
            window.UpdateExternal(state);
            flyout.ShowBrightness(new(state.Available, state.Value, state.Id, Array.Empty<int>(), state.Status));
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception or ObjectDisposedException) { if (!disposed) window.SetExternalBusy(false); }
    }
    private void OnAppearanceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (!disposed) System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() => { if (!disposed) { Palette.Apply(settings); window.ApplyAppearance(); } }));
    }
    private void OnMonitorChange()
    {
        if (disposed) return;
        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(async () => { if (disposed) return; externalRevision++; external.Suspend(); if (settings.ExternalBrightness && window.IsVisible) { if (scanningExternal) rescanExternal = true; else await ScanExternal(); } }));
    }
    private void OnResume(object sender, PowerModeChangedEventArgs e)
    {
        if (e.Mode != PowerModes.Resume || disposed) return;
        System.Windows.Application.Current.Dispatcher.BeginInvoke(new Action(() =>
        {
            if (disposed) return;
            audio.RequestRefresh(); brightness.RequestRefresh(); routing.Reset(); media.RequestRefresh();
            MonitorCatalog.Refresh(); flyout.PlaceOnActiveMonitor();
            if (flyout.AudioPanelOpen) _ = RefreshRouting();
            OnMonitorChange();
        }));
    }
}
