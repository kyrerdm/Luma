using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
namespace Luma.Services;

internal sealed record UpdatePackage(Version Version, Uri Download, long Size, string Sha256);
internal sealed record UpdateResult(bool Available, string Message, Uri? Page, UpdatePackage? Package = null);
internal sealed class UpdateService : IDisposable
{
    private readonly HttpClient client;
    public UpdateService(HttpMessageHandler? handler = null)
    {
        client = new HttpClient(handler ?? new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(12), MaxResponseContentBufferSize = 2 * 1024 * 1024 };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Luma/" + AppVersion.Current.ToString(3));
        client.DefaultRequestHeaders.Accept.ParseAdd("application/vnd.github+json");
    }
    internal static UpdateResult Parse(string json, Version current)
    {
        using var document = JsonDocument.Parse(json);
        var release = document.RootElement;
        var tag = release.GetProperty("tag_name").GetString() ?? "";
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var version) || version.Build < 0 || version.Revision > 0 || release.GetProperty("draft").GetBoolean() || release.GetProperty("prerelease").GetBoolean()) return new(false, "Kararlı sürüm bilgisi doğrulanamadı.", null);
        if (!Uri.TryCreate(release.GetProperty("html_url").GetString(), UriKind.Absolute, out var page) || !TrustedUri(page, "github.com") || page.Query.Length != 0 || page.Fragment.Length != 0 || page.AbsolutePath != "/kyrerdm/Luma/releases/tag/" + tag) return new(false, "Güncelleme bağlantısı doğrulanamadı.", null);
        var normalized = new Version(current.Major, current.Minor, Math.Max(0, current.Build));
        if (version <= normalized) return new(false, "En güncel kararlı sürümü kullanıyorsun.", null);
        UpdatePackage? package = null;
        var filename = $"Luma-{version.ToString(3)}-Setup.exe";
        if (release.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assets.EnumerateArray())
            {
                if (!asset.TryGetProperty("name", out var name) || name.GetString() != filename) continue;
                if (!asset.TryGetProperty("size", out var size) || !size.TryGetInt64(out var bytes) || bytes < 20 * 1024 * 1024 || bytes > MaxDownloadBytes
                    || !asset.TryGetProperty("digest", out var digest) || digest.ValueKind != JsonValueKind.String
                    || digest.GetString() is not string hash || !hash.StartsWith("sha256:", StringComparison.Ordinal) || hash.Length != 71 || !hash[7..].All(Uri.IsHexDigit)
                    || !asset.TryGetProperty("browser_download_url", out var download) || !Uri.TryCreate(download.GetString(), UriKind.Absolute, out var url)
                    || !TrustedUri(url, "github.com") || url.Query.Length != 0 || url.Fragment.Length != 0 || url.AbsolutePath != $"/kyrerdm/Luma/releases/download/{tag}/{filename}") continue;
                package = new(version, url, bytes, hash[7..]);
                break;
            }
        }
        return new(true, package is null ? "Yeni sürüm hazır. Kurulum için sürüm sayfasını aç." : "Yeni sürüm hazır. İndirip doğrulayarak kurabilirsin; tercihler korunur.", page, package);
    }

    internal const long MaxDownloadBytes = 250L * 1024 * 1024;
    private static bool TrustedUri(Uri uri, string host) => uri.IsAbsoluteUri && uri.Scheme == "https" && uri.Host == host && uri.IsDefaultPort && uri.UserInfo.Length == 0;
    internal static bool TrustedDownloadRedirect(Uri uri) => TrustedUri(uri, "release-assets.githubusercontent.com") || TrustedUri(uri, "objects.githubusercontent.com");

    public async Task<string> DownloadAsync(UpdatePackage package, string directory, IProgress<int>? progress = null, CancellationToken cancellation = default)
    {
        var filename = $"Luma-{package.Version.ToString(3)}-Setup.exe";
        if (!TrustedUri(package.Download, "github.com") || package.Download.Query.Length != 0 || package.Download.Fragment.Length != 0
            || package.Download.AbsolutePath != $"/kyrerdm/Luma/releases/download/v{package.Version.ToString(3)}/{filename}"
            || package.Size < 1 || package.Size > MaxDownloadBytes || package.Sha256.Length != 64 || !package.Sha256.All(Uri.IsHexDigit))
            throw new InvalidDataException("Güncelleme paketi doğrulanamadı.");
        Directory.CreateDirectory(directory);
        if ((File.GetAttributes(directory) & FileAttributes.ReparsePoint) != 0) throw new IOException("Güncelleme klasörü bir bağlantı olamaz.");
        var staged = Path.Combine(directory, ".download-" + Guid.NewGuid().ToString("N") + ".part");
        var target = Path.Combine(directory, filename);
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellation);
        timeout.CancelAfter(TimeSpan.FromMinutes(5));
        var token = timeout.Token;
        try
        {
            var uri = package.Download;
            for (var redirect = 0; redirect <= 5; redirect++)
            {
                using var response = await client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, token);
                if ((int)response.StatusCode is >= 300 and < 400)
                {
                    var location = response.Headers.Location;
                    if (redirect == 5 || location is null) throw new HttpRequestException("İndirme yönlendirmesi doğrulanamadı.");
                    uri = location.IsAbsoluteUri ? location : new Uri(uri, location);
                    if (!TrustedDownloadRedirect(uri)) throw new HttpRequestException("İndirme sunucusu doğrulanamadı.");
                    continue;
                }
                response.EnsureSuccessStatusCode();
                if (response.Content.Headers.ContentLength is long length && length != package.Size) throw new InvalidDataException("Paket boyutu eşleşmiyor.");
                await using var source = await response.Content.ReadAsStreamAsync(token);
                using var hash = IncrementalHash.CreateHash(HashAlgorithmName.SHA256);
                long received = 0;
                await using (var destination = new FileStream(staged, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, true))
                {
                    var buffer = new byte[81920];
                    int count;
                    while ((count = await source.ReadAsync(buffer, token)) > 0)
                    {
                        received += count;
                        if (received > package.Size) throw new InvalidDataException("Paket beklenen boyutu aşıyor.");
                        hash.AppendData(buffer, 0, count);
                        await destination.WriteAsync(buffer.AsMemory(0, count), token);
                        progress?.Report((int)(received * 100 / package.Size));
                    }
                }
                token.ThrowIfCancellationRequested();
                if (received != package.Size || !Convert.ToHexString(hash.GetHashAndReset()).Equals(package.Sha256, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Paketin SHA-256 doğrulaması başarısız.");
                File.Move(staged, target, true);
                progress?.Report(100);
                return target;
            }
            throw new HttpRequestException("İndirme tamamlanamadı.");
        }
        finally { if (File.Exists(staged)) File.Delete(staged); }
    }
    public async Task<UpdateResult> CheckAsync(CancellationToken cancellation = default)
    {
        try
        {
            using var response = await client.GetAsync("https://api.github.com/repos/kyrerdm/Luma/releases/latest", cancellation);
            if (!response.IsSuccessStatusCode) return new(false, "Sürüm bilgisi alınamadı. Daha sonra tekrar dene.", null);
            return Parse(await response.Content.ReadAsStringAsync(cancellation), AppVersion.Current);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException or KeyNotFoundException or ArgumentException) { return new(false, "Güncelleme denetlenemedi. İnternet bağlantısını kontrol et.", null); }
    }
    public void Dispose() => client.Dispose();
}
