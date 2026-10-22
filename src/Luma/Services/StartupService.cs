using System;
using System.Threading.Tasks;
using System.IO;
using System.Diagnostics;
using Microsoft.Win32;
using Windows.ApplicationModel;

namespace Luma.Services;

public static class StartupService
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "LumaFlyouts";
    private static StartupTaskState packagedState;
    public static bool IsEnabled()
    {
        if (Distribution.IsPackaged) return packagedState is StartupTaskState.Enabled or StartupTaskState.EnabledByPolicy;
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(KeyPath);
            return key?.GetValue(ValueName) is string;
        }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException)
        {
            return false;
        }
    }
    public static void SetEnabled(bool enabled)
    {
        if (Distribution.IsPackaged) throw new InvalidOperationException("Use SetEnabledAsync for a packaged application.");
        if (enabled)
        {
            var path = Environment.ProcessPath ?? throw new InvalidOperationException("Uygulama yolu bulunamadı.");
            if (System.IO.Path.GetFileNameWithoutExtension(path).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Başlangıç ayarı için Luma.exe dosyasını açın.");
            SetExecutable(ResolveExecutable(path, InstallService.DefaultRoot));
        }
        else { using var key = Registry.CurrentUser.CreateSubKey(KeyPath); key.DeleteValue(ValueName, false); }
    }

    public static async Task RefreshAsync()
    {
        if (!Distribution.IsPackaged) return;
        try { packagedState = (await StartupTask.GetAsync(Distribution.StartupTaskId)).State; }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or UnauthorizedAccessException)
        { throw new InvalidOperationException(Strings.T("Windows başlangıç görevine erişilemedi."), ex); }
    }

    public static async Task SetEnabledAsync(bool enabled)
    {
        if (!Distribution.IsPackaged) { SetEnabled(enabled); return; }
        try
        {
            var task = await StartupTask.GetAsync(Distribution.StartupTaskId);
            if (enabled) packagedState = await task.RequestEnableAsync();
            else { task.Disable(); packagedState = task.State; }
            if (IsEnabled() != enabled)
                throw new InvalidOperationException(Strings.T("Başlangıç tercihini Windows Ayarları > Uygulamalar > Başlangıç bölümünden değiştir."));
        }
        catch (Exception ex) when (ex is System.Runtime.InteropServices.COMException or UnauthorizedAccessException)
        { throw new InvalidOperationException(Strings.T("Windows başlangıç görevine erişilemedi."), ex); }
    }

    internal static void SetExecutable(string executable)
    {
        if (Distribution.IsPackaged) throw new InvalidOperationException("Packaged startup is managed by Windows.");
        if (!System.IO.Path.IsPathFullyQualified(executable) || executable.Contains('"') || !System.IO.File.Exists(executable)) throw new InvalidOperationException("Başlangıç yolu doğrulanamadı.");
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        key.SetValue(ValueName, $"\"{executable}\" --background");
    }

    internal static string ResolveExecutable(string currentExecutable, string installRoot)
    {
        var current = Path.GetFullPath(currentExecutable);
        var best = current;
        var applicationVersion = AppVersion.Current;
        var bestVersion = ReadVersion(current) ?? new Version(applicationVersion.Major, applicationVersion.Minor, Math.Max(0, applicationVersion.Build));
        if (Directory.Exists(installRoot) && (File.GetAttributes(installRoot) & FileAttributes.ReparsePoint) == 0)
        {
            foreach (var directory in Directory.EnumerateDirectories(installRoot))
            {
                if (!Version.TryParse(Path.GetFileName(directory), out var version) || version.Build < 0 || version < bestVersion
                    || (File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) continue;
                var candidate = Path.Combine(directory, "Luma.exe");
                if (ReadVersion(candidate) != version) continue;
                best = candidate;
                bestVersion = version;
            }
        }
        var temporary = Path.GetFullPath(Path.GetTempPath()).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar;
        if (best.StartsWith(temporary, StringComparison.OrdinalIgnoreCase) || ($"\"{best}\" --background").Length > 260)
            throw new InvalidOperationException(Strings.T("Windows ile başlatmak için Luma'yı kur veya kısa, kalıcı bir klasöre çıkar."));
        return best;
    }

    private static Version? ReadVersion(string path)
    {
        try
        {
            if (!File.Exists(path) || (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) return null;
            var info = FileVersionInfo.GetVersionInfo(path);
            return info.ProductName == "Luma" ? new(info.FileMajorPart, info.FileMinorPart, info.FileBuildPart) : null;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception or System.Security.SecurityException) { return null; }
    }

    public static void RefreshEnabledPath()
    {
        if (Distribution.IsPackaged) return;
        if (!IsEnabled()) return;
        try { SetEnabled(true); }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException or InvalidOperationException)
        {
            // A startup-path repair must not prevent the app from opening.
        }
    }
}
