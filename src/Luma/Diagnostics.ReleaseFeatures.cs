using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Luma.Models;
using Luma.Services;
using Luma.Themes;
using Luma.Views;

namespace Luma;

internal static partial class Diagnostics
{
    private static async Task VerifyReleaseFeaturesAsync(string directory, Action<string, bool, string> check)
    {
        var prefs = new AppSettings { Theme = "invalid", CustomAccent = "#notrgb", SurfaceOpacity = double.NaN };
        prefs.Normalize();
        check("Invalid appearance settings safely normalize", prefs.Theme == "Dark" && prefs.CustomAccent == "#81B7DB" && prefs.SurfaceOpacity == 0.94, "");
        prefs = new() { Theme = "Light", Accent = "Custom", CustomAccent = "#16aabb", SurfaceOpacity = 0.8, HideInFullscreen = true, ExternalBrightness = true };
        var store = new SettingsStore(Path.Combine(directory, "new-feature-data")); store.Save(prefs); prefs = store.Load();
        check("New appearance and behavior preferences survive restart", prefs.Theme == "Light" && prefs.Accent == "Custom" && prefs.CustomAccent == "#16AABB" && prefs.SurfaceOpacity == 0.8 && prefs.HideInFullscreen && prefs.ExternalBrightness, "");
        check("System theme selection honors explicit overrides", Appearance.ResolveLight("System", true) && !Appearance.ResolveLight("System", false) && Appearance.ResolveLight("Light", false) && !Appearance.ResolveLight("Dark", true), "");
        check("Custom colors accept RGB hex and reject ambiguous input", Palette.ValidCustomColor("#16AaBb") && !Palette.ValidCustomColor("red") && !Palette.ValidCustomColor("#FF000000") && !Palette.ValidCustomColor("#00000Z"), "");
        Palette.Apply(prefs);
        check("Light theme updates text, surfaces, accent, and opacity", ((SolidColorBrush)Application.Current.Resources["TextBrush"]).Color.R < 60 && ((SolidColorBrush)Application.Current.Resources["BackgroundBrush"]).Color.R > 230 && ((SolidColorBrush)Application.Current.Resources["AccentBrush"]).Color.G == 170 && ((SolidColorBrush)Application.Current.Resources["FlyoutBrush"]).Color.A == 204, "");
        var settings = new SettingsWindow(prefs, () => { }) { AllowClose = true }; settings.Show();
        await Task.Delay(100); Render(settings, Path.Combine(directory, "luma-light-settings.png"));
        settings.SetExternal(new[] { new ExternalBrightness("ddc:test-a", "Ekran 1 · 2560×1440", 60, true, "DDC/CI · örnek ekran"), new ExternalBrightness("ddc:test-b", "Ekran 2 · 1920×1080", 0, false, "DDC/CI kapalı veya desteklenmiyor.") });
        var externalRows = (StackPanel)settings.FindName("ExternalRows");
        var supportedSlider = (Slider)((Grid)((StackPanel)externalRows.Children[0]).Children[1]).Children[0];
        var unsupportedSlider = (Slider)((Grid)((StackPanel)externalRows.Children[1]).Children[1]).Children[0];
        var externalCommands = new List<string>(); settings.ExternalBrightnessRequested += (id, value) => externalCommands.Add(id + ":" + value);
        supportedSlider.Value = 65;
        check("External brightness rows only send supported monitor requests", externalCommands.SequenceEqual(new[] { "ddc:test-a:65" }) && !unsupportedSlider.IsEnabled, "");
        ((ScrollViewer)settings.FindName("PreferencesScroll")).ScrollToVerticalOffset(500);
        await Task.Delay(60); Render(settings, Path.Combine(directory, "luma-light-preferences.png")); settings.Close();

        var sampleApp = new AudioApp("spotify", "Spotify", "output-a", new[] { "session-a" }, 55, false);
        var sample = new AudioRoutingState(new[] { new AudioOutput("output-a", "Kulaklık · USB Audio", true), new AudioOutput("output-b", "Hoparlör · Realtek Audio", false) }, new[] { sampleApp, sampleApp with { Id = "chrome", Name = "Google Chrome", SessionIds = new[] { "session-b", "session-c" }, Volume = 35 } }, "Seçili çıkıştaki uygulamalar");
        var panel = new AudioPanel(); var volumes = new List<string>(); var outputs = new List<string>(); var mutes = new List<bool>();
        panel.OutputRequested += outputs.Add; panel.AppVolumeRequested += (app, value) => volumes.Add(app.SessionIds[0] + ":" + value); panel.AppMuteRequested += (_, muted) => mutes.Add(muted);
        panel.SetState(sample); panel.Measure(new Size(348, double.PositiveInfinity)); panel.Arrange(new Rect(new Point(), panel.DesiredSize)); panel.UpdateLayout();
        RenderElement(panel, Path.Combine(directory, "luma-light-audio-panel.png"));
        var apps = (StackPanel)panel.FindName("AppRows"); var first = (Grid)apps.Children[0]; var appSlider = (Slider)first.Children[2];
        appSlider.Value = 70; ((Button)first.Children[1]).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        ((Button)((StackPanel)panel.FindName("OutputRows")).Children[1]).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        panel.SetState(sample);
        check("Audio panel routes output and per-app commands without writing on refresh", volumes.SequenceEqual(new[] { "session-a:70" }) && mutes.SequenceEqual(new[] { true }) && outputs.SequenceEqual(new[] { "output-b" }), "");
        panel.SetState(sample with { Apps = new[] { sampleApp } });
        check("Closed audio apps are removed from the mixer", apps.Children.Count == 1, "");
        Palette.Apply(new AppSettings());
        panel.SetState(sample); await Task.Delay(80); panel.UpdateLayout(); RenderElement(panel, Path.Combine(directory, "luma-audio-panel.png"));
        using (var native = new AudioService())
        {
            var nativeRouting = native.ReadRouting();
            var master = native.Read();
            check("Real CoreAudio outputs and application sessions can be read", (!master.Available || (nativeRouting.Available && nativeRouting.Outputs.Any(o => o.Selected && o.Id == master.DeviceId))) && nativeRouting.Outputs.Select(o => o.Id).Distinct().Count() == nativeRouting.Outputs.Length && nativeRouting.Apps.All(a => a.Volume is >= 0 and <= 100 && a.SessionIds.Length > 0), $"Outputs={nativeRouting.Outputs.Length}; apps={nativeRouting.Apps.Length}; status={nativeRouting.Status}");
            check("Stale devices and session identities cannot change audio", !native.SelectOutput("luma-missing-output") && !native.SetAppVolume(sampleApp with { DeviceId = "luma-missing-output" }, 50), "");
            var policy = native.VerifyCurrentOutputPolicy();
            check(policy is null ? "Audio policy smoke test preserves distinct or unavailable output roles" : "Windows output policy accepts the current output without changing volume", policy is null || policy.Value, "No alternate device was selected; communication output was untouched.");
        }
        var routingFixture = new DiagnosticRoutingDevice(sample);
        using (var router = new BackgroundAudioRouting(Application.Current.Dispatcher, () => routingFixture))
        {
            await router.RefreshAsync().WaitAsync(TimeSpan.FromSeconds(3));
            check("Audio routing discovery never writes to audio", routingFixture.Writes == 0 && router.State.Outputs.Length == 2, "");
            check("Output selection and per-app audio use the background worker", await router.SetVolumeAsync(sampleApp, 66) && await router.SetMuteAsync(sampleApp, true) && await router.SelectAsync("output-b") && routingFixture.WorkerThread != Environment.CurrentManagedThreadId, "");
            check("An old mixer command is rejected after an output switch", !await router.SetVolumeAsync(sampleApp, 10) && routingFixture.LastVolume == 66, "");
        }
        var burstFixture = new DiagnosticRoutingDevice(sample) { BlockReads = true };
        using (var router = new BackgroundAudioRouting(Application.Current.Dispatcher, () => burstFixture))
        {
            var discovery = router.RefreshAsync();
            await Task.Run(() => burstFixture.Entered.Wait(2000));
            var firstVolume = router.SetVolumeAsync(sampleApp, 10); var lastVolume = router.SetVolumeAsync(sampleApp, 20); var otherVolume = router.SetVolumeAsync(sample.Apps[1], 30);
            burstFixture.Released.Set();
            await Task.WhenAll(discovery, firstVolume, lastVolume, otherVolume).WaitAsync(TimeSpan.FromSeconds(3));
            check("Audio slider bursts retain the latest value independently per app", burstFixture.Writes == 2 && burstFixture.AppVolumes["spotify"] == 20 && burstFixture.AppVolumes["chrome"] == 30, "");
        }

        var position = System.Windows.Forms.Cursor.Position;
        var screen = System.Windows.Forms.Screen.FromPoint(position).WorkingArea;
        var flyoutSettings = new AppSettings { HideInFullscreen = true, Animations = false, DurationSeconds = 1, Position = (position.Y < screen.Top + screen.Height / 2 ? "Bottom" : "Top") + (position.X < screen.Left + screen.Width / 2 ? "Right" : "Left") };
        var flyout = new FlyoutWindow(flyoutSettings) { FullscreenProbe = () => true };
        flyout.ShowVolume(new(true, 40, false, "Sample"));
        check("Fullscreen preference suppresses automatic flyouts", !flyout.IsVisible, "");
        flyout.FullscreenProbe = () => false; flyout.ShowVolume(new(true, 40, false, "Sample"));
        flyout.AudioPanel.SetState(sample);
        ((Button)flyout.FindName("OutputButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
        await Task.Delay(1200);
        check("The output and app-volume popup pauses automatic dismissal", flyout.IsVisible && flyout.AudioPanelOpen, "");
        check("The live audio popup uses the active dark theme", ((SolidColorBrush)((Border)flyout.AudioPanel.Content).Background).Color.R < 70, "");
        RenderElement(flyout.AudioPanel, Path.Combine(directory, "luma-audio-panel.png"));
        Palette.Apply(new AppSettings { Theme = "Light", Accent = "Lilac" }); await Task.Delay(80);
        check("An open audio popup follows a live theme change", ((SolidColorBrush)((Border)flyout.AudioPanel.Content).Background).Color.R > 230, "");
        RenderElement(flyout.AudioPanel, Path.Combine(directory, "luma-light-audio-panel.png"));
        Palette.Apply(new AppSettings()); await Task.Delay(50);
        ((Popup)flyout.FindName("AudioPopup")).IsOpen = false;
        flyout.ShowBrightness(new(true, 60, "ddc:external-a", Array.Empty<int>(), "External"));
        flyout.RefreshBrightness(new(true, 30, "internal-a", Array.Empty<int>(), "Internal"));
        check("Internal brightness polling cannot overwrite an external monitor flyout", ((TextBlock)flyout.FindName("BrightnessValueText")).Text == "60" && flyout.BrightnessTargetId == "ddc:external-a", "");
        flyout.Close();
        check("Fullscreen bounds support negative monitor coordinates and exclude smaller windows", FullscreenDetector.Covers(new(-1920, 0, 1920, 1080), new(-1920, 0, 1920, 1080)) && !FullscreenDetector.Covers(new(-1900, 0, 1880, 1000), new(-1920, 0, 1920, 1080)), "");

        check("DDC brightness maps percentages to each monitor's native range", ExternalBrightnessDevice.Percent(60, 20, 100) == 50 && ExternalBrightnessDevice.Level(50, 20, 100) == 60 && ExternalBrightnessDevice.Percent(0, 0, 0) == 0, "");
        var ddc = new DiagnosticExternalDevice();
        using (var service = new BackgroundExternalBrightness(() => ddc))
        {
            var scan = service.ScanAsync();
            var entered = await Task.Run(() => ddc.Entered.Wait(2000));
            var a1 = service.SetAsync("a", 10); var a2 = service.SetAsync("a", 20); var b = service.SetAsync("b", 30);
            ddc.Released.Set(); await scan.WaitAsync(TimeSpan.FromSeconds(3)); await Task.WhenAll(a1, a2, b).WaitAsync(TimeSpan.FromSeconds(3));
            check("Slow DDC discovery stays off the UI and never changes brightness", entered && ddc.Scans == 1 && ddc.WorkerThread != Environment.CurrentManagedThreadId, "");
            check("DDC coalesces slider changes separately for each monitor", ddc.Writes == 2 && ddc.Values["a"] == 20 && ddc.Values["b"] == 30 && a1.Result is null, "");
            ddc.Entered.Reset(); ddc.Released.Reset(); var rescan = service.ScanAsync(); await Task.Run(() => ddc.Entered.Wait(2000));
            var cancelled = service.SetAsync("a", 90); service.Suspend(); ddc.Released.Set(); await rescan; var cancelledResult = await cancelled;
            check("Disabling DDC cancels queued writes before releasing monitor handles", cancelledResult is null && ddc.Writes == 2 && ddc.Values["a"] == 20, "");
        }
        check("External monitor handles are released on service disposal", await Task.Run(() => ddc.Disposed.Wait(2000)), "");

        const string release = "{\"tag_name\":\"v1.0.0\",\"draft\":false,\"prerelease\":false,\"html_url\":\"https://github.com/kyrerdm/Luma/releases/tag/v1.0.0\"}";
        check("Update checks compare versions and restrict release links", UpdateService.Parse(release, new(0, 6, 0)).Available && !UpdateService.Parse(release, new(1, 0, 0)).Available && !UpdateService.Parse(release.Replace("github.com/", "example.com/"), new(0, 6, 0)).Available && !UpdateService.Parse(release.Replace("\"prerelease\":false", "\"prerelease\":true"), new(0, 6, 0)).Available, "");
        using (var updater = new UpdateService(new DiagnosticHttpHandler(release))) check("Manual update checks handle a verified server response", (await updater.CheckAsync()).Available, "");
        using (var updater = new UpdateService(new DiagnosticHttpHandler("", true))) check("Offline update checks fail with a readable status", !(await updater.CheckAsync()).Available, "");
        var plan = InstallService.Plan(Path.Combine(directory, "install-example"), new(0, 6, 0));
        check("Setup downloads keep installer mode when Windows adds a duplicate suffix", App.IsSetupExecutable("Luma-0.6.0-Setup.exe") && App.IsSetupExecutable("Luma-0.6.0-Setup (1).exe") && !App.IsSetupExecutable("Luma.exe"), "");
        check("Installer uses a versioned app directory and includes all notices", plan.Executable.StartsWith(Path.GetFullPath(directory), StringComparison.OrdinalIgnoreCase) && Path.GetFileName(plan.Executable) == "Luma.exe" && InstallService.Notices.All(name => InstallService.ReadNotice(name).Length > 100), "");
        var stub = Path.Combine(directory, "installer-test-source.bin"); File.WriteAllText(stub, "installer test fixture");
        var registered = false;
        InstallService.InstallFiles(stub, plan, true, (written, desktop) => registered = written == plan && desktop);
        check("Installer writes the app, licenses, guide, and uninstall script before registration", registered && File.ReadAllText(plan.Executable) == "installer test fixture" && InstallService.Notices.All(name => File.ReadAllBytes(Path.Combine(plan.Directory, name)).SequenceEqual(InstallService.ReadNotice(name))) && File.ReadAllText(plan.UninstallScript) == InstallService.UninstallScript, "Registration was replaced by an isolated fixture; no user registry or shortcut was modified.");
        var escaped = false;
        try { InstallService.InstallFiles(stub, plan with { Executable = Path.Combine(directory, "outside.exe") }, false, (_, _) => { }); } catch (IOException) { escaped = true; }
        check("Installer refuses destinations outside its selected app directory", escaped && !File.Exists(Path.Combine(directory, "outside.exe")), "");
        var startupBefore = StartupService.IsEnabled();
        var install = new InstallWindow(); install.Show(); await Task.Delay(80); Render(install, Path.Combine(directory, "luma-installer.png")); install.Close();
        check("Installer preview does not change startup or claim installation", startupBefore == StartupService.IsEnabled() && ((Button)install.FindName("InstallNowButton")).IsEnabled && ((Button)install.FindName("LaunchButton")).Visibility == Visibility.Collapsed, "");
        Palette.Apply(new AppSettings());
    }

    private sealed class DiagnosticRoutingDevice(AudioRoutingState initial) : IAudioRoutingDevice
    {
        public int Writes, WorkerThread, LastVolume;
        public readonly ManualResetEventSlim Entered = new(), Released = new();
        public readonly Dictionary<string, int> AppVolumes = new();
        public bool BlockReads;
        private AudioRoutingState state = initial;
        public AudioRoutingState ReadRouting() { WorkerThread = Environment.CurrentManagedThreadId; if (BlockReads) { Entered.Set(); Released.Wait(3000); } return state; }
        public bool SelectOutput(string id) { if (!state.Outputs.Any(o => o.Id == id)) return false; Writes++; state = state with { Outputs = state.Outputs.Select(o => o with { Selected = o.Id == id }).ToArray(), Apps = Array.Empty<AudioApp>() }; return true; }
        public bool SetAppVolume(AudioApp app, int value) { if (!state.Outputs.Any(o => o.Id == app.DeviceId && o.Selected)) return false; LastVolume = value; AppVolumes[app.Id] = value; Writes++; return true; }
        public bool SetAppMute(AudioApp app, bool muted) { Writes++; return true; }
        public void Dispose() { }
    }
    private sealed class DiagnosticExternalDevice : IExternalBrightnessDevice
    {
        public readonly ManualResetEventSlim Entered = new(), Released = new(), Disposed = new();
        public readonly Dictionary<string, int> Values = new();
        public int Writes, Scans, WorkerThread;
        public ExternalBrightness[] Scan() { Scans++; WorkerThread = Environment.CurrentManagedThreadId; Entered.Set(); Released.Wait(3000); return Array.Empty<ExternalBrightness>(); }
        public ExternalBrightness? Set(string id, int value) { Writes++; Values[id] = value; return new(id, id, value, true, "Sample"); }
        public void Dispose() => Disposed.Set();
    }
    private sealed class DiagnosticHttpHandler(string json, bool offline = false) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => offline ? Task.FromException<HttpResponseMessage>(new HttpRequestException("Offline")) : Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json) });
    }
}
