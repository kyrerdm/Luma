using System;
using System.Windows;
using System.Windows.Media;
using Microsoft.Win32;
using Luma.Models;

namespace Luma.Themes;

internal static class Appearance
{
    public static bool IsLight { get; private set; }
    public static bool ResolveLight(string theme, bool systemLight) => theme == "Light" || (theme == "System" && systemLight);
    public static bool SystemIsLight()
    {
        try { using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize"); return key?.GetValue("AppsUseLightTheme") is int value && value != 0; }
        catch (Exception ex) when (ex is UnauthorizedAccessException or System.Security.SecurityException or System.IO.IOException) { return false; }
    }
    public static void Apply(AppSettings settings)
    {
        IsLight = ResolveLight(settings.Theme, SystemIsLight());
        Set("BackgroundBrush", IsLight ? "#F5F5F8" : "#151517");
        Set("SidebarBrush", IsLight ? "#EDEDF2" : "#121214");
        Set("SurfaceBrush", IsLight ? "#FFFFFF" : "#252528");
        Set("BorderBrush", IsLight ? "#D6D6DF" : "#3D3D43");
        Set("TextBrush", IsLight ? "#202029" : "#F2F2F3");
        Set("MutedBrush", IsLight ? "#636370" : "#9C9CA4");
        Set("SelectionBrush", IsLight ? "#E4E4EE" : "#34343B");
        Set("HoverBrush", IsLight ? "#DADAE6" : "#3E3E46");
        Set("TrackBrush", IsLight ? "#B9B9C6" : "#666669");
        var surface = (Color)ColorConverter.ConvertFromString(IsLight ? "#F8F8FC" : "#232325");
        surface.A = (byte)Math.Round(settings.SurfaceOpacity * 255);
        Set("FlyoutBrush", surface);
        var overlay = (Color)ColorConverter.ConvertFromString(IsLight ? "#F5F5F8" : "#222224"); overlay.A = 150;
        Set("ArtworkOverlayBrush", overlay);
        if (SystemParameters.HighContrast)
        {
            Set("BackgroundBrush", SystemColors.WindowColor); Set("SidebarBrush", SystemColors.WindowColor); Set("SurfaceBrush", SystemColors.WindowColor);
            Set("FlyoutBrush", SystemColors.WindowColor); Set("ArtworkOverlayBrush", SystemColors.WindowColor); Set("TextBrush", SystemColors.WindowTextColor);
            Set("MutedBrush", SystemColors.WindowTextColor); Set("BorderBrush", SystemColors.WindowTextColor); Set("AccentBrush", SystemColors.HighlightColor);
            Set("SelectionBrush", SystemColors.ControlColor); Set("HoverBrush", SystemColors.ControlColor); Set("TrackBrush", SystemColors.GrayTextColor);
        }
    }
    private static void Set(string key, string value) => Set(key, (Color)ColorConverter.ConvertFromString(value));
    private static void Set(string key, Color value) { var brush = new SolidColorBrush(value); brush.Freeze(); Application.Current.Resources[key] = brush; }
}
