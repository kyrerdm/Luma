using System;
using System.ComponentModel;
using System.IO;

namespace Luma.Services;

internal sealed record UpdateNotice(Version Version, string Title, string Body);

// Persist only the last announced version, separately from shareable preferences.
internal sealed class UpdateNotification
{
    private readonly string path;
    private readonly Action<UpdateNotice> show;
    private Version? announced;

    internal UpdateNotification(string directory, Action<UpdateNotice> show)
    {
        path = Path.Combine(directory, "update-notification.txt");
        this.show = show;
        try
        {
            if (File.Exists(path) && new FileInfo(path).Length <= 64 && Version.TryParse(File.ReadAllText(path), out var saved)
                && saved.Build >= 0 && saved.Revision <= 0) announced = new(saved.Major, saved.Minor, saved.Build);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
    }

    internal bool Notify(UpdateResult result)
    {
        var version = result.ReleaseVersion ?? result.Package?.Version;
        var current = AppVersion.Current;
        if (!result.Verified || !result.Available || version is null || version.Build < 0 || version.Revision > 0) return false;
        version = new(version.Major, version.Minor, version.Build);
        if (version <= new Version(current.Major, current.Minor, Math.Max(0, current.Build)) || announced is not null && version <= announced) return false;
        try
        {
            show(new(version, Strings.Format("Luma {0} hazır", version.ToString(3)),
                Strings.T("Yeni sürümü incelemek ve güncellemek için tıkla.")));
        }
        catch (Exception ex) when (ex is Win32Exception or InvalidOperationException) { return false; }
        announced = version;
        string? staged = null;
        try
        {
            var directory = Path.GetDirectoryName(path)!;
            Directory.CreateDirectory(directory);
            staged = Path.Combine(directory, ".update-notification-" + Guid.NewGuid().ToString("N") + ".tmp");
            File.WriteAllText(staged, version.ToString(3));
            File.Move(staged, path, true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        finally
        {
            try { if (staged is not null && File.Exists(staged)) File.Delete(staged); }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException) { }
        }
        return true;
    }
}
