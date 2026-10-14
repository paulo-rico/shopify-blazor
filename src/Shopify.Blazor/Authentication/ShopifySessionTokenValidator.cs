using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Shopify.Blazor.Internal;

namespace Shopify.Blazor.Authentication;

/// <summary>
/// A verified App Bridge session token (an ID token). Every property here has been checked
/// against the app's signing secret before it was populated.
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
    /// Verifies the signature, audience and lifetime, and confirms the token was issued for
    /// <paramref name="expectedShopDomain"/> when one is supplied.
    /// </summary>
    /// <returns>A result; this method does not throw for an unusable token.</returns>
    Task<ShopifySessionTokenValidationResult> ValidateAsync(
        string? sessionToken,
        string? expectedShopDomain = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Validates App Bridge session tokens as HMAC-SHA256 JWTs signed with the app's own API secret.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Session tokens are symmetric.</strong> Shopify signs them HS256 using the app's client
/// secret, so verifying one needs the secret and nothing else — no per-shop key set, no network
/// call. This mirrors what Shopify's own libraries do: the <c>shopify_api</c> gem decodes with
/// <c>JWT.decode(token, api_secret_key, true, algorithm: "HS256")</c> and compares the
/// <c>aud</c> claim with the app's API key.
/// </para>
/// <para>
/// An earlier version of this class fetched <c>https://{shop}/.well-known/jwks.json</c> and
/// verified an RS256 signature. That endpoint belongs to the <em>Storefront</em> and
/// <em>Customer Account</em> APIs, not to admin session tokens, and it sits behind the storefront:
/// a password-protected store — which every Shopify dev store is — answers it with the storefront
/// password page, so validation could never have worked there. Verified: that URL returns
/// <c>302 → /password</c> for a locked store, and Shopify documents that a dev store's password
/// cannot be removed.
/// </para>
/// <para>
/// Three things this deliberately does <em>not</em> accept:
/// <list type="bullet">
/// <item>a query-string or form parameter as proof of identity (only a signed token counts);</item>
/// <item>an unsigned token, or one signed with a different algorithm — the algorithm is pinned to
/// HS256, so an <c>alg: none</c> token cannot be downgraded into acceptance;</item>
/// <item>a token whose <c>aud</c> claim is a different app, or whose <c>dest</c> claim points
/// somewhere other than the shop being addressed.</item>
/// </list>
/// </para>
/// </remarks>
public sealed class ShopifySessionTokenValidator : IShopifySessionTokenValidator
{
    /// <summary>
    /// Tolerance for clock differences, matching the leeway Shopify's own libraries apply.
    /// </summary>
    private static readonly TimeSpan DefaultClockSkew = TimeSpan.FromSeconds(10);

    private readonly IOptionsMonitor<ShopifyOptions> _options;
    private readonly ILogger<ShopifySessionTokenValidator> _logger;
    private readonly JsonWebTokenHandler _handler = new() { MapInboundClaims = false };

    /// <summary>Creates a validator.</summary>
    public ShopifySessionTokenValidator(
        IOptionsMonitor<ShopifyOptions> options,
        ILogger<ShopifySessionTokenValidator> logger)
    {
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public Task<ShopifySessionTokenValidationResult> ValidateAsync(
        string? sessionToken,
        string? expectedShopDomain = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sessionToken))
        {
            return Task.FromResult(ShopifySessionTokenValidationResult.Fail(
                ShopifySessionTokenValidationFailure.Missing, "No session token was supplied."));
        }

        var options = _options.CurrentValue;

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            return Task.FromResult(ShopifySessionTokenValidationResult.Fail(
                ShopifySessionTokenValidationFailure.Invalid,
                "Shopify:ApiKey is not configured, so no token can be trusted."));
        }

        if (string.IsNullOrWhiteSpace(options.ApiSecret))
        {
            // Without the secret no signature can be checked, so every token must be refused rather
            // than accepted unverified.
            return Task.FromResult(ShopifySessionTokenValidationResult.Fail(
                ShopifySessionTokenValidationFailure.Invalid,
                "Shopify:ApiSecret is not configured, so a session token's signature cannot be verified."));
        }

        // Read the destination without trusting it, then validate the domain before anything else.
        string? destination;
        try
        {
            var unvalidated = _handler.ReadJsonWebToken(sessionToken);
            destination = unvalidated.TryGetClaim("dest", out var dest) ? dest.Value : null;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Session token could not be parsed.");
            return Task.FromResult(ShopifySessionTokenValidationResult.Fail(
                ShopifySessionTokenValidationFailure.Invalid, "The session token is not a readable JWT."));
        }

        var shopFromToken = ShopDomain.TryNormalize(destination);

        if (shopFromToken is null)
        {
            _logger.LogWarning("Session token rejected: 'dest' claim '{Destination}' is not a myshopify shop domain.", destination);
            return Task.FromResult(ShopifySessionTokenValidationResult.Fail(
                ShopifySessionTokenValidationFailure.Invalid, "The session token has no usable 'dest' claim."));
        }

        if (!string.IsNullOrWhiteSpace(expectedShopDomain))
        {
            var expected = ShopDomain.TryNormalize(expectedShopDomain);

            if (expected is null)
            {
                return Task.FromResult(ShopifySessionTokenValidationResult.Fail(
                    ShopifySessionTokenValidationFailure.Invalid, "The expected shop domain is not a myshopify.com domain."));
            }

            if (!string.Equals(expected, shopFromToken, StringComparison.Ordinal))
            {
                return Task.FromResult(ShopifySessionTokenValidationResult.Fail(
                    ShopifySessionTokenValidationFailure.WrongShop,
                    $"The session token is for '{shopFromToken}' but the request addressed '{expected}'."));
            }
        }

        // Microsoft's JWT handler refuses an HS256 key shorter than 128 bits, while Shopify's own
        // libraries (and Ruby's JWT gem) use the secret as-is with no minimum. A short secret
        // would therefore fail *every* token with a message about key size, which reads like a
        // configuration error rather than what it is. Say so plainly instead, and treat it as a
        // configuration problem rather than a token problem.
        var secretBytes = Encoding.UTF8.GetBytes(options.ApiSecret);

        if (secretBytes.Length < 16)
        {
            _logger.LogError(
                "Shopify:ApiSecret is {Length} characters, which is shorter than the 16 bytes (128 bits) " +
                "that HMAC-SHA256 requires. Session tokens cannot be verified with it. A Shopify client " +
                "secret is 32 hexadecimal characters; check the value was copied in full.",
                options.ApiSecret.Length);

            return Task.FromResult(ShopifySessionTokenValidationResult.Fail(
                ShopifySessionTokenValidationFailure.Invalid,
                $"Shopify:ApiSecret is only {options.ApiSecret.Length} characters, which is shorter than the " +
                "16 bytes HMAC-SHA256 requires, so no session token can be verified."));
        }

        var parameters = new TokenValidationParameters
        {
            // HS256 with the API secret. This is the whole trust anchor: no key set, no network
            // call, and nothing a password-protected storefront can obstruct.
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(secretBytes),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],

            // The audience is the app's API key, so a token minted for any other app is refused.
            ValidateAudience = true,
            ValidAudience = options.ApiKey,

            ValidateLifetime = true,
            ClockSkew = options.ClockSkew == TimeSpan.Zero ? DefaultClockSkew : options.ClockSkew,

            // The issuer is https://{shop}/admin, which the dest check above already covers; pinning
            // it to a fixed string would add nothing.
            ValidateIssuer = false,
            NameClaimType = "sub",
        };

        var result = _handler.ValidateTokenAsync(sessionToken, parameters).GetAwaiter().GetResult();

        if (!result.IsValid)
        {
            var exception = result.Exception;

            // Two exception types can mean "expired". SecurityTokenExpiredException is the obvious
            // one, but a lifetime failure is often reported as SecurityTokenInvalidLifetimeException
            // with a message that covers both "already expired" and "not valid yet". Treating only
            // the first as expired told the caller a token was Invalid, which sends them looking for
            // a signature or audience problem instead of fetching a fresh token a second later.
            var failure = exception switch
            {
                SecurityTokenExpiredException => ShopifySessionTokenValidationFailure.Expired,
                SecurityTokenInvalidLifetimeException lifetime
                    when lifetime.Message.Contains("is after Expires", StringComparison.Ordinal)
                        || lifetime.Message.Contains("Expired", StringComparison.OrdinalIgnoreCase)
                    => ShopifySessionTokenValidationFailure.Expired,
                _ => ShopifySessionTokenValidationFailure.Invalid,
            };

            _logger.LogWarning(
                "Session token rejected for {Shop}: {Reason}",
                shopFromToken,
                exception?.Message ?? "unknown validation failure");

            return Task.FromResult(ShopifySessionTokenValidationResult.Fail(
                failure, exception?.Message ?? "Session token validation failed."));
        }

        var jwt = result.SecurityToken as JsonWebToken;
        var expires = jwt?.ValidTo ?? DateTime.UtcNow;
        var issued = jwt?.IssuedAt ?? DateTime.UtcNow;
        var userId = jwt is not null && jwt.TryGetClaim("sub", out var sub) ? sub.Value : string.Empty;
        var scopes = (jwt is not null && jwt.TryGetClaim("scp", out var scp) ? scp.Value : null)?
            .Split([' ', ','], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            ?? [];

        return Task.FromResult(ShopifySessionTokenValidationResult.Success(new ShopifySessionToken(
            shopFromToken,
            userId,
            new DateTimeOffset(DateTime.SpecifyKind(expires, DateTimeKind.Utc)),
            new DateTimeOffset(DateTime.SpecifyKind(issued, DateTimeKind.Utc)),
            scopes,
            sessionToken)));
    }
}
