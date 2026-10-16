using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
namespace Luma.Services;

internal sealed record UpdateResult(bool Available, string Message, Uri? Page);
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
        if (!Version.TryParse(tag.TrimStart('v', 'V'), out var version) || release.GetProperty("draft").GetBoolean() || release.GetProperty("prerelease").GetBoolean()) return new(false, "Kararlı sürüm bilgisi doğrulanamadı.", null);
        if (!Uri.TryCreate(release.GetProperty("html_url").GetString(), UriKind.Absolute, out var page) || page.Scheme != "https" || page.Host != "github.com" || !page.IsDefaultPort || page.UserInfo.Length != 0 || !page.AbsolutePath.StartsWith("/kyrerdm/Luma/releases/tag/", StringComparison.Ordinal)) return new(false, "Güncelleme bağlantısı doğrulanamadı.", null);
        var normalized = new Version(current.Major, current.Minor, Math.Max(0, current.Build));
        return version > normalized ? new(true, $"Luma {version.ToString(3)} hazır. İndirme sayfasından kurulum paketini açabilirsin; tercihler korunur.", page) : new(false, "En güncel kararlı sürümü kullanıyorsun.", null);
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
