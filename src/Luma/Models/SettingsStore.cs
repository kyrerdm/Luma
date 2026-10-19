using System;
using System.IO;
using System.Text.Json;

namespace Luma.Models;

public enum SettingsRecovery { None, Backup, Defaults }

public sealed class SettingsStore
{
    internal const int MaxBytes = 64 * 1024;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, MaxDepth = 16 };
    private readonly object sync = new();
    public string DirectoryPath { get; }
    public string FilePath => Path.Combine(DirectoryPath, "settings.json");
    public string BackupPath => FilePath + ".bak";
    public SettingsRecovery Recovery { get; private set; }
    public SettingsStore(string directoryPath) => DirectoryPath = directoryPath;

    public AppSettings Load()
    {
        lock (sync)
        {
            var primary = Read(FilePath, out var current);
            if (primary == ReadResult.Valid) { Recovery = SettingsRecovery.None; return current!; }
            var backup = Read(BackupPath, out var previous);
            if (backup == ReadResult.Valid) { Recovery = SettingsRecovery.Backup; return previous!; }
            var damaged = primary == ReadResult.Invalid || backup == ReadResult.Invalid;
            Recovery = damaged ? SettingsRecovery.Defaults : SettingsRecovery.None;
            // A damaged existing installation is not a first-time installation.
            return new AppSettings { WelcomeCompleted = damaged };
        }
    }

    public void Save(AppSettings settings)
    {
        lock (sync)
        {
            settings.Normalize();
            var bytes = JsonSerializer.SerializeToUtf8Bytes(settings, JsonOptions);
            if (bytes.Length > MaxBytes) throw new IOException("Ayar dosyası çok büyük.");
            Directory.CreateDirectory(DirectoryPath);
            var staged = Path.Combine(DirectoryPath, ".settings-" + Guid.NewGuid().ToString("N") + ".tmp");
            var stagedBackup = staged + ".bak";
            try
            {
                using (var file = new FileStream(staged, FileMode.CreateNew, FileAccess.Write, FileShare.None)) file.Write(bytes);
                if (Read(FilePath, out _) == ReadResult.Valid)
                {
                    // Replace and rotate the last known good primary as one operation.
                    File.Replace(staged, FilePath, BackupPath);
                }
                else
                {
                    // Never rotate a corrupt primary over a healthy recovery copy.
                    if (Read(BackupPath, out _) != ReadResult.Valid)
                    {
                        File.Copy(staged, stagedBackup);
                        File.Move(stagedBackup, BackupPath, true);
                    }
                    File.Move(staged, FilePath, true);
                }
                Recovery = SettingsRecovery.None;
            }
            finally
            {
                if (File.Exists(staged)) File.Delete(staged);
                if (File.Exists(stagedBackup)) File.Delete(stagedBackup);
            }
        }
    }

    private enum ReadResult { Missing, Invalid, Valid }
    private static ReadResult Read(string path, out AppSettings? settings)
    {
        settings = null;
        try
        {
            using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read | FileShare.Delete);
            if (file.Length > MaxBytes) return ReadResult.Invalid;
            var bytes = new byte[(int)file.Length];
            file.ReadExactly(bytes);
            // Accept the BOM used by some Windows editors and older saves.
            var start = bytes.AsSpan().StartsWith(new byte[] { 0xEF, 0xBB, 0xBF }) ? 3 : 0;
            using var document = JsonDocument.Parse(bytes.AsMemory(start), new JsonDocumentOptions { MaxDepth = 16 });
            if (document.RootElement.ValueKind != JsonValueKind.Object) return ReadResult.Invalid;
            settings = document.RootElement.Deserialize<AppSettings>(JsonOptions);
            if (settings is null) return ReadResult.Invalid;
            if (!document.RootElement.TryGetProperty(nameof(AppSettings.WelcomeCompleted), out _)) settings.WelcomeCompleted = true;
            settings.Normalize();
            return ReadResult.Valid;
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException) { return ReadResult.Missing; }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or System.Security.SecurityException) { return ReadResult.Invalid; }
    }
}
