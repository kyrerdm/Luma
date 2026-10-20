using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Windows;
using Luma.Services;
using Luma.Views;
using Luma.Themes;

namespace Luma;

public partial class App : Application
{
    private Mutex? instanceMutex;
    private EventWaitHandle? openSettings;
    private RegisteredWaitHandle? openSettingsRegistration;
    private EventWaitHandle? exitSignal;
    private RegisteredWaitHandle? exitRegistration;
    private AppController? controller;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        var artworkIndex = Array.IndexOf(e.Args, "--export-store-artwork");
        if (artworkIndex >= 0)
        {
            var output = artworkIndex + 1 < e.Args.Length ? Path.GetFullPath(e.Args[artworkIndex + 1]) : Path.Combine(AppContext.BaseDirectory, "store-artwork");
            try { Shutdown(await Diagnostics.CreateStoreArtworkAsync(output)); }
            catch (Exception ex) { Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, "failure.txt"), ex.ToString()); Shutdown(1); }
            return;
        }
        var osdIndex = Array.IndexOf(e.Args, "--diagnose-brightness-osd");
        if (osdIndex >= 0)
        {
            var output = osdIndex + 1 < e.Args.Length ? Path.GetFullPath(e.Args[osdIndex + 1]) : Path.Combine(AppContext.BaseDirectory, "osd-diagnostics");
            try { Shutdown(await Diagnostics.WatchNativeBrightnessOsdAsync(output)); }
            catch (Exception ex) { Directory.CreateDirectory(output); File.WriteAllText(Path.Combine(output, "failure.txt"), ex.ToString()); Shutdown(1); }
            return;
        }
        var testIndex = Array.IndexOf(e.Args, "--self-test");
        if (testIndex >= 0)
        {
            var output = testIndex + 1 < e.Args.Length ? e.Args[testIndex + 1] : Path.Combine(AppContext.BaseDirectory, "test-results");
            try { Shutdown(await Diagnostics.RunAsync(Path.GetFullPath(output))); }
            catch (Exception ex)
            {
                Directory.CreateDirectory(output);
                File.WriteAllText(Path.Combine(output, "failure.txt"), ex.ToString());
                Shutdown(1);
            }
            return;
        }

        if (!Distribution.IsPackaged && (e.Args.Contains("--install") || e.Args.Contains("--apply-update") || IsSetupExecutable(Environment.ProcessPath ?? "")))
        {
            var store = new Luma.Models.SettingsStore(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LumaFlyouts"));
            Palette.Apply(store.Load());
            Strings.SetLanguage(store.Load().Language);
            var previousIndex = Array.IndexOf(e.Args, "--previous");
            new InstallWindow(e.Args.Contains("--apply-update"), previousIndex >= 0 && previousIndex + 1 < e.Args.Length ? e.Args[previousIndex + 1] : null).ShowDialog(); Shutdown(); return;
        }
        instanceMutex = new Mutex(true, "Local\\Luma.Flyouts.Instance", out var created);
        openSettings = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\Luma.Flyouts.OpenSettings");
        if (!created && e.Args.Contains("--wait-for-instance"))
        {
            for (var attempt = 0; attempt < 100 && !created; attempt++)
            {
                try { created = instanceMutex.WaitOne(0); }
                catch (AbandonedMutexException) { created = true; }
                if (!created) await Task.Delay(100);
            }
        }
        if (!created) { if (!e.Args.Contains("--background")) openSettings.Set(); instanceMutex.Dispose(); instanceMutex = null; Shutdown(); return; }

        var dataIndex = Array.IndexOf(e.Args, "--data-dir");
        var dataDirectory = dataIndex >= 0 && dataIndex + 1 < e.Args.Length ? Path.GetFullPath(e.Args[dataIndex + 1])
            : Distribution.DataDirectory;
        try
        {
            if (Distribution.IsPackaged && dataIndex < 0) Distribution.TryMigrateSettings(dataDirectory, Distribution.LegacyDataDirectory);
            StartupService.RefreshEnabledPath();
            controller = new AppController(dataDirectory);
            openSettingsRegistration = ThreadPool.RegisterWaitForSingleObject(openSettings,
                (_, _) => Dispatcher.BeginInvoke(new Action(() => controller?.ShowSettings())), null, Timeout.Infinite, false);
            exitSignal = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\Luma.Flyouts.Exit");
            exitRegistration = ThreadPool.RegisterWaitForSingleObject(exitSignal,
                (_, _) => Dispatcher.BeginInvoke(new Action(() => Shutdown())), null, Timeout.Infinite, false);
            var readyIndex = Array.IndexOf(e.Args, "--ready-event");
            if (readyIndex >= 0 && readyIndex + 1 < e.Args.Length && System.Text.RegularExpressions.Regex.IsMatch(e.Args[readyIndex + 1], @"^Local\\Luma\.Update\.Ready\.[a-f0-9]{32}$"))
            {
                var readyName = e.Args[readyIndex + 1];
                _ = Dispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle, new Action(() => { try { using var signal = EventWaitHandle.OpenExisting(readyName); signal.Set(); } catch (WaitHandleCannotBeOpenedException) { } }));
            }
            if (!e.Args.Contains("--background")) controller.ShowInitial();
        }
        catch (Exception ex)
        {
            controller?.Dispose();
            MessageBox.Show(Strings.T("Luma başlatılamadı.") + "\n\n" + ex.Message, "Luma", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        exitRegistration?.Unregister(null); exitSignal?.Dispose();
        openSettingsRegistration?.Unregister(null);
        controller?.Dispose();
        openSettings?.Dispose();
        if (instanceMutex is not null) { instanceMutex.ReleaseMutex(); instanceMutex.Dispose(); }
        base.OnExit(e);
    }
    internal static void LaunchInstalled(string executable)
    {
        using var launched = Process.Start(new ProcessStartInfo(executable, "--wait-for-instance") { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(executable), WindowStyle = ProcessWindowStyle.Hidden });
        try { using var signal = EventWaitHandle.OpenExisting("Local\\Luma.Flyouts.Exit"); signal.Set(); }
        catch (WaitHandleCannotBeOpenedException)
        {
            // Versions before 0.6 have no exit signal. Close only an older Luma
            // belonging to this session; settings were already saved on change.
            using var current = Process.GetCurrentProcess();
            foreach (var process in Process.GetProcessesByName("Luma"))
            {
                using (process)
                {
                    try
                    {
                        if (process.Id == current.Id || process.SessionId != current.SessionId) continue;
                        var info = process.MainModule?.FileVersionInfo;
                        if (info?.ProductName == "Luma" && info.FileMajorPart == 0 && info.FileMinorPart < 6) process.Kill();
                    }
                    catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { }
                }
            }
        }
    }
    internal static bool IsSetupExecutable(string path) => System.Text.RegularExpressions.Regex.IsMatch(Path.GetFileNameWithoutExtension(path), @"^Luma-\d+\.\d+\.\d+-Setup(?: \(\d+\))?$", System.Text.RegularExpressions.RegexOptions.IgnoreCase | System.Text.RegularExpressions.RegexOptions.CultureInvariant);
}
