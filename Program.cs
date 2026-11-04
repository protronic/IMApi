using IMApi.Data;
using IMApi.Options;
using IMApi.Services;
using ImageMagick;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

// Optional file for site-specific settings such as the presets (mounted in the container).
builder.Configuration.AddJsonFile("config/imapi.json", optional: true, reloadOnChange: false);
builder.Configuration.AddEnvironmentVariables();

var section = builder.Configuration.GetSection(ImApiOptions.Section);
builder.Services.Configure<ImApiOptions>(section);
var options = section.Get<ImApiOptions>() ?? new ImApiOptions();

var databasePath = Path.GetFullPath(options.DatabasePath);
Directory.CreateDirectory(Path.GetDirectoryName(databasePath)!);
builder.Services.AddDbContext<ImApiDbContext>(o => o.UseSqlite($"Data Source={databasePath}"));

builder.Services.AddHttpClient<OpenCloudClient>(http =>
    {
        http.BaseAddress = new Uri(options.OpenCloudUrl.TrimEnd('/') + "/");
        http.Timeout = TimeSpan.FromMinutes(5);
    })
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
    {
        // Self-signed OpenCloud certificates (INSECURE=true in opencloud-compose).
        ServerCertificateCustomValidationCallback = options.OpenCloudInsecure
            ? HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            : null,
    });

builder.Services.AddMemoryCache();
builder.Services.AddSingleton<ImageConverter>();
builder.Services.AddScoped<ConversionService>();
builder.Services.AddScoped<OpenCloudAuthFilter>();
builder.Services.AddControllers();
builder.Services.AddHealthChecks();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

var app = builder.Build();

// The OpenCloud proxy forwards /imapi/... unchanged.
if (!string.IsNullOrEmpty(options.PathBase))
    app.UsePathBase(options.PathBase);
app.UseRouting();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<ImApiDbContext>().Database.Migrate();
}

MagickNET.Initialize();
// Guard against decompression bombs in uploaded images.
ResourceLimits.Width = 30_000;
ResourceLimits.Height = 30_000;

app.Logger.LogInformation("OpenCloud: {Url}, database: {Database}, presets: {Presets}",
    options.OpenCloudUrl, databasePath,
    string.Join(", ", app.Services.GetRequiredService<IOptions<ImApiOptions>>().Value.EffectivePresets.Select(p => p.Name)));

app.UseSwagger();
app.UseSwaggerUI();
app.MapHealthChecks("/health");
app.MapControllers();

app.Run();

public partial class Program;
