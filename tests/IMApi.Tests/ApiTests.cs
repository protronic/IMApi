using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using IMApi.Services;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

namespace IMApi.Tests;

/// <summary>Stands in for OpenCloud: Graph /me, WebDAV GET, MKCOL and PUT.</summary>
public class FakeOpenCloud : HttpMessageHandler
{
    public const string Token = "Bearer valid-token";
    public ConcurrentDictionary<string, byte[]> Files { get; } = new();
    public ConcurrentBag<string> Folders { get; } = [];
    public ConcurrentBag<string> Authorizations { get; } = [];

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        var auth = request.Headers.Authorization?.ToString() ?? "";
        Authorizations.Add(auth);
        if (auth != Token) return new HttpResponseMessage(HttpStatusCode.Unauthorized);

        var path = Uri.UnescapeDataString(request.RequestUri!.AbsolutePath);
        if (path == "/graph/v1.0/me")
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = JsonContent.Create(new { id = "u-1", displayName = "Alan Turing", onPremisesSamAccountName = "alan" }),
            };

        const string prefix = "/dav/spaces/space$1";
        if (!path.StartsWith(prefix)) return new HttpResponseMessage(HttpStatusCode.NotFound);
        var item = path[prefix.Length..];
        switch (request.Method.Method)
        {
            case "GET":
                return Files.TryGetValue(item, out var bytes)
                    ? new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) }
                    : new HttpResponseMessage(HttpStatusCode.NotFound);
            case "MKCOL":
                if (Folders.Contains(item)) return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
                Folders.Add(item);
                return new HttpResponseMessage(HttpStatusCode.Created);
            case "PUT":
                Files[item] = await request.Content!.ReadAsByteArrayAsync(ct);
                return new HttpResponseMessage(HttpStatusCode.Created);
            default:
                return new HttpResponseMessage(HttpStatusCode.MethodNotAllowed);
        }
    }
}

public class ApiTests : IDisposable
{
    private readonly string dataDir = Directory.CreateTempSubdirectory("imapi-tests").FullName;
    private readonly FakeOpenCloud openCloud = new();
    private readonly WebApplicationFactory<Program> factory;

    public ApiTests()
    {
        Environment.SetEnvironmentVariable("ImApi__DatabasePath", Path.Combine(dataDir, "imapi.db"));
        Environment.SetEnvironmentVariable("ImApi__LabelFont", "/usr/share/fonts/truetype/liberation/LiberationSans-Regular.ttf");
        openCloud.Files["/Produkte/1017000034_1.png"] =
            File.ReadAllBytes(Path.Combine(AppContext.BaseDirectory, "Fixtures", "1017000034_1.png"));
        openCloud.Files["/Produkte/kaputt.png"] = "kein Bild"u8.ToArray();

        factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b.ConfigureTestServices(services =>
            services.AddHttpClient<OpenCloudClient>().ConfigurePrimaryHttpMessageHandler(() => openCloud)));
    }

    public void Dispose()
    {
        factory.Dispose();
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        Directory.Delete(dataDir, true);
    }

    private HttpClient Client(string? token = FakeOpenCloud.Token)
    {
        var client = factory.CreateClient();
        if (token is not null) client.DefaultRequestHeaders.TryAddWithoutValidation("Authorization", token);
        return client;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task HealthNeedsNoLogin()
    {
        var response = await Client(null).GetAsync("/health", Ct);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Bearer expired")]
    public async Task ApiNeedsAValidOpenCloudLogin(string? token)
    {
        var response = await Client(token).GetAsync("/imapi/api/config", Ct);
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ReturnsTheDefaultPresets()
    {
        var config = await Client().GetFromJsonAsync<JsonElement>("/imapi/api/config", Ct);
        var names = config.GetProperty("presets").EnumerateArray().Select(p => p.GetProperty("name").GetString());
        Assert.Equal(["web", "thumb", "100x100", "200x200", "2000x2000", "max"], names);
        Assert.Equal("konvertiert", config.GetProperty("defaultOutputFolder").GetString());
    }

    [Fact]
    public async Task ConvertsIntoOpenCloudWithStoredTexts()
    {
        var client = Client();
        var put = await client.PutAsJsonAsync("/imapi/api/texts/1017000034",
            new { texts = new Dictionary<string, string> { ["de"] = "Temperaturfühler" } }, Ct);
        put.EnsureSuccessStatusCode();
        var texts = await put.Content.ReadFromJsonAsync<JsonElement>(Ct);
        Assert.Equal("Alan Turing", texts.GetProperty("updatedBy").GetString());

        var response = await client.PostAsJsonAsync("/imapi/api/convert", new
        {
            files = new[] { new { spaceId = "space$1", path = "/Produkte/1017000034_1.png" } },
            presets = new[] { "web", "thumb" },
        }, Ct);
        response.EnsureSuccessStatusCode();
        var result = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct))[0];

        Assert.Equal(JsonValueKind.Null, result.GetProperty("error").ValueKind);
        Assert.Equal("1017000034", result.GetProperty("artikelnummer").GetString());
        var outputs = result.GetProperty("outputs").EnumerateArray()
            .Select(o => (o.GetProperty("path").GetString()!, o.GetProperty("withText").GetBoolean())).ToList();
        Assert.Equal(
        [
            ("/Produkte/konvertiert/DE/web/1017000034_1.png", true),
            ("/Produkte/konvertiert/EN/web/1017000034_1.png", false),
            ("/Produkte/konvertiert/DE/thumb/1017000034_1.png", true),
        ], outputs);
        Assert.All(outputs, o => Assert.True(openCloud.Files.ContainsKey(o.Item1)));
        Assert.Contains("/Produkte/konvertiert/DE/web", openCloud.Folders);
        // Every call to OpenCloud carried the user's own token.
        Assert.All(openCloud.Authorizations, a => Assert.Equal(FakeOpenCloud.Token, a));
    }

    [Fact]
    public async Task ReportsErrorsPerFile()
    {
        var response = await Client().PostAsJsonAsync("/imapi/api/convert", new
        {
            files = new[]
            {
                new { spaceId = "space$1", path = "/Produkte/fehlt.png" },
                new { spaceId = "space$1", path = "/Produkte/kaputt.png" },
                new { spaceId = "space$1", path = "/Produkte/1017000034_1.png" },
            },
            presets = new[] { "100x100" },
            languages = new[] { "EN" },
            outputFolder = "/Ausgabe",
        }, Ct);
        response.EnsureSuccessStatusCode();
        var results = (await response.Content.ReadFromJsonAsync<JsonElement>(Ct)).EnumerateArray().ToList();

        Assert.Contains("nicht gefunden", results[0].GetProperty("error").GetString());
        Assert.Contains("kann nicht gelesen", results[1].GetProperty("error").GetString());
        Assert.Equal(JsonValueKind.Null, results[2].GetProperty("error").ValueKind);
        Assert.True(openCloud.Files.ContainsKey("/Ausgabe/EN/100x100/1017000034_1.png"));
    }

    [Fact]
    public async Task RejectsUnknownPresets()
    {
        var response = await Client().PostAsJsonAsync("/imapi/api/convert", new
        {
            files = new[] { new { spaceId = "space$1", path = "/Produkte/1017000034_1.png" } },
            presets = new[] { "riesig" },
        }, Ct);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task ManagesTexts()
    {
        var client = Client();
        (await client.PutAsJsonAsync("/imapi/api/texts/4711",
            new { texts = new Dictionary<string, string> { ["DE"] = "Sensor", ["EN"] = "sensor" } }, Ct)).EnsureSuccessStatusCode();
        (await client.PutAsJsonAsync("/imapi/api/texts/4711",
            new { texts = new Dictionary<string, string?> { ["EN"] = null } }, Ct)).EnsureSuccessStatusCode();

        var list = await client.GetFromJsonAsync<JsonElement>("/imapi/api/texts?q=Sens", Ct);
        var article = Assert.Single(list.EnumerateArray());
        Assert.Equal("Sensor", article.GetProperty("texts").GetProperty("DE").GetString());
        Assert.False(article.GetProperty("texts").TryGetProperty("EN", out _));

        var described = await client.GetFromJsonAsync<JsonElement>("/imapi/api/describe?name=4711_2_EN.png&name=foto.jpg", Ct);
        Assert.Equal("4711", described[0].GetProperty("artikelnummer").GetString());
        Assert.Equal("EN", described[0].GetProperty("language").GetString());
        Assert.Equal("Sensor", described[0].GetProperty("texts").GetProperty("DE").GetString());
        Assert.Equal(JsonValueKind.Null, described[1].GetProperty("artikelnummer").ValueKind);

        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PutAsJsonAsync("/imapi/api/texts/abc", new { texts = new Dictionary<string, string>() }, Ct)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest,
            (await client.PutAsJsonAsync("/imapi/api/texts/4711", new { texts = new Dictionary<string, string> { ["FR"] = "x" } }, Ct)).StatusCode);

        (await client.DeleteAsync("/imapi/api/texts/4711", Ct)).EnsureSuccessStatusCode();
        Assert.Empty((await client.GetFromJsonAsync<JsonElement>("/imapi/api/texts", Ct)).EnumerateArray());
    }
}
