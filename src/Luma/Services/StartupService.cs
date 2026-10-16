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
}
