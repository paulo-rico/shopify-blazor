namespace Shopify.Blazor;

/// <summary>
/// An Admin API access token scoped to one shop.
/// </summary>
/// <param name="ShopDomain">The <c>*.myshopify.com</c> domain the token belongs to.</param>
/// <param name="AccessToken">The bearer token to send to the Admin API.</param>
/// <param name="Scopes">The scopes the token actually grants.</param>
/// <param name="ExpiresAt">
/// When the token expires, or <c>null</c> when it does not (offline tokens).
/// </param>
public sealed record ShopifyAccessToken(
    string ShopDomain,
    string AccessToken,
    IReadOnlyList<string> Scopes,
    DateTimeOffset? ExpiresAt)
{
    /// <summary>True when <see cref="ExpiresAt"/> has passed.</summary>
    public bool IsExpired => ExpiresAt is not null && ExpiresAt <= DateTimeOffset.UtcNow;
}
