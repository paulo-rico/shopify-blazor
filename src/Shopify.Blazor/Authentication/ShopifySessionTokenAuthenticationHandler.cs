using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shopify.Blazor.Diagnostics;
using Shopify.Blazor.Internal;

namespace Shopify.Blazor.Authentication;

/// <summary>Authentication scheme name used for App Bridge session tokens.</summary>
public static class ShopifyAuthenticationDefaults
{
    /// <summary>The scheme an app should pass to <c>RequireAuthorization</c> and friends.</summary>
    public const string Scheme = "ShopifySessionToken";

    /// <summary>The claim type carrying the canonical shop domain.</summary>
    public const string ShopDomainClaim = "shopify:shop_domain";

    /// <summary>The claim type carrying the merchant user id.</summary>
    public const string UserIdClaim = "shopify:user_id";
}

/// <summary>Options for <see cref="ShopifySessionTokenAuthenticationHandler"/>.</summary>
public sealed class ShopifyAuthenticationOptions : AuthenticationSchemeOptions
{
    /// <summary>
    /// When set, a token whose <c>dest</c> claim is a different shop is rejected. Leave
    /// <c>null</c> in an embedded app: the shop is whatever the token says it is, which is
    /// exactly why the signature check matters.
    /// </summary>
    public string? ExpectedShopDomain { get; set; }

    /// <summary>
    /// Records the explanation for a rejection in the application's
    /// <see cref="ISessionTokenRejectionLog"/>, so a developer can read why the last request failed.
    /// </summary>
    /// <remarks>
    /// Off in production, where the reason belongs in the log only. The explanation names claim
    /// values, which is useful to a developer and unnecessary for an unauthenticated caller.
    /// </remarks>
    public bool IncludeFailureDetail { get; set; } = true;
}

/// <summary>
/// Authenticates requests carrying an <c>Authorization: Bearer &lt;App Bridge session token&gt;</c>
/// header.
/// </summary>
/// <remarks>
/// The browser gets the token from App Bridge (<c>await shopify.idToken()</c>) and the server
/// verifies its signature against Shopify's published keys before any claim in it is believed.
/// A query-string or cookie value is never treated as identity.
/// </remarks>
public sealed class ShopifySessionTokenAuthenticationHandler : AuthenticationHandler<ShopifyAuthenticationOptions>
{
    private readonly IShopifySessionTokenValidator _validator;

    /// <summary>Creates the handler.</summary>
    public ShopifySessionTokenAuthenticationHandler(
        IOptionsMonitor<ShopifyAuthenticationOptions> options,
        ILoggerFactory logger,
        UrlEncoder encoder,
        IShopifySessionTokenValidator validator)
        : base(options, logger, encoder)
    {
        _validator = validator;
    }

    /// <summary>
    /// Where the most recent rejections are recorded.
    /// </summary>
    /// <remarks>
    /// Resolved from the request's services rather than injected: an authentication scheme is built
    /// from its options by the framework, so its constructor only receives the handful of types the
    /// handler base declares. Resolving here keeps the log a normal singleton.
    /// </remarks>
    private ISessionTokenRejectionLog Rejections =>
        Context.RequestServices.GetService(typeof(ISessionTokenRejectionLog)) as ISessionTokenRejectionLog
        ?? NullRejectionLog.Instance;

    /// <inheritdoc />
    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var headerValues))
        {
            // NoResult, not Fail: this scheme is the default, so it runs for every page and static
            // asset request too. Reporting a failure for those fills the log with warnings and
            // hides the one line that matters.
            Logger.LogDebug("No Authorization header on {Path}; nothing to authenticate.", Request.Path);
            return AuthenticateResult.NoResult();
        }

        var header = headerValues.ToString();
        const string prefix = "Bearer ";

        if (header.Length <= prefix.Length || !header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            Logger.LogWarning(
                "Authorization header on {Path} is not a bearer token: {Scheme}.",
                Request.Path,
                header.Split(' ', 2)[0]);
            return AuthenticateResult.Fail("The Authorization header is not a bearer token.");
        }

        var token = header[prefix.Length..].Trim();
        if (token.Length == 0)
        {
            Logger.LogWarning("Bearer token on {Path} is empty.", Request.Path);
            return AuthenticateResult.Fail("The bearer token is empty.");
        }

        // Enough to identify which token was presented without putting a credential in the log.
        var segments = token.Split('.').Length;
        Logger.LogDebug("Verifying a {Length}-character bearer token with {Segments} JWT segments.", token.Length, segments);

        var validation = await _validator
            .ValidateAsync(token, Options.ExpectedShopDomain, Context.RequestAborted)
            .ConfigureAwait(false);

        if (!validation.Succeeded || validation.SessionToken is null)
        {
            var hint = Explain(validation, token);

            // A token was presented and rejected: this is the line that matters.
            Logger.LogWarning(
                "Session token rejected on {Path} ({Failure}): {Reason}{Hint}",
                Request.Path,
                validation.Failure,
                validation.Reason,
                string.IsNullOrEmpty(hint) ? string.Empty : $" {hint}");

            var authenticationFailure = new AuthenticationFailure(
                "session_token_rejected",
                validation.Failure?.ToString() ?? "Invalid",
                validation.Reason ?? "The session token is not valid.",
                string.IsNullOrEmpty(hint) ? null : hint);

            Rejections.Record(authenticationFailure, Request.Path);

            return AuthenticateResult.Fail(
                authenticationFailure.Hint is { Length: > 0 } tip
                    ? $"{authenticationFailure.Reason} {tip}"
                    : authenticationFailure.Reason);
        }

        var session = validation.SessionToken;

        var claims = new List<Claim>
        {
            new(ShopifyAuthenticationDefaults.ShopDomainClaim, session.ShopDomain),
            new(ShopifyAuthenticationDefaults.UserIdClaim, session.UserId),
            new(ClaimTypes.NameIdentifier, session.UserId),
        };

        foreach (var scope in session.Scopes)
        {
            claims.Add(new Claim("shopify:scope", scope));
        }

        var identity = new ClaimsIdentity(claims, ShopifyAuthenticationDefaults.Scheme);
        var principal = new ClaimsPrincipal(identity);

        return AuthenticateResult.Success(new AuthenticationTicket(principal, ShopifyAuthenticationDefaults.Scheme));
    }


    /// <summary>
    /// Turns a rejection into the thing the caller should do about it.
    /// </summary>
    /// <remarks>
    /// The audience check is reported first on purpose. A token minted for another app is signed
    /// by a <em>different shop's</em> keys, so verification fails, the key-set fetch fails, and the
    /// mechanical reason is "keys could not be retrieved" — which points at the network rather than
    /// at the actual misconfiguration.
    /// </remarks>
    private string Explain(ShopifySessionTokenValidationResult validation, string token)
    {
        if (validation.Failure is null)
        {
            return string.Empty;
        }

        var expectedAudience = Options.IncludeFailureDetail
            ? Context.RequestServices.GetService(typeof(IOptionsMonitor<ShopifyOptions>)) is IOptionsMonitor<ShopifyOptions> options
                ? options.CurrentValue.ApiKey
                : null
            : null;

        var claims = TryDecodeClaims(token);

        var audience = claims is not null && claims.TryGetValue("aud", out var aud) ? aud?.ToString() : null;

        if (!string.IsNullOrEmpty(expectedAudience)
            && !string.IsNullOrEmpty(audience)
            && !string.Equals(audience, expectedAudience, StringComparison.Ordinal))
        {
            return $"The aud claim is '{audience}', but this app's API key is '{expectedAudience}'. The token " +
                   "was minted for a different Shopify app: the App URL open in the Admin belongs to an app " +
                   "whose Client ID is not what this server is configured with. Check for a SHOPIFY_API_KEY " +
                   "environment variable overriding user secrets.";
        }

        return validation.Failure switch
        {
            ShopifySessionTokenValidationFailure.Expired =>
                "The session token has expired; it lives about a minute, so fetch a fresh one for each request.",
            ShopifySessionTokenValidationFailure.WrongShop =>
                "The token is for a different shop than the one the request addressed.",
            ShopifySessionTokenValidationFailure.Missing =>
                "No token was supplied.",
            _ => $"The token's dest claim is '{(claims is not null && claims.TryGetValue("dest", out var dest) ? dest?.ToString() : "missing")}'. " +
                 "Its signature, issuer or lifetime did not verify.",
        };
    }

    /// <summary>Decodes a JWT payload without verifying it, for explanatory messages only.</summary>
    private static Dictionary<string, object?>? TryDecodeClaims(string token)
    {
        var segments = token.Split('.');

        if (segments.Length != 3)
        {
            return null;
        }

        try
        {
            var padded = segments[1].Replace('-', '+').Replace('_', '/');
            padded = (padded.Length % 4) switch
            {
                2 => padded + "==",
                3 => padded + "=",
                _ => padded,
            };

            return System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, object?>>(
                System.Text.Encoding.UTF8.GetString(Convert.FromBase64String(padded)));
        }
        catch (Exception ex) when (ex is FormatException or System.Text.Json.JsonException)
        {
            return null;
        }
    }
}

/// <summary>
/// The reason an <c>Authorization</c> header was refused.
/// </summary>
/// <remarks>
/// A bare "Unauthorized" costs a developer an hour; "the aud claim is for a different app" costs
/// them a minute. <see cref="Hint"/> is only populated when the app opts in, so it can be shown to a
/// developer without telling an unauthenticated client why its credential failed.
/// </remarks>
public sealed record AuthenticationFailure(string Code, string Failure, string Reason, string? Hint);
