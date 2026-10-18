using System;
using System.IO;
using System.Text;
using System.Text.Json;
using Luma.Models;

namespace Luma.Services;

internal static class PreferenceTransfer
{
    internal const int MaxBytes = 64 * 1024;
    private sealed record Backup(string Application, int FormatVersion, AppSettings Settings);

    public static string Export(AppSettings settings)
    {
        var copy = JsonSerializer.Deserialize<AppSettings>(JsonSerializer.Serialize(settings))!;
        copy.Normalize();
        copy.WelcomeCompleted = true;
        return JsonSerializer.Serialize(new Backup("Luma", 1, copy), new JsonSerializerOptions { WriteIndented = true });
    }

    public static AppSettings Parse(string json)
    {
        if (Encoding.UTF8.GetByteCount(json) > MaxBytes) throw new InvalidDataException("Ayar dosyası çok büyük.");
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = 16 });
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("Application", out var app) || app.ValueKind != JsonValueKind.String || app.GetString() != "Luma"
            || !root.TryGetProperty("FormatVersion", out var format) || format.ValueKind != JsonValueKind.Number || !format.TryGetInt32(out var version) || version != 1
            || !root.TryGetProperty("Settings", out var preferences) || preferences.ValueKind != JsonValueKind.Object || !preferences.TryGetProperty(nameof(AppSettings.VolumeKeys), out _))
            throw new InvalidDataException("Bu dosya desteklenen bir Luma ayar yedeği değil.");
        var settings = preferences.Deserialize<AppSettings>() ?? throw new InvalidDataException("Ayarlar okunamadı.");
        settings.Normalize();
        settings.WelcomeCompleted = true;
        return settings;
    }

    public static AppSettings Read(string path)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        if (file.Length > MaxBytes) throw new InvalidDataException("Ayar dosyası çok büyük.");
        using var reader = new StreamReader(file, Encoding.UTF8, true);
        return Parse(reader.ReadToEnd());
    }
}
