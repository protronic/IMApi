using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Caching.Memory;

namespace IMApi.Services;

/// <summary>
/// Requires an OpenCloud login: the Authorization header of the request is checked by
/// asking OpenCloud who the user is (cached briefly). The service itself stores no
/// credentials and trusts no network position.
/// </summary>
public class OpenCloudAuthFilter(OpenCloudClient openCloud, IMemoryCache cache, ILogger<OpenCloudAuthFilter> logger)
    : IAsyncActionFilter
{
    private const string ItemKey = "OpenCloudUser";
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(2);

    public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
    {
        var authorization = context.HttpContext.Request.Headers.Authorization.ToString();
        if (string.IsNullOrWhiteSpace(authorization))
        {
            context.Result = Problem(StatusCodes.Status401Unauthorized, "Anmeldung bei OpenCloud erforderlich");
            return;
        }

        var key = "auth:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(authorization)));
        if (!cache.TryGetValue(key, out OpenCloudUser? user))
        {
            try
            {
                user = await openCloud.GetMeAsync(authorization, context.HttpContext.RequestAborted);
                cache.Set(key, user, CacheDuration);
            }
            catch (OpenCloudException e) when (e.Status is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.Forbidden)
            {
                context.Result = Problem(StatusCodes.Status401Unauthorized, e.Message);
                return;
            }
            catch (Exception e) when (e is OpenCloudException or HttpRequestException)
            {
                logger.LogError(e, "OpenCloud is not reachable for the login check");
                context.Result = Problem(StatusCodes.Status502BadGateway, "OpenCloud ist nicht erreichbar");
                return;
            }
        }

        context.HttpContext.Items[ItemKey] = user;
        await next();
    }

    public static OpenCloudUser CurrentUser(HttpContext context) =>
        context.Items[ItemKey] as OpenCloudUser
        ?? throw new InvalidOperationException("OpenCloudAuthFilter did not run");

    private static ObjectResult Problem(int status, string detail) =>
        new(new ProblemDetails { Status = status, Title = detail }) { StatusCode = status };
}

/// <summary>Marks controllers that need an OpenCloud login.</summary>
public class OpenCloudAuthAttribute() : TypeFilterAttribute(typeof(OpenCloudAuthFilter));
