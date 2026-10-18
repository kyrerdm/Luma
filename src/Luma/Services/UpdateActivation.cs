using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace Luma.Services;

internal interface IUpdateRuntime
{
    Task<bool> StartAndWaitAsync(string executable);
    void RestoreRegistration(InstallPlan plan);
}

internal sealed record ActivationResult(bool Updated, bool Restored);

internal static class UpdateActivation
{
    internal static InstallPlan? PreviousInstallation(string? executable, string root)
    {
        if (string.IsNullOrWhiteSpace(executable)) return null;
        try
        {
            var directory = Path.GetDirectoryName(Path.GetFullPath(executable));
            if (directory is null || !Version.TryParse(Path.GetFileName(directory), out var version) || version.Build < 0) return null;
            var plan = InstallService.Plan(root, version);
            if (!Path.GetFullPath(executable).Equals(plan.Executable, StringComparison.OrdinalIgnoreCase) || !File.Exists(executable)) return null;
            if ((File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0 || (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) return null;
            var info = FileVersionInfo.GetVersionInfo(executable);
            return info.ProductName == "Luma" && info.FileMajorPart == version.Major && info.FileMinorPart == version.Minor && info.FileBuildPart == version.Build ? plan : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.ComponentModel.Win32Exception or System.Security.SecurityException) { return null; }
    }

    internal static async Task<ActivationResult> ActivateAsync(InstallPlan next, InstallPlan? previous, IUpdateRuntime runtime)
    {
        if (await runtime.StartAndWaitAsync(next.Executable)) return new(true, false);
        if (previous is null || previous.Executable.Equals(next.Executable, StringComparison.OrdinalIgnoreCase)) return new(false, false);
        runtime.RestoreRegistration(previous);
        return new(false, await runtime.StartAndWaitAsync(previous.Executable));
    }
}

internal sealed class NativeUpdateRuntime : IUpdateRuntime
{
    public void RestoreRegistration(InstallPlan plan)
    {
        InstallService.Register(plan, false);
        if (StartupService.IsEnabled()) StartupService.SetExecutable(plan.Executable);
    }
    public async Task<bool> StartAndWaitAsync(string executable)
    {
        var eventName = "Local\\Luma.Update.Ready." + Guid.NewGuid().ToString("N");
        using var ready = new EventWaitHandle(false, EventResetMode.ManualReset, eventName);
        EventWaitHandle? oldExit = null;
        try { oldExit = EventWaitHandle.OpenExisting("Local\\Luma.Flyouts.Exit"); } catch (WaitHandleCannotBeOpenedException) { }
        using (oldExit)
        {
            Process? launched = null;
            try
            {
                var info = FileVersionInfo.GetVersionInfo(executable);
                var supportsReady = info.FileMajorPart > 0 || info.FileMinorPart >= 7;
                var start = new ProcessStartInfo(executable) { UseShellExecute = false, WorkingDirectory = Path.GetDirectoryName(executable), WindowStyle = ProcessWindowStyle.Hidden };
                start.ArgumentList.Add("--wait-for-instance");
                if (supportsReady) { start.ArgumentList.Add("--background"); start.ArgumentList.Add("--ready-event"); start.ArgumentList.Add(eventName); }
                launched = Process.Start(start);
                if (launched is null) return false;
                oldExit?.Set();
                var clock = Stopwatch.StartNew();
                while (clock.Elapsed < TimeSpan.FromSeconds(30))
                {
                    if (ready.WaitOne(0) || (!supportsReady && launched.MainWindowHandle != IntPtr.Zero))
                    {
                        try { using var open = EventWaitHandle.OpenExisting("Local\\Luma.Flyouts.OpenSettings"); open.Set(); } catch (WaitHandleCannotBeOpenedException) { }
                        return true;
                    }
                    if (launched.HasExited) return false;
                    await Task.Delay(100);
                }
                if (!launched.HasExited) { launched.Kill(); await launched.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(5)); }
                return false;
            }
            catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or TimeoutException) { return false; }
            finally { launched?.Dispose(); }
        }
    }
}
