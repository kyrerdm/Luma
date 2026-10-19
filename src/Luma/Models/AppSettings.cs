using System;
using Luma.Themes;

namespace Luma.Models;

public sealed class AppSettings
{
    public bool VolumeKeys { get; set; } = true;
    public bool LockKeys { get; set; } = true;
    public bool ObserveVolume { get; set; } = true;
    public bool ObserveBrightness { get; set; } = true;
    public bool HideNativeBrightness { get; set; } = true;
    public bool Animations { get; set; } = true;
    public bool ShowMedia { get; set; } = true;
    public bool ArtworkBackground { get; set; } = true;
    public string Accent { get; set; } = "Ice";
    public string Position { get; set; } = "BottomCenter";
    public double DurationSeconds { get; set; } = 2.5;
    public int VolumeStep { get; set; } = 2;
    public double FlyoutScale { get; set; } = 1;
    public string TargetDisplay { get; set; } = "Cursor";
    public string Theme { get; set; } = "Dark";
    public string CustomAccent { get; set; } = "#81B7DB";
    public double SurfaceOpacity { get; set; } = 0.94;
    public bool HideInFullscreen { get; set; }
    public bool ExternalBrightness { get; set; }
    public string Language { get; set; } = "tr";
    public bool WelcomeCompleted { get; set; }

    public void Apply(AppSettings source)
    {
        source.Normalize();
        // Keep the shared instance: the controller and open flyouts reference it.
        foreach (var property in typeof(AppSettings).GetProperties())
            property.SetValue(this, property.GetValue(source));
    }

    public void Normalize()
    {
        if (Language is not ("tr" or "en")) Language = "tr";
        if (Position is not ("BottomCenter" or "TopCenter" or "TopRight" or "BottomRight" or "TopLeft" or "BottomLeft"))
            Position = "BottomCenter";
        if (!double.IsFinite(DurationSeconds)) DurationSeconds = 2.5;
        DurationSeconds = Math.Clamp(DurationSeconds, 1, 8);
        VolumeStep = Math.Clamp(VolumeStep, 1, 10);
        if (!Palette.IsKnown(Accent) && Accent != "Custom") Accent = "Ice";
        if (Theme is not ("Dark" or "Light" or "System")) Theme = "Dark";
        if (!Palette.ValidCustomColor(CustomAccent)) CustomAccent = "#81B7DB";
        CustomAccent = CustomAccent.ToUpperInvariant();
        if (!double.IsFinite(SurfaceOpacity)) SurfaceOpacity = 0.94;
        SurfaceOpacity = Math.Clamp(SurfaceOpacity, 0.65, 1);
        if (!double.IsFinite(FlyoutScale)) FlyoutScale = 1;
        FlyoutScale = Math.Clamp(FlyoutScale, 0.75, 1.4);
        if (string.IsNullOrWhiteSpace(TargetDisplay) || TargetDisplay.Length > 1024) TargetDisplay = "Cursor";
    }
}
