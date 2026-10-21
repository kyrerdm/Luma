using System;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Luma.Models;
using Luma.Services;
using Luma.Views;

namespace Luma;

internal static partial class Diagnostics
{
    private static async Task VerifyDistributionAsync(string directory, Action<string, bool, string> check)
    {
        var root = Path.Combine(directory, "store-migration-" + Guid.NewGuid().ToString("N"));
        var source = new SettingsStore(Path.Combine(root, "legacy"));
        source.Save(new AppSettings { Language = "de", Accent = "Rose", WelcomeCompleted = true, VolumeStep = 6 });
        var primary = File.ReadAllBytes(source.FilePath);
        var backup = File.ReadAllBytes(source.BackupPath);
        var destination = new SettingsStore(Path.Combine(root, "packaged"));
        var migrated = Distribution.TryMigrateSettings(destination.DirectoryPath, source.DirectoryPath);
        var copied = destination.Load();
        check("First Store launch copies valid legacy preferences and language", migrated && copied.Language == "de" && copied.Accent == "Rose" && copied.VolumeStep == 6 && copied.WelcomeCompleted, "Isolated directories only; no user preferences are modified.");
        check("Store migration leaves both legacy preference files byte-for-byte intact", primary.SequenceEqual(File.ReadAllBytes(source.FilePath)) && backup.SequenceEqual(File.ReadAllBytes(source.BackupPath)), "");
        copied.Language = "it"; destination.Save(copied);
        check("A later Store launch never overwrites existing Store preferences", !Distribution.TryMigrateSettings(destination.DirectoryPath, source.DirectoryPath) && destination.Load().Language == "it", "");
        var backupOnly = new SettingsStore(Path.Combine(root, "backup-only"));
        Directory.CreateDirectory(backupOnly.DirectoryPath); File.WriteAllBytes(backupOnly.BackupPath, backup);
        check("An existing Store backup is preserved even when its primary is missing", !Distribution.TryMigrateSettings(backupOnly.DirectoryPath, source.DirectoryPath) && !File.Exists(backupOnly.FilePath) && File.ReadAllBytes(backupOnly.BackupPath).SequenceEqual(backup), "");
        File.WriteAllText(source.FilePath, "{broken");
        var recovered = new SettingsStore(Path.Combine(root, "recovered"));
        check("Legacy recovery imports the valid backup instead of a corrupt primary", Distribution.TryMigrateSettings(recovered.DirectoryPath, source.DirectoryPath) && recovered.Load().Language == "de", "");
        File.WriteAllText(source.BackupPath, "{broken");
        var invalidTarget = Path.Combine(root, "invalid-target");
        check("Damaged legacy preferences are not imported as a completed first run", !Distribution.TryMigrateSettings(invalidTarget, source.DirectoryPath) && !Directory.Exists(invalidTarget), "");
        var fresh = Path.Combine(root, "fresh");
        check("A fresh Store user keeps English onboarding without creating settings", !Distribution.TryMigrateSettings(fresh, Path.Combine(root, "missing")) && !Directory.Exists(fresh) && new SettingsStore(fresh).Load().Language == "en" && !new SettingsStore(fresh).Load().WelcomeCompleted, "");

        using var requests = new StoreRequestProbe();
        using var updater = new UpdateService(requests, packaged: true);
        var update = await updater.CheckAsync();
        check("The Store channel blocks GitHub update checks before any HTTP request", !update.Available && update.Package is null && update.Page is null && update.Message == Distribution.StoreUpdatesMessage && requests.Count == 0, "");
        var downloads = Path.Combine(root, "downloads");
        var refused = false;
        try { await updater.DownloadAsync(new(new(2, 0, 0), new("https://github.com/kyrerdm/Luma/releases/download/v2.0.0/Luma-2.0.0-Setup.exe"), 1, new string('a', 64)), downloads); }
        catch (InvalidOperationException) { refused = true; }
        check("The Store channel refuses EXE updates before network or disk writes", refused && requests.Count == 0 && !Directory.Exists(downloads), "");

        var previousLanguage = Strings.Language;
        var window = new SettingsWindow(new AppSettings(), () => { }) { AllowClose = true };
        try
        {
            var valid = true;
            foreach (var language in Strings.Languages.Keys)
            {
                Strings.SetLanguage(language); window.ConfigureDistribution(true);
                valid &= ((Button)window.FindName("CheckUpdatesButton")).Visibility == Visibility.Collapsed
                    && ((Button)window.FindName("InstallUpdateButton")).Visibility == Visibility.Collapsed
                    && ((Button)window.FindName("InstallButton")).Visibility == Visibility.Collapsed
                    && ((Button)window.FindName("StoreUpdatesButton")).Visibility == Visibility.Visible
                    && ((FrameworkElement)window.FindName("AutomaticUpdatesRow")).Visibility == Visibility.Collapsed
                    && ((Button)window.FindName("UpdateBadge")).Visibility == Visibility.Collapsed
                    && ((TextBlock)window.FindName("UpdateStatusText")).Text == Strings.T(Distribution.StoreUpdatesMessage);
            }
            check("Store settings expose Store updates and hide EXE installation in all five languages", valid, "UI policy fixture; actual package identity is verified separately during deployment.");
        }
        finally { window.Close(); Strings.SetLanguage(previousLanguage); }
    }

    private sealed class StoreRequestProbe : HttpMessageHandler
    {
        public int Count;
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Count++;
            throw new InvalidOperationException("The Store channel must not make an updater request.");
        }
    }
}
