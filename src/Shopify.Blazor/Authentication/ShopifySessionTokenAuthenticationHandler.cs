using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

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
            // A token was presented and rejected: this is the line that matters.
            Logger.LogWarning(
                "Session token rejected on {Path} ({Failure}): {Reason}",
                Request.Path,
                validation.Failure,
                validation.Reason);

            return AuthenticateResult.Fail(validation.Reason ?? "The session token is not valid.");
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
}
