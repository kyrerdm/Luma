using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows;
using Luma.Services;

namespace Luma;

public partial class App : Application
{
    private Mutex? instanceMutex;
    private EventWaitHandle? openSettings;
    private RegisteredWaitHandle? openSettingsRegistration;
    private AppController? controller;

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
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

        instanceMutex = new Mutex(true, "Local\\Luma.Flyouts.Instance", out var created);
        openSettings = new EventWaitHandle(false, EventResetMode.AutoReset, "Local\\Luma.Flyouts.OpenSettings");
        if (!created) { openSettings.Set(); instanceMutex.Dispose(); instanceMutex = null; Shutdown(); return; }

        var dataIndex = Array.IndexOf(e.Args, "--data-dir");
        var dataDirectory = dataIndex >= 0 && dataIndex + 1 < e.Args.Length ? Path.GetFullPath(e.Args[dataIndex + 1])
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LumaFlyouts");
        try
        {
            StartupService.RefreshEnabledPath();
            controller = new AppController(dataDirectory);
            openSettingsRegistration = ThreadPool.RegisterWaitForSingleObject(openSettings,
                (_, _) => Dispatcher.BeginInvoke(new Action(() => controller?.ShowSettings())), null, Timeout.Infinite, false);
            if (!e.Args.Contains("--background")) controller.ShowSettings();
        }
        catch (Exception ex)
        {
            controller?.Dispose();
            MessageBox.Show("Luma başlatılamadı.\n\n" + ex.Message, "Luma", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        openSettingsRegistration?.Unregister(null);
        controller?.Dispose();
        openSettings?.Dispose();
        if (instanceMutex is not null) { instanceMutex.ReleaseMutex(); instanceMutex.Dispose(); }
        base.OnExit(e);
    }
}
