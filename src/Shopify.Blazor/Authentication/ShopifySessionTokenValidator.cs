using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shopify.Blazor.Internal;

namespace Shopify.Blazor.Authentication;

/// <summary>
/// A verified App Bridge session token (an ID token). Every property here has been checked
/// against Shopify's published signing keys before it was populated.
/// </summary>
/// <param name="ShopDomain">The canonical <c>name.myshopify.com</c> domain from the <c>dest</c> claim.</param>
/// <param name="UserId">The numeric merchant user id from the <c>sub</c> claim.</param>
/// <param name="ExpiresAt">Session token expiry. These are short lived, on the order of a minute.</param>
/// <param name="IssuedAt">When Shopify issued the token.</param>
/// <param name="Scopes">Scopes carried by the token, when present.</param>
/// <param name="RawToken">The original serialized JWT, needed as the <c>subject_token</c> for token exchange.</param>
public sealed record ShopifySessionToken(
    string ShopDomain,
    string UserId,
    DateTimeOffset ExpiresAt,
    DateTimeOffset IssuedAt,
    IReadOnlyList<string> Scopes,
    string RawToken);

/// <summary>Outcome of session token validation, so callers can report why it failed.</summary>
public enum ShopifySessionTokenValidationFailure
{
    /// <summary>No token was supplied.</summary>
    Missing,

    /// <summary>The token is malformed, or its signature did not verify.</summary>
    Invalid,

    /// <summary>The token verified but is outside its validity window.</summary>
    Expired,

    /// <summary>The token was issued for a different shop than the one being addressed.</summary>
    WrongShop,
}

/// <summary>Result of <see cref="IShopifySessionTokenValidator.ValidateAsync"/>.</summary>
public sealed class ShopifySessionTokenValidationResult
{
    private ShopifySessionTokenValidationResult(ShopifySessionToken? token, ShopifySessionTokenValidationFailure? failure, string? reason)
    {
        SessionToken = token;
        Failure = failure;
        Reason = reason;
    }

    /// <summary>The verified token, or <c>null</c> when validation failed.</summary>
    public ShopifySessionToken? SessionToken { get; }

    /// <summary>Why validation failed, or <c>null</c> on success.</summary>
    public ShopifySessionTokenValidationFailure? Failure { get; }

    /// <summary>A short human-readable explanation, safe to log. Never echoes the token.</summary>
    public string? Reason { get; }

    /// <summary>True when <see cref="SessionToken"/> is usable.</summary>
    public bool Succeeded => SessionToken is not null;

    internal static ShopifySessionTokenValidationResult Success(ShopifySessionToken token) => new(token, null, null);

    internal static ShopifySessionTokenValidationResult Fail(ShopifySessionTokenValidationFailure failure, string reason)
        => new(null, failure, reason);
}

/// <summary>Validates App Bridge session tokens.</summary>
public interface IShopifySessionTokenValidator
{
    /// <summary>
    /// Verifies signature, audience, issuer and expiry, and confirms the token was issued for
    /// <paramref name="expectedShopDomain"/> when one is supplied.
    /// </summary>
    /// <returns>A result; this method does not throw for an unusable token.</returns>
    Task<ShopifySessionTokenValidationResult> ValidateAsync(
        string? sessionToken,
        string? expectedShopDomain = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Validates App Bridge session tokens against the shop's published JWKS endpoint.
/// </summary>
/// <remarks>
/// This is the server-side half of the embedded-app handshake, and the check that makes the rest
/// of the app trustworthy. Three things it deliberately does <em>not</em> accept:
/// <list type="bullet">
/// <item>a query-string or form parameter as proof of identity (only a signed token counts);</item>
/// <item>an unsigned or <c>alg: none</c> token;</item>
/// <item>a token whose <c>dest</c> claim points somewhere other than the shop being addressed.</item>
/// </list>
/// </remarks>
public sealed class ShopifySessionTokenValidator : IShopifySessionTokenValidator
{
    // Shopify publishes its key set per shop. Twelve hours is comfortably inside the rotation
    // window and every miss falls back to a refresh, so a shorter value buys nothing.
    private static readonly TimeSpan JwksCacheDuration = TimeSpan.FromHours(12);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly IOptionsMonitor<ShopifyOptions> _options;
    private readonly ILogger<ShopifySessionTokenValidator> _logger;
    private readonly JsonWebTokenHandler _handler = new() { MapInboundClaims = false };

    /// <summary>Creates a validator.</summary>
    public ShopifySessionTokenValidator(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IOptionsMonitor<ShopifyOptions> options,
        ILogger<ShopifySessionTokenValidator> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ShopifySessionTokenValidationResult> ValidateAsync(
        string? sessionToken,
        string? expectedShopDomain = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            return ShopifySessionTokenValidationResult.Fail(
                ShopifySessionTokenValidationFailure.Missing, "No session token was supplied.");
        }

        var options = _options.CurrentValue;

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return ShopifySessionTokenValidationResult.Fail(
                ShopifySessionTokenValidationFailure.Invalid, "Shopify:ApiKey is not configured, so no token can be trusted.");
        }

        // Read the destination without trusting it. Shopify's own guidance is to parse the token,
        // extract the shop, then validate the signature *against that shop's* keys — which is why
        // the domain check happens here, before anything is fetched, rather than afterwards.
        string? destination;
        try
        {
            var unvalidated = _handler.ReadJsonWebToken(sessionToken);
            destination = unvalidated.TryGetClaim("dest", out var dest) ? dest.Value : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Session token could not be parsed.");
            return ShopifySessionTokenValidationResult.Fail(
                ShopifySessionTokenValidationFailure.Invalid, "The session token is not a readable JWT.");
        }

        var shopFromToken = ShopDomain.TryNormalize(destination);

        if (shopFromToken is null)
        {
            // Report what was rejected, but never echo the whole token.
            _logger.LogWarning("Session token rejected: 'dest' claim '{Destination}' is not a myshopify shop domain.", destination);
            return ShopifySessionTokenValidationResult.Fail(
                ShopifySessionTokenValidationFailure.Invalid, "The session token has no usable 'dest' claim.");
        }

        string? expected = null;
        if (!string.IsNullOrWhiteSpace(expectedShopDomain))
        {
            expected = ShopDomain.TryNormalize(expectedShopDomain);
            if (expected is null)
            {
                return ShopifySessionTokenValidationResult.Fail(
                    ShopifySessionTokenValidationFailure.Invalid, "The expected shop domain is not a myshopify.com domain.");
            }
        }

        if (expected is not null && !string.Equals(expected, shopFromToken, StringComparison.Ordinal))
        {
            return ShopifySessionTokenValidationResult.Fail(
                ShopifySessionTokenValidationFailure.WrongShop,
                $"The session token is for '{shopFromToken}' but the request addressed '{expected}'.");
        }

        JsonWebKeySet keySet;
        try
        {
            keySet = await GetKeySetAsync(shopFromToken, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not fetch the signing keys for {Shop}.", shopFromToken);
            return ShopifySessionTokenValidationResult.Fail(
                ShopifySessionTokenValidationFailure.Invalid, "The shop's signing keys could not be retrieved.");
        }

        var parameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = $"https://{shopFromToken}/admin",
            ValidateAudience = true,
            ValidAudience = options.ApiKey,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKeys = keySet.Keys,
            ClockSkew = options.ClockSkew,
            NameClaimType = "sub",
        };

        var result = await _handler.ValidateTokenAsync(sessionToken, parameters).ConfigureAwait(false);

        if (!result.IsValid)
        {
            var exception = result.Exception;
            var failure = exception is SecurityTokenExpiredException
                ? ShopifySessionTokenValidationFailure.Expired
                : ShopifySessionTokenValidationFailure.Invalid;

            _logger.LogWarning(
                "Session token rejected for {Shop}: {Reason}",
                shopFromToken,
                exception?.Message ?? "unknown validation failure");

            return ShopifySessionTokenValidationResult.Fail(failure, exception?.Message ?? "Session token validation failed.");
        }

        var jwt = result.SecurityToken as JsonWebToken;
        var expires = jwt?.ValidTo ?? DateTime.UtcNow;
        var issued = jwt?.IssuedAt ?? DateTime.UtcNow;
        var userId = jwt is not null && jwt.TryGetClaim("sub", out var sub) ? sub.Value : string.Empty;
        var scopes = (jwt is not null && jwt.TryGetClaim("scp", out var scp) ? scp.Value : null)?
            .Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? [];

        return ShopifySessionTokenValidationResult.Success(new ShopifySessionToken(
            shopFromToken,
            userId,
            new DateTimeOffset(DateTime.SpecifyKind(expires, DateTimeKind.Utc)),
            new DateTimeOffset(DateTime.SpecifyKind(issued, DateTimeKind.Utc)),
            scopes,
            sessionToken));
    }

    private async Task<JsonWebKeySet> GetKeySetAsync(string shopDomain, CancellationToken cancellationToken)
    {
        var jwksUrl = _options.CurrentValue.JwksUrlOverride;
        if (string.IsNullOrWhiteSpace(jwksUrl))
        {
            jwksUrl = $"https://{shopDomain}/.well-known/jwks.json";
        }

        var cacheKey = "shopify:jwks:" + jwksUrl;

        if (_cache.TryGetValue(cacheKey, out JsonWebKeySet? cached) && cached is not null)
        {
            return cached;
        }

        var client = _httpClientFactory.CreateClient(ShopifyHttpClients.Public);
        var json = await client.GetStringAsync(jwksUrl, cancellationToken).ConfigureAwait(false);
        var keySet = new JsonWebKeySet(json);

        _cache.Set(cacheKey, keySet, JwksCacheDuration);

        return keySet;
    }
}
