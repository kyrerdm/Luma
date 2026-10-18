using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Luma.Models;

namespace Luma.Services;

// An explicit, local report. No paths, device identifiers, app names, media,
// usernames, key presses, or network transmission are included.
internal static class SupportReport
{
    public static string Create(AppSettings settings, bool audioAvailable, bool brightnessAvailable, int outputCount, int appCount, int mediaCount)
    {
        using var process = Process.GetCurrentProcess();
        var report = new StringBuilder();
        report.AppendLine("Luma support report / Destek raporu");
        report.AppendLine($"Version: {AppVersion.Current.ToString(3)}");
        report.AppendLine($"Windows build: {Environment.OSVersion.Version}");
        report.AppendLine($"Architecture: {RuntimeInformation.ProcessArchitecture}");
        report.AppendLine($"Working set (MiB): {process.WorkingSet64 / 1048576.0:0.0}");
        report.AppendLine($"Private memory (MiB): {process.PrivateMemorySize64 / 1048576.0:0.0}");
        report.AppendLine($"CPU time (ms, lifetime): {process.TotalProcessorTime.TotalMilliseconds:0}");
        report.AppendLine($"Audio available: {audioAvailable}; cached outputs: {outputCount}; sessions: {appCount}");
        report.AppendLine($"Internal brightness available: {brightnessAvailable}; DDC enabled: {settings.ExternalBrightness}");
        report.AppendLine($"Media sessions: {mediaCount}");
        report.AppendLine($"Theme: {settings.Theme}; language: {settings.Language}; scale: {settings.FlyoutScale:0.##}");
        report.AppendLine($"Fullscreen suppression: {settings.HideInFullscreen}; native brightness suppression: {settings.HideNativeBrightness}");
        foreach (var monitor in MonitorCatalog.Current)
            report.AppendLine($"Display: {monitor.Bounds.Width}x{monitor.Bounds.Height}; primary: {monitor.Primary}");
        report.AppendLine();
        report.AppendLine("What happened / Ne oldu:");
        report.AppendLine();
        report.AppendLine("Steps to reproduce / Tekrarlama adımları:");
        report.AppendLine();
        report.AppendLine("Expected behavior / Beklenen davranış:");
        return report.ToString();
    }
}
