using System.Collections.Concurrent;

namespace Shopify.Blazor;

/// <summary>Where exchanged Admin API access tokens are kept.</summary>
/// <remarks>
/// The default is an in-memory store, which is correct for a single-instance app and for
/// development. A multi-instance deployment needs a shared store (the database, or a distributed
/// cache); otherwise each instance exchanges its own token on first use, which works but churns
/// tokens and loses the benefit of the cache.
/// </remarks>
public interface IShopifyAccessTokenStore
{
    /// <summary>Returns a cached token, or <c>null</c>.</summary>
    bool TryGet(string shopDomain, string subject, ShopifyAccessTokenKind kind, out ShopifyAccessToken? token);

    /// <summary>Stores a token.</summary>
    void Set(string shopDomain, string subject, ShopifyAccessTokenKind kind, ShopifyAccessToken token, TimeSpan lifetime);

    /// <summary>Drops a token, for example after a 401 from the Admin API.</summary>
    void Remove(string shopDomain, string subject, ShopifyAccessTokenKind kind);
}

/// <summary>Single-process token store.</summary>
public sealed class InMemoryShopifyAccessTokenStore : IShopifyAccessTokenStore
{
    private readonly ConcurrentDictionary<string, Entry> _tokens = new(StringComparer.Ordinal);

    /// <inheritdoc />
    public bool TryGet(string shopDomain, string subject, ShopifyAccessTokenKind kind, out ShopifyAccessToken? token)
    {
        var key = Key(shopDomain, subject, kind);

        if (_tokens.TryGetValue(key, out var entry))
        {
            if (entry.ExpiresAt > DateTimeOffset.UtcNow && !entry.Token.IsExpired)
            {
                token = entry.Token;
                return true;
            }

            _tokens.TryRemove(key, out _);
        }

        token = null;
        return false;
    }

    /// <inheritdoc />
    public void Set(string shopDomain, string subject, ShopifyAccessTokenKind kind, ShopifyAccessToken token, TimeSpan lifetime)
    {
        var expiresAt = DateTimeOffset.UtcNow.Add(lifetime);

        if (token.ExpiresAt is not null && token.ExpiresAt < expiresAt)
        {
            expiresAt = token.ExpiresAt.Value;
        }

        _tokens[Key(shopDomain, subject, kind)] = new Entry(token, expiresAt);
    }

    /// <inheritdoc />
    public void Remove(string shopDomain, string subject, ShopifyAccessTokenKind kind)
        => _tokens.TryRemove(Key(shopDomain, subject, kind), out _);

    private static string Key(string shopDomain, string subject, ShopifyAccessTokenKind kind)
        => kind == ShopifyAccessTokenKind.Offline
            ? $"{shopDomain}|offline"
            : $"{shopDomain}|online|{subject}";

    private readonly record struct Entry(ShopifyAccessToken Token, DateTimeOffset ExpiresAt);
}
