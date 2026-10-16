using System;
using System.IO;
using System.Text.Json;

namespace Luma.Models;

public sealed class AppSettings
{
    public bool VolumeKeys { get; set; } = true;
    public bool LockKeys { get; set; } = true;
    public bool ObserveVolume { get; set; } = true;
    public bool Animations { get; set; } = true;
    public bool ShowMedia { get; set; } = true;
    public bool ArtworkBackground { get; set; } = true;
    public string Accent { get; set; } = "Ice";
    public string Position { get; set; } = "BottomCenter";
    public double DurationSeconds { get; set; } = 2.5;
    public int VolumeStep { get; set; } = 2;
    public double FlyoutScale { get; set; } = 1;
    public string TargetDisplay { get; set; } = "Cursor";

    public void Normalize()
    {
        if (Position is not ("BottomCenter" or "TopCenter" or "TopRight" or "BottomRight" or "TopLeft" or "BottomLeft"))
            Position = "BottomCenter";
        if (!double.IsFinite(DurationSeconds)) DurationSeconds = 2.5;
        DurationSeconds = Math.Clamp(DurationSeconds, 1, 8);
        VolumeStep = Math.Clamp(VolumeStep, 1, 10);
        if (Accent is not ("Ice" or "Lilac" or "Mint")) Accent = "Ice";
        if (!double.IsFinite(FlyoutScale)) FlyoutScale = 1;
        FlyoutScale = Math.Clamp(FlyoutScale, 0.75, 1.4);
        if (string.IsNullOrWhiteSpace(TargetDisplay) || TargetDisplay.Length > 1024) TargetDisplay = "Cursor";
    }
}

public sealed class SettingsStore
{
    public string DirectoryPath { get; }
    public string FilePath => Path.Combine(DirectoryPath, "settings.json");
    public SettingsStore(string directoryPath) => DirectoryPath = directoryPath;

    public AppSettings Load()
    {
        try
        {
            var settings = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath)) ?? new();
            settings.Normalize();
            return settings;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return new();
        }
    }

    public void Save(AppSettings settings)
    {
        settings.Normalize();
        Directory.CreateDirectory(DirectoryPath);
        var temporary = FilePath + ".tmp";
        File.WriteAllText(temporary, JsonSerializer.Serialize(settings, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temporary, FilePath, true);
    }
}
