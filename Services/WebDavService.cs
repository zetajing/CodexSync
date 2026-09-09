using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;

namespace CodexSync.Services;

public sealed class WebDavService : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly Uri _baseUri;

    public WebDavService(string baseUrl, string username, string password)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new ArgumentException("WebDAV 地址不能为空。", nameof(baseUrl));
        }

        if (!baseUrl.EndsWith('/'))
        {
            baseUrl += "/";
        }

        _baseUri = new Uri(baseUrl, UriKind.Absolute);
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromMinutes(10)
        };

        if (!string.IsNullOrWhiteSpace(username))
        {
            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
            _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
        }
    }

    public async Task TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        using var request = new HttpRequestMessage(new HttpMethod("PROPFIND"), _baseUri);
        request.Headers.TryAddWithoutValidation("Depth", "0");
        request.Content = new StringContent("<?xml version=\"1.0\"?><propfind xmlns=\"DAV:\"><prop><displayname/></prop></propfind>", Encoding.UTF8, "application/xml");

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (response.StatusCode != HttpStatusCode.MultiStatus && !response.IsSuccessStatusCode)
        {
            throw new HttpRequestException($"WebDAV 连接失败：{(int)response.StatusCode} {response.ReasonPhrase}");
        }
    }

    public async Task EnsureCollectionAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        var parts = Normalize(relativePath).Split('/', StringSplitOptions.RemoveEmptyEntries);
        var current = string.Empty;

        foreach (var part in parts)
        {
            current = string.IsNullOrEmpty(current) ? part : $"{current}/{part}";
            using var request = new HttpRequestMessage(new HttpMethod("MKCOL"), BuildUri(current));
            using var response = await _httpClient.SendAsync(request, cancellationToken);

            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.MethodNotAllowed)
            {
                continue;
            }

            throw new HttpRequestException($"创建 WebDAV 目录失败：{current}，{(int)response.StatusCode} {response.ReasonPhrase}");
        }
    }

    public async Task UploadFileAsync(string relativePath, string localFile, CancellationToken cancellationToken = default)
    {
        var parent = Path.GetDirectoryName(Normalize(relativePath))?.Replace('\\', '/');
        if (!string.IsNullOrWhiteSpace(parent))
        {
            await EnsureCollectionAsync(parent, cancellationToken);
        }

        await using var stream = File.OpenRead(localFile);
        using var content = new StreamContent(stream);
        using var response = await _httpClient.PutAsync(BuildUri(relativePath), content, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task UploadTextAsync(string relativePath, string content, CancellationToken cancellationToken = default)
    {
        var parent = Path.GetDirectoryName(Normalize(relativePath))?.Replace('\\', '/');
        if (!string.IsNullOrWhiteSpace(parent))
        {
            await EnsureCollectionAsync(parent, cancellationToken);
        }

        using var body = new StringContent(content, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PutAsync(BuildUri(relativePath), body, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    public async Task<string> DownloadTextAsync(string relativePath, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(BuildUri(relativePath), cancellationToken);
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    public async Task DownloadFileAsync(string relativePath, string localFile, CancellationToken cancellationToken = default)
    {
        using var response = await _httpClient.GetAsync(BuildUri(relativePath), HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        Directory.CreateDirectory(Path.GetDirectoryName(localFile)!);
        await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using var output = File.Create(localFile);
        await input.CopyToAsync(output, cancellationToken);
    }

    private Uri BuildUri(string relativePath)
    {
        var normalized = Normalize(relativePath);
        var escaped = string.Join('/', normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Select(Uri.EscapeDataString));
        return new Uri(_baseUri, escaped);
    }

    private static string Normalize(string path) => path.Replace('\\', '/').Trim('/');

    public void Dispose() => _httpClient.Dispose();
}
