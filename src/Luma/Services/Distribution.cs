using System;
using System.IO;
using System.Runtime.InteropServices;
using Windows.Storage;
using Luma.Models;

namespace Luma.Services;

internal static class Distribution
{
    // Package identity is supplied by Windows, never by a command-line switch.
    public static bool IsPackaged { get; } = HasPackageIdentity();
    public const string StartupTaskId = "LumaStartup";
    public const string StoreUpdatesMessage = "Bu sürümün güncellemeleri Microsoft Store tarafından yönetilir.";
    public static string LegacyDataDirectory => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "LumaFlyouts");
    public static string DataDirectory => IsPackaged
        ? Path.Combine(ApplicationData.Current.LocalFolder.Path, "Luma") : LegacyDataDirectory;

    private static bool HasPackageIdentity()
    {
        uint length = 0;
        return GetCurrentPackageFullName(ref length, IntPtr.Zero) == 122; // ERROR_INSUFFICIENT_BUFFER
    }

    // Store preferences are independent after the first copy. Never overwrite an
    // existing primary/backup, or modify the unpackaged installation's files.
    internal static bool TryMigrateSettings(string destination, string source)
    {
        try
        {
            var target = new SettingsStore(destination);
            var legacy = new SettingsStore(source);
            if (File.Exists(target.FilePath) || File.Exists(target.BackupPath)
                || (!File.Exists(legacy.FilePath) && !File.Exists(legacy.BackupPath))) return false;
            var settings = legacy.Load();
            if (legacy.Recovery == SettingsRecovery.Defaults) return false;
            target.Save(settings);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { return false; }
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetCurrentPackageFullName(ref uint length, IntPtr fullName);
}
