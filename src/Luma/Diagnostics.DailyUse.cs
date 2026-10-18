using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Luma.Models;
using Luma.Services;
using Luma.Themes;
using Luma.Views;

namespace Luma;

internal static partial class Diagnostics
{
    private static async Task VerifyDailyUseAsync(string directory, Action<string, bool, string> check)
    {
        var prefs = new AppSettings { Accent = "Custom", CustomAccent = "#AABBCC", Position = "TopLeft", TargetDisplay = "private-display-identifier", Language = "en", ExternalBrightness = true };
        var backup = PreferenceTransfer.Export(prefs);
        var restored = PreferenceTransfer.Parse(backup);
        check("Preference backups retain appearance and behavior without startup or live device levels", restored.CustomAccent == "#AABBCC" && restored.Position == "TopLeft" && restored.ExternalBrightness && restored.Language == "en" && !backup.Contains("Startup") && !backup.Contains("CurrentVolume") && !backup.Contains("BrightnessValue"), "");
        var badFiles = new[] { "{}", "null", backup.Replace("\"FormatVersion\": 1", "\"FormatVersion\": 2"), backup.Replace("\"VolumeKeys\": true", "\"VolumeKeys\": \"wrong\""), new string('x', PreferenceTransfer.MaxBytes + 1) };
        var refused = 0;
        foreach (var json in badFiles) { try { PreferenceTransfer.Parse(json); } catch (Exception ex) when (ex is IOException or InvalidDataException or JsonException) { refused++; } }
        check("Invalid, future, incorrectly typed and oversized preference backups are refused", refused == badFiles.Length, "");
        var migration = new SettingsStore(Path.Combine(directory, "migration")); Directory.CreateDirectory(migration.DirectoryPath);
        File.WriteAllText(migration.FilePath, "{\"Accent\":\"Lilac\",\"Position\":\"TopLeft\"}");
        var existing = migration.Load();
        check("Existing preferences migrate without first-run onboarding", existing.WelcomeCompleted && existing.Accent == "Lilac" && existing.Position == "TopLeft", "");
        check("New installations get onboarding without changing Windows startup", !new SettingsStore(Path.Combine(directory, "not-created")).Load().WelcomeCompleted, "");
        File.WriteAllText(migration.FilePath, "null"); check("A null settings file safely recovers", migration.Load().VolumeKeys, "");

        Strings.SetLanguage("tr"); Palette.Apply(new AppSettings());
        var saves = 0; var selected = new AppSettings { WelcomeCompleted = true };
        var window = new SettingsWindow(selected, () => saves++) { AllowClose = true }; SeedSettingsPreview(window); window.Show();
        await Task.Delay(80);
        foreach (var page in new[] { "General", "Appearance", "Sound", "Media", "Display" })
        {
            window.Navigate(page); await Task.Delay(25);
            var visible = new[] { "General", "Appearance", "Sound", "Media", "Display" }.Count(p => ((FrameworkElement)window.FindName(p + "Section")).Visibility == Visibility.Visible);
            check("Settings navigation shows only the selected page: " + page, visible == 1 && ((FrameworkElement)window.FindName(page + "Section")).IsVisible && saves == 0, "");
            Render(window, Path.Combine(directory, "luma-settings-" + page.ToLowerInvariant() + ".png"));
        }
        var startup = StartupService.IsEnabled();
        window.ApplyTransferredPreferences(new AppSettings { Accent = "Rose", DurationSeconds = 4, ObserveBrightness = false, WelcomeCompleted = true });
        check("Imported preferences update shared state and controls without changing startup", selected.Accent == "Rose" && ((Slider)window.FindName("DurationSlider")).Value == 4 && ((CheckBox)window.FindName("BrightnessToggle")).IsChecked == false && saves == 1 && StartupService.IsEnabled() == startup, "");
        ((ComboBox)window.FindName("LanguageCombo")).SelectedIndex = 1;
        window.Navigate("Appearance"); await Task.Delay(30);
        check("English can be selected live and is saved", Strings.Language == "en" && selected.Language == "en" && ((TextBlock)window.FindName("PageTitle")).Text == "Appearance" && ((RadioButton)window.FindName("GeneralNav")).Content is "General" && Palette.Options[0].DisplayName == "Ice", "");
        Render(window, Path.Combine(directory, "luma-settings-english.png"));
        ((ComboBox)window.FindName("LanguageCombo")).SelectedIndex = 0;
        check("Changing back to Turkish restores navigation and accent names", Strings.Language == "tr" && ((RadioButton)window.FindName("GeneralNav")).Content is "Genel" && Palette.Options[0].DisplayName == "Buz mavisi", "");
        window.Close();
        var failedSettings = new AppSettings { Accent = "Lilac", WelcomeCompleted = false };
        var failedWindow = new SettingsWindow(failedSettings, () => throw new IOException("Fixture write failure")) { AllowClose = true };
        try { failedWindow.ApplyTransferredPreferences(new AppSettings { Accent = "Rose" }); } catch (IOException) { }
        check("A failed import save restores the previous shared preferences and controls", failedSettings.Accent == "Lilac" && !failedSettings.WelcomeCompleted && ((ComboBoxItem)((ComboBox)failedWindow.FindName("AccentCombo")).SelectedItem).Tag is "Lilac", "");
        failedWindow.Close();
        var firstRunSettings = new AppSettings(); var firstRunSaves = 0;
        var welcome = new WelcomeWindow(firstRunSettings, () => firstRunSaves++); welcome.Show(); await Task.Delay(40);
        Render(welcome, Path.Combine(directory, "luma-welcome.png"));
        ((ComboBox)welcome.FindName("ThemeCombo")).SelectedIndex = 1;
        ((ComboBox)welcome.FindName("LanguageCombo")).SelectedIndex = 1;
        welcome.Close();
        check("Closing onboarding cancels previews without saving or altering startup", firstRunSaves == 0 && !firstRunSettings.WelcomeCompleted && firstRunSettings.Theme == "Dark" && Strings.Language == "tr" && StartupService.IsEnabled() == startup, "");
        var completedWelcome = new WelcomeWindow(firstRunSettings, () => firstRunSaves++); completedWelcome.Show();
        ((ComboBox)completedWelcome.FindName("PositionCombo")).SelectedIndex = 4;
        ((Button)completedWelcome.FindName("FinishButton")).RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ButtonBase.ClickEvent));
        check("Finishing onboarding saves the chosen position without changing an untouched startup choice", completedWelcome.Completed && firstRunSettings.WelcomeCompleted && firstRunSettings.Position == "TopLeft" && firstRunSaves == 1 && StartupService.IsEnabled() == startup, "");
        var report = SupportReport.Create(prefs, true, false, 2, 3, 1);
        check("Support reports include useful counts and omit identifying settings and paths", report.Contains("outputs: 2; sessions: 3") && report.Contains("Version:") && !report.Contains("private-display-identifier") && !report.Contains(Environment.UserName, StringComparison.OrdinalIgnoreCase) && !report.Contains(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), StringComparison.OrdinalIgnoreCase), "");

        var blocker = new ResumeAudioDevice();
        using (var service = new BackgroundAudioService(Application.Current.Dispatcher, () => blocker))
        {
            await service.Ready.WaitAsync(TimeSpan.FromSeconds(3));
            blocker.Block = true; await Task.Run(() => blocker.Entered.Wait(2000));
            service.SetVolume(10); service.RequestRefresh(); blocker.Released.Set();
            await service.FlushAsync().WaitAsync(TimeSpan.FromSeconds(3));
            check("Resume refresh discards pending writes for the old audio endpoint", blocker.Writes == 0 && service.State.Volume == 40, "");
        }
        var oldDevice = new ResumeRoutingDevice(true); var newDevice = new ResumeRoutingDevice(false); var creations = 0;
        using (var service = new BackgroundAudioRouting(Application.Current.Dispatcher, () => ++creations == 1 ? oldDevice : newDevice))
        {
            var read = service.RefreshAsync(); await Task.Run(() => oldDevice.Entered.Wait(2000));
            var stale = service.SetVolumeAsync(new("old", "old", "old", new[] { "old-session" }, 40, false), 20);
            service.Reset(); oldDevice.Released.Set();
            await Task.WhenAll(read, stale).WaitAsync(TimeSpan.FromSeconds(3));
            await service.RefreshAsync().WaitAsync(TimeSpan.FromSeconds(3));
            check("Routing reset releases old devices and cancels stale mixer writes", oldDevice.Disposed && oldDevice.Writes == 0 && !stale.Result && creations == 2 && service.State.Available, "");
        }

        await VerifyUpdateDownloadsAsync(directory, check);
        var attempts = 0;
        var recoveredSession = new FakeMediaSession(PreviewData.Media());
        using (var media = new MediaService(Application.Current.Dispatcher, () => ++attempts == 1 ? throw new System.Runtime.InteropServices.COMException("Fixture unavailable") : new FakeMediaProvider(recoveredSession)))
        {
            await media.StartAsync(); var initiallyFailed = media.Error is not null;
            media.RequestRefresh(); await media.RefreshForDiagnosticsAsync().WaitAsync(TimeSpan.FromSeconds(3));
            check("Media initialization recovers after a transient service failure", initiallyFailed && media.Error is null && media.State.Available && attempts == 2, "");
        }
        var next = InstallService.Plan(Path.Combine(directory, "activation"), new(2, 0, 0));
        var previous = InstallService.Plan(next.Root, new(1, 0, 0));
        var runtime = new ActivationFixture(true, true);
        var result = await UpdateActivation.ActivateAsync(next, previous, runtime);
        check("A healthy update commits without restoring the previous version", result.Updated && !result.Restored && runtime.Launches.SequenceEqual(new[] { next.Executable }) && runtime.Restored is null, "");
        runtime = new(false, true); result = await UpdateActivation.ActivateAsync(next, previous, runtime);
        check("Failed update startup restores the previous registration and launches it", !result.Updated && result.Restored && runtime.Restored == previous && runtime.Launches.SequenceEqual(new[] { next.Executable, previous.Executable }), "");
        runtime = new(false, false); result = await UpdateActivation.ActivateAsync(next, null, runtime);
        check("Update failure without a prior installation reports failure without claiming rollback", !result.Updated && !result.Restored && runtime.Launches.Count == 1 && runtime.Restored is null, "");
        check("Rollback rejects an arbitrary or outside application path", UpdateActivation.PreviousInstallation(Environment.ProcessPath, Path.Combine(directory, "activation")) is null && UpdateActivation.PreviousInstallation("C:\\Windows\\notepad.exe", InstallService.DefaultRoot) is null, "");
        Strings.SetLanguage("tr"); Palette.Apply(new AppSettings());
    }

    private static async Task VerifyUpdateDownloadsAsync(string directory, Action<string, bool, string> check)
    {
        var body = Encoding.UTF8.GetBytes("Luma download verification fixture");
        var digest = Convert.ToHexString(SHA256.HashData(body));
        var package = new UpdatePackage(new(2, 0, 0), new("https://github.com/kyrerdm/Luma/releases/download/v2.0.0/Luma-2.0.0-Setup.exe"), body.Length, digest);
        var release = JsonSerializer.Serialize(new { tag_name = "v2.0.0", draft = false, prerelease = false, html_url = "https://github.com/kyrerdm/Luma/releases/tag/v2.0.0", assets = new[] { new { name = "Luma-2.0.0-Setup.exe", size = 80 * 1024 * 1024, digest = "sha256:" + digest, browser_download_url = package.Download.AbsoluteUri } } });
        var metadata = UpdateService.Parse(release, new(0, 7, 0));
        check("In-app updates require the exact release asset and GitHub SHA-256 digest", metadata.Package is not null && metadata.Package.Sha256 == digest && UpdateService.Parse(release.Replace("sha256:", "unknown:"), new(0, 7, 0)).Package is null && UpdateService.Parse(release.Replace("download/v2.0.0", "download/v9.0.0"), new(0, 7, 0)).Package is null, "");
        var target = Path.Combine(directory, "update-download-test");
        using (var updates = new UpdateService(new DownloadFixture(body)))
        {
            var file = await updates.DownloadAsync(package, target);
            check("Downloaded update is committed only after size and SHA-256 verification", File.ReadAllBytes(file).SequenceEqual(body) && !Directory.EnumerateFiles(target, "*.part").Any(), "The fixture is data, never executed.");
        }
        var rejected = false;
        using (var updates = new UpdateService(new DownloadFixture(Encoding.UTF8.GetBytes(new string('x', body.Length)))))
        { try { await updates.DownloadAsync(package, target); } catch (InvalidDataException) { rejected = true; } }
        check("Tampered updates are removed and cannot replace a verified download", rejected && File.ReadAllBytes(Path.Combine(target, "Luma-2.0.0-Setup.exe")).SequenceEqual(body) && !Directory.EnumerateFiles(target, "*.part").Any(), "");
        rejected = false;
        using (var updates = new UpdateService(new DownloadFixture(body[..^1])))
        { try { await updates.DownloadAsync(package, target); } catch (InvalidDataException) { rejected = true; } }
        check("Truncated update downloads fail without leaving partial files", rejected && !Directory.EnumerateFiles(target, "*.part").Any(), "");
        rejected = false;
        using (var updates = new UpdateService(new DownloadFixture(body, "https://untrusted.example/update.exe")))
        { try { await updates.DownloadAsync(package, target); } catch (HttpRequestException) { rejected = true; } }
        check("Update redirects to unrelated hosts are refused", rejected && !UpdateService.TrustedDownloadRedirect(new("http://release-assets.githubusercontent.com/file")) && !UpdateService.TrustedDownloadRedirect(new("https://github.com.evil.example/file")), "");
        using (var updates = new UpdateService(new DownloadFixture(body, "https://release-assets.githubusercontent.com/fixture")))
        { var file = await updates.DownloadAsync(package, target); check("Verified HTTPS GitHub asset redirects are supported", File.ReadAllBytes(file).SequenceEqual(body), ""); }
        using var cancellation = new CancellationTokenSource(); cancellation.Cancel(); rejected = false;
        using (var updates = new UpdateService(new DownloadFixture(body)))
        { try { await updates.DownloadAsync(package, target, cancellation: cancellation.Token); } catch (OperationCanceledException) { rejected = true; } }
        check("Cancelled updates leave the current version and verified file untouched", rejected && !Directory.EnumerateFiles(target, "*.part").Any() && File.ReadAllBytes(Path.Combine(target, "Luma-2.0.0-Setup.exe")).SequenceEqual(body), "");
    }

    private sealed class DownloadFixture(byte[] body, string? redirect = null) : HttpMessageHandler
    {
        private int requests;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (redirect is not null && requests++ == 0) { var response = new HttpResponseMessage(HttpStatusCode.Found); response.Headers.Location = new Uri(redirect); return Task.FromResult(response); }
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(body) });
        }
    }
    private sealed class ActivationFixture(bool first, bool second) : IUpdateRuntime
    {
        public readonly List<string> Launches = new();
        public InstallPlan? Restored;
        public Task<bool> StartAndWaitAsync(string executable) { Launches.Add(executable); return Task.FromResult(Launches.Count == 1 ? first : second); }
        public void RestoreRegistration(InstallPlan plan) => Restored = plan;
    }
    private sealed class ResumeAudioDevice : IAudioDevice
    {
        public readonly ManualResetEventSlim Entered = new(), Released = new();
        public volatile bool Block; public int Writes;
        public AudioState Read() { if (Block) { Entered.Set(); Released.Wait(3000); } return new(true, 40, false, "Fixture"); }
        public bool SetVolume(int volume) { Writes++; return true; }
        public bool ToggleMute() { Writes++; return true; }
        public void Dispose() { }
    }
    private sealed class ResumeRoutingDevice(bool block) : IAudioRoutingDevice
    {
        public readonly ManualResetEventSlim Entered = new(), Released = new();
        public int Writes; public bool Disposed;
        public AudioRoutingState ReadRouting() { if (block) { Entered.Set(); Released.Wait(3000); } return new(Array.Empty<AudioOutput>(), Array.Empty<AudioApp>(), "Fixture"); }
        public bool SelectOutput(string id) { Writes++; return true; }
        public bool SetAppVolume(AudioApp app, int value) { Writes++; return true; }
        public bool SetAppMute(AudioApp app, bool muted) { Writes++; return true; }
        public void Dispose() => Disposed = true;
    }
}
