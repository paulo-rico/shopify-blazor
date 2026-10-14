namespace Shopify.Blazor.SampleApp.Services;

/// <summary>Records every request to <c>/api/</c> so a 401 can be explained after the fact.</summary>
public sealed class RequestObservationMiddleware(RequestDelegate next)
{
    /// <summary>Runs the middleware.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        if (!context.Request.Path.StartsWithSegments("/api")
            || context.Request.Path.StartsWithSegments("/api/diagnostics"))
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        var header = context.Request.Headers.Authorization.ToString();
        var token = BearerTokens.Read(context);

        var scheme = string.IsNullOrWhiteSpace(header)
            ? null
            : header.Split(' ', 2)[0];

        await next(context).ConfigureAwait(false);

        RequestObservationLog.Record(new RequestObservation(
            DateTimeOffset.UtcNow,
            context.Request.Path.ToString(),
            !string.IsNullOrWhiteSpace(header),
            scheme,
            token?.Length,
            $"HTTP {context.Response.StatusCode}"));
    }
}
