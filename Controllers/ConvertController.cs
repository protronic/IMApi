using IMApi.Options;
using IMApi.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace IMApi.Controllers;

public record ClientConfig(IReadOnlyList<string> Languages, IReadOnlyList<ConversionPreset> Presets, string DefaultOutputFolder);

[ApiController]
[Route("api")]
[OpenCloudAuth]
public class ConvertController(ConversionService conversions, IOptions<ImApiOptions> options) : ControllerBase
{
    /// <summary>Languages, presets and the default output folder for the conversion dialog.</summary>
    [HttpGet("config")]
    public ClientConfig GetConfig() => new(
        options.Value.EffectiveLanguages,
        options.Value.EffectivePresets,
        options.Value.DefaultOutputFolder);

    /// <summary>
    /// Converts images stored in OpenCloud and writes the results back into OpenCloud,
    /// with the permissions of the calling user.
    /// </summary>
    [HttpPost("convert")]
    public async Task<ActionResult<List<ConversionResult>>> Convert(ConvertRequest request, CancellationToken ct)
    {
        var error = conversions.Validate(request);
        if (error is not null) return Problem(title: error, statusCode: StatusCodes.Status400BadRequest);

        var user = OpenCloudAuthFilter.CurrentUser(HttpContext);
        return await conversions.ConvertAsync(request, Request.Headers.Authorization.ToString(),
            user.UserName ?? user.Id, ct);
    }
}
