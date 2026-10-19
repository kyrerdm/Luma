using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using Luma.Controls;
using Luma.Models;
using Luma.Services;
using Luma.Themes;
using Luma.Views;

namespace Luma;

internal static partial class Diagnostics
{
    private static async Task VerifyStabilityAsync(string directory, Action<string, bool, string> check)
    {
        var root = Path.Combine(directory, "recovery-" + Guid.NewGuid().ToString("N"));
        var store = new SettingsStore(Path.Combine(root, "normal"));
        var fresh = store.Load();
        check("A new preference store neither creates files nor reports recovery", !fresh.WelcomeCompleted && store.Recovery == SettingsRecovery.None && !Directory.Exists(store.DirectoryPath), "");
        var first = new AppSettings { Accent = "Rose", Position = "TopLeft", WelcomeCompleted = true, VolumeStep = 3 };
        store.Save(first);
        var original = File.ReadAllBytes(store.FilePath);
        check("The first successful save also creates a valid recovery copy", File.ReadAllBytes(store.BackupPath).SequenceEqual(original) && store.Load().Accent == "Rose", "");
        store.Save(new AppSettings { Accent = "Mint", Position = "BottomRight", WelcomeCompleted = true });
        check("Saving rotates the previous complete preferences into the recovery copy", File.ReadAllBytes(store.BackupPath).SequenceEqual(original) && store.Load().Accent == "Mint", "");
        File.WriteAllText(store.FilePath, "{broken");
        var recovered = store.Load();
        check("A corrupt primary recovers the last valid appearance and behavior", recovered.Accent == "Rose" && recovered.Position == "TopLeft" && recovered.VolumeStep == 3 && recovered.WelcomeCompleted && store.Recovery == SettingsRecovery.Backup, "");
        check("Reading recovered preferences does not overwrite the damaged file or backup", File.ReadAllText(store.FilePath) == "{broken" && File.ReadAllBytes(store.BackupPath).SequenceEqual(original), "");
        recovered.Accent = "Amber";
        store.Save(recovered);
        check("Saving after recovery preserves the good backup and commits the new preferences", store.Recovery == SettingsRecovery.None && store.Load().Accent == "Amber" && File.ReadAllBytes(store.BackupPath).SequenceEqual(original), "");
        File.Delete(store.FilePath);
        check("A missing primary can recover an existing valid backup", store.Load().Accent == "Rose" && store.Recovery == SettingsRecovery.Backup, "");
        var invalid = new[] { "null", "[]", "\"wrong\"", "{\"VolumeKeys\":\"wrong\"}", new string('x', SettingsStore.MaxBytes + 1), string.Concat(Enumerable.Repeat("{\"nested\":", 24)) + "{}" + new string('}', 24) };
        var recoveredAll = true;
        foreach (var value in invalid) { File.WriteAllText(store.FilePath, value); recoveredAll &= store.Load().Accent == "Rose" && store.Recovery == SettingsRecovery.Backup; }
        check("Invalid roots, types, excessive nesting and oversized settings recover safely", recoveredAll, "Only files in the isolated diagnostic directory are modified.");
        File.WriteAllText(store.BackupPath, "{broken too");
        var reset = store.Load();
        check("If both copies are damaged the app reports defaults without repeating onboarding", store.Recovery == SettingsRecovery.Defaults && reset.VolumeKeys && reset.WelcomeCompleted, "");
        File.WriteAllText(store.FilePath, "{\"Accent\":\"Lilac\",\"Position\":\"invalid\"}", new UTF8Encoding(true));
        var legacy = store.Load();
        check("Valid BOM-prefixed legacy settings take priority over an invalid backup", store.Recovery == SettingsRecovery.None && legacy.Accent == "Lilac" && legacy.Position == "BottomCenter" && legacy.WelcomeCompleted, "");

        var blocked = new SettingsStore(Path.Combine(root, "blocked"));
        blocked.Save(first);
        var safeBackup = File.ReadAllBytes(blocked.BackupPath);
        File.Delete(blocked.FilePath); Directory.CreateDirectory(blocked.FilePath);
        var refused = false;
        try { blocked.Save(new AppSettings { Accent = "Ocean" }); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { refused = true; }
        check("A failed primary commit preserves the recovery copy and removes its own staging files", refused && File.ReadAllBytes(blocked.BackupPath).SequenceEqual(safeBackup) && !Directory.EnumerateFiles(blocked.DirectoryPath, "*.tmp*").Any() && blocked.Load().Accent == "Rose", "");
        var rotation = new SettingsStore(Path.Combine(root, "rotation"));
        rotation.Save(first); File.Delete(rotation.BackupPath); Directory.CreateDirectory(rotation.BackupPath);
        refused = false;
        try { rotation.Save(new AppSettings { Accent = "Ocean" }); } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { refused = true; }
        check("A failed backup rotation leaves the primary preferences usable", refused && rotation.Load().Accent == "Rose" && !Directory.EnumerateFiles(rotation.DirectoryPath, "*.tmp*").Any(), "");
        var parallel = new SettingsStore(Path.Combine(root, "parallel"));
        parallel.Save(first);
        await Task.WhenAll(Enumerable.Range(0, 12).Select(i => Task.Run(() => parallel.Save(new AppSettings { VolumeStep = i % 10 + 1, WelcomeCompleted = true }))));
        File.WriteAllText(parallel.FilePath, "{broken");
        check("Concurrent saves still leave a complete recoverable snapshot", parallel.Load().WelcomeCompleted && parallel.Recovery == SettingsRecovery.Backup && !Directory.EnumerateFiles(parallel.DirectoryPath, "*.tmp*").Any(), "");

        Strings.SetLanguage("tr"); var prefs = new AppSettings { WelcomeCompleted = true, Animations = false }; Palette.Apply(prefs);
        var saves = 0;
        var window = new SettingsWindow(prefs, () => saves++) { AllowClose = true };
        SeedSettingsPreview(window); window.SetSettingsRecovery(SettingsRecovery.Backup); window.Show();
        await Task.Delay(50);
        try
        {
            var notice = (Border)window.FindName("RecoveryNotice");
            var message = (TextBlock)window.FindName("RecoveryText");
            check("Recovery is visible in settings without silently saving over existing files", notice.IsVisible && message.Text.Contains("yedekten") && saves == 0, "");
            Render(window, Path.Combine(directory, "luma-settings-recovery.png"));
            var controls = new[] { (PreviewSlider)window.FindName("PreviewVolumeSlider"), (PreviewSlider)window.FindName("PreviewTimeline") };
            var readOnly = true;
            foreach (var control in controls)
            {
                var peer = FrameworkElementAutomationPeer.CreatePeerForElement(control)!;
                var range = (IRangeValueProvider)peer.GetPattern(PatternInterface.RangeValue);
                var before = control.Value; refused = false;
                try { range.SetValue(control.Minimum + 1); } catch (InvalidOperationException) { refused = true; }
                readOnly &= range.IsReadOnly && refused && control.Value == before && !control.Focusable && !control.IsHitTestVisible && !peer.IsKeyboardFocusable() && peer.GetAutomationControlType() == AutomationControlType.ProgressBar && (peer.GetChildren()?.Count ?? 0) == 0 && peer.GetPattern(PatternInterface.Invoke) is null;
            }
            check("Preview meters expose read-only values and no actionable child controls", readOnly && saves == 0, "No volume, seek, input or clipboard commands are sent.");
            window.UpdatePreviewAudio(new(true, 73, false, "Fixture"));
            var volumeRange = (IRangeValueProvider)FrameworkElementAutomationPeer.CreatePeerForElement(controls[0])!.GetPattern(PatternInterface.RangeValue);
            check("Read-only preview providers still report live audio values", volumeRange.Value == 73 && volumeRange.Minimum == 0 && volumeRange.Maximum == 100, "");
            var regular = new SmoothSlider { Minimum = 0, Maximum = 100, Value = 40 };
            var regularPeer = FrameworkElementAutomationPeer.CreatePeerForElement(regular)!;
            var interactive = (IRangeValueProvider)regularPeer.GetPattern(PatternInterface.RangeValue);
            interactive.SetValue(45);
            check("Normal sliders retain their writable accessibility behavior", !interactive.IsReadOnly && regular.Value == 45, "The regular slider is an isolated control with no system command handlers.");
            Strings.SetLanguage("en");
            check("Recovery notices and passive meter descriptions translate live", message.Text.Contains("last valid backup") && AutomationProperties.GetName(controls[0]) == "Live volume level" && AutomationProperties.GetHelpText(controls[1]) == "Preview; display only." && volumeRange.IsReadOnly, "");
            ((Button)window.FindName("DismissRecoveryButton")).RaiseEvent(new RoutedEventArgs(ButtonBase.ClickEvent));
            check("Dismissing recovery information leaves the preferences untouched", notice.Visibility == Visibility.Collapsed && saves == 0 && prefs.Accent == "Ice", "");
            window.SetSettingsRecovery(SettingsRecovery.Defaults);
            check("Unrecoverable preferences explain the fallback rather than claiming recovery", message.Text.Contains("defaults were loaded") && notice.IsVisible, "");
            window.SetSettingsRecovery(SettingsRecovery.None);
            check("Successful saving can clear the recovery notice", notice.Visibility == Visibility.Collapsed, "");
            var report = SupportReport.Create(prefs, true, false, 1, 2, 3, SettingsRecovery.Backup);
            check("Support reports summarize recovery without including backup contents or locations", report.Contains("Preference recovery: backup") && !report.Contains(root, StringComparison.OrdinalIgnoreCase) && !report.Contains("settings.json") && !report.Contains(first.Position), "");
        }
        finally { window.Close(); Strings.SetLanguage("tr"); Palette.Apply(new AppSettings()); }
    }
}
