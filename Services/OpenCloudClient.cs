using System.Net;
using System.Net.Http.Headers;
using System.Text.Json.Serialization;

namespace IMApi.Services;

public class OpenCloudException(HttpStatusCode status, string message) : Exception(message)
{
    public HttpStatusCode Status { get; } = status;
}

public record OpenCloudUser(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("displayName")] string? DisplayName,
    [property: JsonPropertyName("onPremisesSamAccountName")] string? UserName);

/// <summary>
/// Talks to OpenCloud on behalf of the calling user: the Authorization header of the
/// incoming request (OIDC bearer token, forwarded unchanged by the OpenCloud proxy) is
/// passed on, so OpenCloud checks every access with the user's own permissions.
/// </summary>
public class OpenCloudClient(HttpClient http)
{
    public async Task<OpenCloudUser> GetMeAsync(string authorization, CancellationToken ct)
    {
        using var request = Request(HttpMethod.Get, "graph/v1.0/me", authorization);
        using var response = await http.SendAsync(request, ct);
        await EnsureSuccess(response, "Benutzer");
        return await response.Content.ReadFromJsonAsync<OpenCloudUser>(ct)
            ?? throw new OpenCloudException(HttpStatusCode.BadGateway, "Leere Antwort von OpenCloud");
    }

    public async Task<byte[]> DownloadAsync(string spaceId, string path, long maxBytes, string authorization, CancellationToken ct)
    {
        using var request = Request(HttpMethod.Get, DavUrl(spaceId, path), authorization);
        using var response = await http.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        await EnsureSuccess(response, path);
        if (response.Content.Headers.ContentLength > maxBytes)
            throw new OpenCloudException(HttpStatusCode.RequestEntityTooLarge,
                $"{path} ist größer als {maxBytes / 1024 / 1024} MB");

        await using var stream = await response.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[81920];
        int read;
        while ((read = await stream.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + read > maxBytes)
                throw new OpenCloudException(HttpStatusCode.RequestEntityTooLarge,
                    $"{path} ist größer als {maxBytes / 1024 / 1024} MB");
            buffer.Write(chunk, 0, read);
        }
        return buffer.ToArray();
    }

    /// <summary>Creates the folder and its missing parents (MKCOL on each level).</summary>
    public async Task EnsureFolderAsync(string spaceId, string folder, string authorization, CancellationToken ct)
    {
        var current = "";
        foreach (var segment in folder.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            current += "/" + segment;
            using var request = Request(new HttpMethod("MKCOL"), DavUrl(spaceId, current), authorization);
            using var response = await http.SendAsync(request, ct);
            // 405: the folder exists already
            if (response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.MethodNotAllowed) continue;
            await EnsureSuccess(response, current);
        }
    }

    public async Task UploadAsync(string spaceId, string path, byte[] content, string contentType, string authorization, CancellationToken ct)
    {
        using var request = Request(HttpMethod.Put, DavUrl(spaceId, path), authorization);
        request.Content = new ByteArrayContent(content);
        request.Content.Headers.ContentType = new MediaTypeHeaderValue(contentType);
        using var response = await http.SendAsync(request, ct);
        await EnsureSuccess(response, path);
    }

    /// <summary>WebDAV URL of a resource: dav/spaces/{spaceId}/{path}, every segment escaped.</summary>
    public static string DavUrl(string spaceId, string path)
    {
        if (string.IsNullOrWhiteSpace(spaceId) || spaceId.Contains('/'))
            throw new ArgumentException("Ungültige Space-ID", nameof(spaceId));
        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Any(s => s is "." or ".."))
            throw new ArgumentException("Ungültiger Pfad", nameof(path));
        return "dav/spaces/" + Uri.EscapeDataString(spaceId) +
               string.Concat(segments.Select(s => "/" + Uri.EscapeDataString(s)));
    }

    private static HttpRequestMessage Request(HttpMethod method, string url, string authorization)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.TryAddWithoutValidation("Authorization", authorization);
        return request;
    }

    private static async Task EnsureSuccess(HttpResponseMessage response, string what)
    {
        if (response.IsSuccessStatusCode) return;
        var message = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => "Nicht angemeldet oder Sitzung abgelaufen",
            HttpStatusCode.Forbidden => $"Keine Berechtigung für {what}",
            HttpStatusCode.NotFound => $"{what} nicht gefunden",
            HttpStatusCode.InsufficientStorage => "Kein Speicherplatz mehr frei",
            _ => $"OpenCloud antwortet mit {(int)response.StatusCode} für {what}",
        };
        // Drain the body so the connection can be reused.
        await response.Content.ReadAsByteArrayAsync();
        throw new OpenCloudException(response.StatusCode, message);
    }
}
