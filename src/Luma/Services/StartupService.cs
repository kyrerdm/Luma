using System;
using Microsoft.Win32;

namespace Luma.Services;

public static class StartupService
{
    private const string KeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "LumaFlyouts";
    public static bool IsEnabled()
    {
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

    internal static void SetExecutable(string executable)
    {
        if (!System.IO.Path.IsPathFullyQualified(executable) || executable.Contains('"') || !System.IO.File.Exists(executable)) throw new InvalidOperationException("Başlangıç yolu doğrulanamadı.");
        using var key = Registry.CurrentUser.CreateSubKey(KeyPath);
        key.SetValue(ValueName, $"\"{executable}\" --background");
    }

    public static void RefreshEnabledPath()
    {
        if (!IsEnabled()) return;
        try { SetEnabled(true); }
        catch (Exception ex) when (ex is System.Security.SecurityException or UnauthorizedAccessException or System.IO.IOException or InvalidOperationException)
        {
            // A startup-path repair must not prevent the app from opening.
        }
    }
}
