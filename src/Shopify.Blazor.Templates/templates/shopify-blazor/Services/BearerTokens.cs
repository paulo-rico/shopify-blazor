namespace Shopify.Blazor.SampleApp.Services;

/// <summary>Reads a bearer token from a request, in one place.</summary>
/// <remarks>
/// The endpoint and the diagnostic both need this, and if they read the header differently a bug in
/// one looks like a bug in the other. It also keeps "no header" distinguishable from "the header is
/// not a bearer token", which a bare <c>Results.Unauthorized()</c> throws away.
/// </remarks>
public static class BearerTokens
{
    private const string Prefix = "Bearer ";

    /// <summary>Returns the token, or <c>null</c> when the request carries no usable one.</summary>
    public static string? Read(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var header = context.Request.Headers.Authorization.ToString();

        if (string.IsNullOrWhiteSpace(header)
            || header.Length <= Prefix.Length
            || !header.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var token = header[Prefix.Length..].Trim();

        return token.Length == 0 ? null : token;
    }
}
