using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Luma.Models;
using Luma.Services;
using Luma.Themes;

namespace Luma;

internal static partial class Diagnostics
{
    private static async Task VerifyUpdateIndicatorsAsync(string directory, Action<string, bool, string> check)
    {
        var ui = Application.Current.Dispatcher;
        var prefs = new AppSettings { AutomaticUpdateChecks = false };
        var store = new SettingsStore(Path.Combine(directory, "update-preferences-" + Guid.NewGuid().ToString("N")));
        store.Save(prefs);
        check("Automatic update checks default on and a disabled choice survives save and export", new AppSettings().AutomaticUpdateChecks &&
            !store.Load().AutomaticUpdateChecks && !PreferenceTransfer.Parse(PreferenceTransfer.Export(prefs)).AutomaticUpdateChecks, "");

        var autoRequests = new UpdateNoticeHandler();
        using var automatic = new UpdateMonitor(ui, new UpdateService(autoRequests, packaged: false), packaged: false);
        automatic.SetEnabled(true);
        check("Background checks wait fifteen seconds before the first request", automatic.AutomaticChecksEnabled &&
            automatic.NextCheckDelay == TimeSpan.FromSeconds(15) && autoRequests.Requests == 0, "The real dispatcher timer is exercised below.");

        var probe = new UpdateNoticeHandler { Gate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        using (var monitor = new UpdateMonitor(ui, new UpdateService(probe, packaged: false), packaged: false))
        {
            var notices = 0;
            monitor.Changed += _ => notices++;
            var first = monitor.CheckAsync();
            var second = monitor.CheckAsync();
            check("Concurrent manual and scheduled checks share one metadata request", ReferenceEquals(first, second) && probe.Requests == 1, "");
            probe.Gate.SetResult();
            var result = await first.WaitAsync(TimeSpan.FromSeconds(3));
            probe.Gate = null;
            check("A verified stable release publishes an available update without a download", result.Verified && result.Available &&
                monitor.Current?.Package?.Version == new Version(9, 0, 0) && notices == 1 && probe.OnlyMetadata, "");
            probe.Fail = true;
            var offline = await monitor.CheckAsync();
            check("Offline checks retain the last verified update and do not repeat its notice", !offline.Verified && monitor.Current?.Available == true && notices == 1, "");
            probe.Fail = false;
            probe.Json = UpdateNoticeJson(new(9, 0, 0), draft: true);
            await monitor.CheckAsync();
            check("Draft release metadata cannot replace an already verified update", monitor.Current?.Package?.Version == new Version(9, 0, 0) && notices == 1, "");
            probe.Json = UpdateNoticeJson(AppVersion.Current);
            await monitor.CheckAsync();
            check("A verified current version clears a stale update indicator", monitor.Current is { Available: false, Verified: true } && notices == 2, "");
        }

        var storeRequests = new UpdateNoticeHandler();
        using (var monitor = new UpdateMonitor(ui, new UpdateService(storeRequests, packaged: false), packaged: true))
        {
            monitor.SetEnabled(true);
            await monitor.CheckAsync();
            check("Store update monitoring blocks both scheduled and manual GitHub requests", !monitor.AutomaticChecksEnabled &&
                monitor.Current is null && storeRequests.Requests == 0, "Microsoft Store continues to manage its own updates.");
        }
        var cancellationProbe = new UpdateNoticeHandler { Gate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        var cancellable = new UpdateMonitor(ui, new UpdateService(cancellationProbe, packaged: false), packaged: false);
        var afterClose = 0;
        cancellable.Changed += _ => afterClose++;
        var pending = cancellable.CheckAsync();
        cancellable.Dispose();
        await pending.WaitAsync(TimeSpan.FromSeconds(3));
        check("Closing the monitor cancels an in-flight request without a late notification", afterClose == 0 && !cancellable.AutomaticChecksEnabled, "");

        var controllerProbe = new UpdateNoticeHandler();
        var updates = new UpdateMonitor(ui, new UpdateService(controllerProbe, packaged: false), packaged: false);
        var data = Path.Combine(directory, "update-ui-" + Guid.NewGuid().ToString("N"));
        new SettingsStore(data).Save(new AppSettings { Language = "en", WelcomeCompleted = true, Animations = false });
        using (var runtime = new AppController(data, () => new DelayedAudioDevice(), () => new DiagnosticBrightnessDevice(),
            () => new FakeMediaProvider(), updates, enableAutomaticUpdates: true))
        {
            var window = runtime.DiagnosticSettings;
            var badge = (Button)window.FindName("UpdateBadge");
            var menu = runtime.DiagnosticTrayMenu;
            check("No update decoration or extra tray action appears before a release is found", badge.Visibility == Visibility.Collapsed &&
                menu.Items.Count == 2 && !window.IsVisible, "");
            await updates.CheckAsync();
            check("A background result decorates the tray and settings without opening a window", runtime.DiagnosticUpdateIndicator &&
                badge.Visibility == Visibility.Visible && menu.Items.Count == 3 && menu.Items[1].Name == "Update" &&
                runtime.DiagnosticTray.Text.Contains("Update available") && !window.IsVisible, "No toast, forced focus, download or installation.");
            ((System.Windows.Forms.ToolStripMenuItem)menu.Items[1]).PerformClick();
            await Task.Delay(80);
            check("The tray update action opens settings at the update section", window.IsVisible && window.CurrentSection == "General", "");
            window.Navigate("Appearance");
            badge.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            check("The header update badge opens the update section from another page", window.CurrentSection == "General", "");

            var fits = true;
            window.Width = window.MinWidth;
            window.Height = window.MinHeight;
            foreach (var language in Strings.Languages.Keys)
            {
                Strings.SetLanguage(language);
                window.UpdateLayout();
                var heading = (FrameworkElement)VisualTreeHelper.GetParent(badge);
                var rectangle = badge.TransformToAncestor(heading).TransformBounds(new Rect(badge.RenderSize));
                fits &= rectangle.Left >= -1 && rectangle.Right <= heading.ActualWidth + 1 &&
                    rectangle.Bottom <= heading.ActualHeight + 1 && runtime.DiagnosticTray.Text.Contains(Strings.T("Güncelleme var")) &&
                    menu.Items[1].Text == Strings.T("Güncelleme var") && ((Button)window.FindName("InstallUpdateButton")).Visibility == Visibility.Visible;
            }
            check("Update badges and tray actions translate and fit the minimum window in five languages", fits, "");
            Strings.SetLanguage("en");
            foreach (var theme in new[] { "Dark", "Light" })
            {
                Palette.Apply(new AppSettings { Theme = theme });
                window.ApplyAppearance();
                window.UpdateLayout();
                Render(window, Path.Combine(directory, "luma-update-" + theme.ToLowerInvariant() + ".png"));
            }
            check("The tray icon includes a visible update dot at small icon sizes", HasUpdateDot(16) && HasUpdateDot(32), "");
            ((CheckBox)window.FindName("AutomaticUpdatesToggle")).IsChecked = false;
            check("Disabling automatic checks stops the timer and keeps an already found update available", !updates.AutomaticChecksEnabled &&
                !new SettingsStore(data).Load().AutomaticUpdateChecks && badge.Visibility == Visibility.Visible, "");
            controllerProbe.Fail = true;
            ((Button)window.FindName("CheckUpdatesButton")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Task.Delay(50);
            check("A failed manual recheck keeps the existing update actionable", badge.Visibility == Visibility.Visible &&
                ((Button)window.FindName("InstallUpdateButton")).Visibility == Visibility.Visible &&
                ((Button)window.FindName("CheckUpdatesButton")).IsEnabled, "");
            controllerProbe.Fail = false;
            controllerProbe.Json = UpdateNoticeJson(AppVersion.Current);
            await updates.CheckAsync();
            check("Current-version metadata restores the original tray and removes update actions", badge.Visibility == Visibility.Collapsed &&
                !runtime.DiagnosticUpdateIndicator && menu.Items.Count == 2 && runtime.DiagnosticTray.Text == "Luma", "");
            check("Update notices request only release metadata and never call a download endpoint", controllerProbe.OnlyMetadata, "");
        }

        var clock = System.Diagnostics.Stopwatch.StartNew();
        while (autoRequests.Requests == 0 && clock.Elapsed < TimeSpan.FromSeconds(18)) await Task.Delay(25);
        check("The real background timer checks once then schedules six hours later", autoRequests.Requests == 1 && automatic.Current?.Available == true &&
            automatic.NextCheckDelay == TimeSpan.FromHours(6) && autoRequests.OnlyMetadata, "");
        automatic.SetEnabled(false);
        var count = autoRequests.Requests;
        await Task.Delay(50);
        check("Disabling scheduled checks leaves no running timer or new request", !automatic.AutomaticChecksEnabled && autoRequests.Requests == count, "");
        Palette.Apply(new AppSettings());
        Strings.SetLanguage("tr");
    }

    private static bool HasUpdateDot(int size)
    {
        var image = BrandIcon.Render((ImageSource)Application.Current.FindResource("BrandAppIcon"), size, updateAvailable: true);
        var pixel = new byte[4];
        image.CopyPixels(new Int32Rect((int)(size * 0.79), (int)(size * 0.79), 1, 1), pixel, 4, 0);
        return pixel[1] > 180 && pixel[1] > pixel[2] + 30 && pixel[3] > 240;
    }

    private static string UpdateNoticeJson(Version version, bool draft = false) => JsonSerializer.Serialize(new
    {
        tag_name = "v" + version.ToString(3), draft, prerelease = false,
        html_url = "https://github.com/kyrerdm/Luma/releases/tag/v" + version.ToString(3),
        assets = new[] { new { name = "Luma-" + version.ToString(3) + "-Setup.exe", size = 80 * 1024 * 1024,
            digest = "sha256:" + new string('a', 64),
            browser_download_url = "https://github.com/kyrerdm/Luma/releases/download/v" + version.ToString(3) + "/Luma-" + version.ToString(3) + "-Setup.exe" } }
    });

    private sealed class UpdateNoticeHandler : HttpMessageHandler
    {
        public int Requests;
        public bool OnlyMetadata = true;
        public bool Fail;
        public string Json = UpdateNoticeJson(new(9, 0, 0));
        public TaskCompletionSource? Gate;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref Requests);
            OnlyMetadata &= request.Method == HttpMethod.Get && request.RequestUri?.AbsoluteUri == "https://api.github.com/repos/kyrerdm/Luma/releases/latest";
            if (Gate is not null) await Gate.Task.WaitAsync(cancellationToken);
            if (Fail) throw new HttpRequestException("Offline fixture");
            return new(HttpStatusCode.OK) { Content = new StringContent(Json) };
        }
    }
}
