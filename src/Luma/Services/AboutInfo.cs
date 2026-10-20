using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Luma.Services;

internal sealed record LicenseNotice(string Title, string ResourceName);

internal static class AboutInfo
{
    internal const string Author = "kyrerdm";
    internal const string ProjectUrl = "https://github.com/kyrerdm/Luma";
    internal const string IssuesUrl = ProjectUrl + "/issues";
    // GitHub and Store can ship different versions. The history remains valid
    // even when the running package has no matching GitHub release tag.
    internal static string ReleaseUrl => ProjectUrl + "/releases";
    internal static string VersionLabel => "Luma " + AppVersion.Current.ToString(3);
    internal static string CopyText => VersionLabel + Environment.NewLine + ProjectUrl;

    internal static IReadOnlyList<LicenseNotice> Licenses { get; } = Array.AsReadOnly(new[]
    {
        new LicenseNotice("Luma · MIT", "LICENSE.txt"),
        new LicenseNotice(".NET · MIT", "DOTNET-LICENSE.txt"),
        new LicenseNotice(".NET · Üçüncü taraf bildirimleri", "DOTNET-THIRD-PARTY-NOTICES.txt"),
        new LicenseNotice("Windows Desktop · MIT", "WINDOWS-DESKTOP-LICENSE.txt"),
        new LicenseNotice("C#/WinRT · MIT", "WINRT-LICENSE.txt"),
        new LicenseNotice("Windows SDK · Bildirim", "WINDOWS-SDK-NOTICE.txt")
    });

    internal static string ReadLicense(string resourceName)
    {
        if (!Licenses.Any(notice => notice.ResourceName == resourceName)) throw new ArgumentException("Unknown license resource.", nameof(resourceName));
        return Encoding.UTF8.GetString(InstallService.ReadNotice(resourceName)).TrimStart('\uFEFF');
    }
}
