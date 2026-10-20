using System;
using System.Threading.Tasks;
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
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        if (enabled)
        {
            var path = Environment.ProcessPath ?? throw new InvalidOperationException("Uygulama yolu bulunamadı.");
            if (System.IO.Path.GetFileNameWithoutExtension(path).Equals("dotnet", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Başlangıç ayarı için Luma.exe dosyasını açın.");
            key.SetValue(ValueName, $"\"{path}\" --background");
        }
        else key.DeleteValue(ValueName, false);
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
