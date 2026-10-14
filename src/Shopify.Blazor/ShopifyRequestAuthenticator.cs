using Shopify.Blazor.Authentication;

namespace Shopify.Blazor;

/// <summary>The shop and merchant a request is acting for, after successful authentication.</summary>
/// <param name="ShopDomain">Canonical <c>name.myshopify.com</c> domain.</param>
/// <param name="UserId">The merchant user id from the session token's <c>sub</c> claim.</param>
public sealed record ShopifyShopContext(string ShopDomain, string UserId);

/// <summary>Outcome of turning a session token into an authenticated shop context.</summary>
/// <param name="Context">The context, or <c>null</c> on failure.</param>
/// <param name="Failure">Why authentication failed, or <c>null</c> on success.</param>
/// <param name="Reason">A short explanation safe to log.</param>
public sealed record ShopifyAuthenticationResult(
    ShopifyShopContext? Context,
    ShopifySessionTokenValidationFailure? Failure,
    string? Reason)
{
    /// <summary>True when <see cref="Context"/> is usable.</summary>
    public bool Succeeded => Context is not null;

    internal static ShopifyAuthenticationResult Success(ShopifyShopContext context) => new(context, null, null);

    internal static ShopifyAuthenticationResult Fail(ShopifySessionTokenValidationFailure failure, string reason)
        => new(null, failure, reason);
}

/// <summary>
/// Authenticates a request from its App Bridge session token and hands back a shop context.
/// </summary>
/// <remarks>
/// This is the one place a session token is turned into an Admin API access token, so every
/// protected endpoint gets the same treatment: verify the signature, confirm the shop, exchange
/// the token, and cache the result. Endpoints should depend on this rather than parsing tokens
/// themselves.
/// </remarks>
public interface IShopifyRequestAuthenticator
{
    /// <summary>
    /// Authenticates <paramref name="sessionToken"/>, optionally requiring that it was issued
    /// for <paramref name="expectedShopDomain"/>.
    /// </summary>
    Task<ShopifyAuthenticationResult> AuthenticateAsync(
        string? sessionToken,
        string? expectedShopDomain = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Default <see cref="IShopifyRequestAuthenticator"/>.</summary>
public sealed class ShopifyRequestAuthenticator : IShopifyRequestAuthenticator
{
    private readonly IShopifySessionTokenValidator _validator;
    private readonly IShopifyTokenExchanger _exchanger;
    private readonly IShopifyAccessTokenStore _store;
    private readonly Microsoft.Extensions.Options.IOptionsMonitor<ShopifyOptions> _options;

    /// <summary>Creates an authenticator.</summary>
    public ShopifyRequestAuthenticator(
        IShopifySessionTokenValidator validator,
        IShopifyTokenExchanger exchanger,
        IShopifyAccessTokenStore store,
        Microsoft.Extensions.Options.IOptionsMonitor<ShopifyOptions> options)
    {
        _validator = validator;
        _exchanger = exchanger;
        _store = store;
        _options = options;
    }

    /// <inheritdoc />
    public async Task<ShopifyAuthenticationResult> AuthenticateAsync(
        string? sessionToken,
        string? expectedShopDomain = null,
        CancellationToken cancellationToken = default)
    {
        var validation = await _validator.ValidateAsync(sessionToken, expectedShopDomain, cancellationToken).ConfigureAwait(false);

        if (!validation.Succeeded)
        {
            return ShopifyAuthenticationResult.Fail(
                validation.Failure ?? ShopifySessionTokenValidationFailure.Invalid,
                validation.Reason ?? "Session token validation failed.");
        }

        var token = validation.SessionToken!;
        var options = _options.CurrentValue;

        if (!_store.TryGet(token.ShopDomain, token.UserId, options.AccessTokenKind, out _))
        {
            // Exchanging here rather than lazily means an endpoint that authenticates and then
            // fails to reach Shopify is distinguishable from one that was never authenticated.
            var accessToken = await _exchanger.ExchangeAsync(token, cancellationToken).ConfigureAwait(false);

            var lifetime = options.AccessTokenCacheDuration;
            if (accessToken.ExpiresAt is not null)
            {
                var untilExpiry = accessToken.ExpiresAt.Value - DateTimeOffset.UtcNow;
                if (untilExpiry < lifetime)
                {
                    lifetime = untilExpiry > TimeSpan.Zero ? untilExpiry : TimeSpan.Zero;
                }
            }

            if (lifetime > TimeSpan.Zero)
            {
                _store.Set(token.ShopDomain, token.UserId, options.AccessTokenKind, accessToken, lifetime);
            }
        }

        return ShopifyAuthenticationResult.Success(new ShopifyShopContext(token.ShopDomain, token.UserId));
    }
}
