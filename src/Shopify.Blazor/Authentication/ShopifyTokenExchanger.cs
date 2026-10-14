using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shopify.Blazor.Internal;

namespace Shopify.Blazor.Authentication;

/// <summary>
/// Raised when Shopify refuses a token exchange or answers it with something unusable.
/// </summary>
/// <remarks>
/// The message never contains the session token or the API secret, so it is safe to log; it may
/// still contain Shopify's own error text, so do not return it verbatim to a browser.
/// </remarks>
public sealed class ShopifyTokenExchangeException : Exception
{
    /// <summary>Creates the exception.</summary>
    public ShopifyTokenExchangeException(string message, HttpStatusCode? statusCode = null, string? errorCode = null, Exception? inner = null)
        : base(message, inner)
    {
        StatusCode = statusCode;
        ErrorCode = errorCode;
    }

    /// <summary>HTTP status returned by Shopify, when a response was received.</summary>
    public HttpStatusCode? StatusCode { get; }

    /// <summary>Shopify's OAuth error code, for example <c>invalid_subject_token</c>.</summary>
    public string? ErrorCode { get; }
}

/// <summary>Exchanges a verified session token for an Admin API access token.</summary>
public interface IShopifyTokenExchanger
{
    /// <summary>
    /// Performs the token exchange, reusing a cached access token while it is valid.
    /// </summary>
    /// <exception cref="ShopifyTokenExchangeException">Shopify refused the exchange.</exception>
    Task<ShopifyAccessToken> ExchangeAsync(ShopifySessionToken sessionToken, CancellationToken cancellationToken = default);

    /// <summary>
    /// Drops the cached access token for a shop, so the next exchange asks Shopify for a new one.
    /// </summary>
    /// <remarks>
    /// Call this when Shopify rejects a token. An app-secret rotation revokes every issued token,
    /// and the only way to recover without a restart is to stop reusing the dead one.
    /// </remarks>
    void Invalidate(string shopDomain, string subject);
}

/// <summary>
/// Implements Shopify's token exchange grant for embedded apps.
/// </summary>
/// <remarks>
/// <para>
/// The exchange is the second half of the handshake. <see cref="ShopifySessionTokenValidator"/>
/// answers "is this really Shopify, for this shop?"; this answers "what may this app do on that
/// shop's behalf?". The result is an Admin API access token, which is a server-side secret and
/// is never rendered to the browser.
/// </para>
/// <para>
/// Shopify rotates the access token on every exchange, and a session token lives for about a
/// minute, so the token is cached for <see cref="ShopifyOptions.AccessTokenCacheDuration"/>
/// rather than exchanged per request.
/// </para>
/// </remarks>
public sealed class ShopifyTokenExchanger : IShopifyTokenExchanger
{
    // RFC 8693 token exchange identifiers, which Shopify uses verbatim.
    private const string TokenExchangeGrantType = "urn:ietf:params:oauth:grant-type:token-exchange";
    private const string IdTokenTokenType = "urn:ietf:params:oauth:token-type:id_token";
    private const string OnlineAccessTokenType = "urn:shopify:params:oauth:token-type:online-access-token";
    private const string OfflineAccessTokenType = "urn:shopify:params:oauth:token-type:offline-access-token";

    private static readonly TimeSpan DefaultCacheDuration = TimeSpan.FromMinutes(50);

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IMemoryCache _cache;
    private readonly IOptionsMonitor<ShopifyOptions> _options;
    private readonly ILogger<ShopifyTokenExchanger> _logger;

    /// <summary>Creates an exchanger.</summary>
    public ShopifyTokenExchanger(
        IHttpClientFactory httpClientFactory,
        IMemoryCache cache,
        IOptionsMonitor<ShopifyOptions> options,
        ILogger<ShopifyTokenExchanger> logger)
    {
        _httpClientFactory = httpClientFactory;
        _cache = cache;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ShopifyAccessToken> ExchangeAsync(ShopifySessionToken sessionToken, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(sessionToken);

        var options = _options.CurrentValue;
        options.Validate();

        var shop = ShopDomain.Normalize(sessionToken.ShopDomain);
        var cacheKey = CacheKey(shop, sessionToken.UserId, options.AccessTokenKind);

        if (_cache.TryGetValue(cacheKey, out ShopifyAccessToken? cached) && cached is not null && !cached.IsExpired)
        {
            return cached;
        }

        var token = await RequestTokenAsync(shop, sessionToken.RawToken, options, cancellationToken).ConfigureAwait(false);

        // Never outlive the token itself. A response that reports the token as already expired
        // (Shopify sends `expires_in: 0` for some grants) must not be cached at all, or every
        // later request would present a token that is dead on arrival.
        var cacheDuration = options.AccessTokenCacheDuration;

        if (token.ExpiresAt is not null)
        {
            var untilExpiry = token.ExpiresAt.Value - DateTimeOffset.UtcNow;

            if (untilExpiry <= TimeSpan.Zero)
            {
                _logger.LogWarning(
                    "Shopify returned an already-expired access token for {Shop}; it will not be cached.",
                    shop);

                return token;
            }

            if (untilExpiry < cacheDuration)
            {
                cacheDuration = untilExpiry;
            }
        }

        if (cacheDuration > TimeSpan.Zero)
        {
            _cache.Set(cacheKey, token, cacheDuration);
        }

        return token;
    }

    /// <inheritdoc />
    public void Invalidate(string shopDomain, string subject)
    {
        var shop = ShopDomain.TryNormalize(shopDomain);

        if (shop is null)
        {
            return;
        }

        // Both kinds, because the caller reports a rejected token without necessarily knowing
        // which kind it was.
        _cache.Remove(CacheKey(shop, subject, ShopifyAccessTokenKind.Online));
        _cache.Remove(CacheKey(shop, subject, ShopifyAccessTokenKind.Offline));
    }

    /// <summary>
    /// The cache key for one shop and one merchant.
    /// </summary>
    /// <remarks>
    /// Online tokens are per-user, so the subject is part of the key: two merchants on one shop
    /// have different access tokens, and sharing an entry would grant one merchant the other's
    /// session.
    /// </remarks>
    private static string CacheKey(string shop, string subject, ShopifyAccessTokenKind kind)
        => kind == ShopifyAccessTokenKind.Offline
            ? $"shopify:token:{shop}:offline"
            : $"shopify:token:{shop}:online:{subject}";

    private async Task<ShopifyAccessToken> RequestTokenAsync(
        string shop,
        string sessionToken,
        ShopifyOptions options,
        CancellationToken cancellationToken)
    {
        var endpoint = $"https://{shop}/admin/oauth/access_token";

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["client_id"] = options.ApiKey,
                ["client_secret"] = options.ApiSecret,
                ["grant_type"] = TokenExchangeGrantType,
                ["subject_token"] = sessionToken,
                ["subject_token_type"] = IdTokenTokenType,
                ["requested_token_type"] = options.AccessTokenKind == ShopifyAccessTokenKind.Offline
                    ? OfflineAccessTokenType
                    : OnlineAccessTokenType,
            }),
        };

        var client = _httpClientFactory.CreateClient(ShopifyHttpClients.TokenExchange);

        HttpResponseMessage response;
        try
        {
            response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            throw new ShopifyTokenExchangeException($"The token exchange request to {shop} failed before a response was received.", inner: ex);
        }

        using (response)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                var error = TryReadString(body, "error");
                _logger.LogWarning(
                    "Token exchange for {Shop} was refused with {Status} ({Error}).",
                    shop,
                    (int)response.StatusCode,
                    error ?? "no error code");

                throw new ShopifyTokenExchangeException(
                    $"Shopify refused the token exchange for {shop}: HTTP {(int)response.StatusCode}{(error is null ? string.Empty : $" ({error})")}.",
                    response.StatusCode,
                    error);
            }

            TokenExchangeResponse? payload;
            try
            {
                payload = JsonSerializer.Deserialize(body, TokenExchangeJsonContext.Default.TokenExchangeResponse);
            }
            catch (JsonException ex)
            {
                throw new ShopifyTokenExchangeException("The token exchange response was not valid JSON.", response.StatusCode, inner: ex);
            }

            if (payload?.AccessToken is null || string.IsNullOrWhiteSpace(payload.AccessToken))
            {
                throw new ShopifyTokenExchangeException(
                    "The token exchange response contained no access token.",
                    response.StatusCode,
                    payload?.Error);
            }

            var scopes = payload.Scope?
                .Split([',', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                ?? [];

            DateTimeOffset? expiresAt = null;
            if (payload.ExpiresIn is > 0)
            {
                expiresAt = DateTimeOffset.UtcNow.AddSeconds(payload.ExpiresIn.Value);
            }

            _logger.LogDebug(
                "Exchanged a session token for a {Kind} Admin API access token on {Shop} granting {ScopeCount} scope(s).",
                options.AccessTokenKind,
                shop,
                scopes.Length);

            return new ShopifyAccessToken(shop, payload.AccessToken, scopes, expiresAt);
        }
    }

    private static string? TryReadString(string body, string property)
    {
        try
        {
            using var document = JsonDocument.Parse(body);
            return document.RootElement.ValueKind == JsonValueKind.Object
                && document.RootElement.TryGetProperty(property, out var value)
                && value.ValueKind == JsonValueKind.String
                    ? value.GetString()
                    : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    internal sealed class TokenExchangeResponse
    {
        [JsonPropertyName("access_token")]
        public string? AccessToken { get; set; }

        [JsonPropertyName("scope")]
        public string? Scope { get; set; }

        [JsonPropertyName("expires_in")]
        public int? ExpiresIn { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }

        [JsonPropertyName("error_description")]
        public string? ErrorDescription { get; set; }
    }
}

/// <summary>
/// Source-generated (de)serialization for the token exchange response.
/// </summary>
/// <remarks>
/// The naming policy is stated explicitly. Without it the generated context uses the property
/// names verbatim, so Shopify's <c>expires_in</c> silently fails to bind to <c>ExpiresIn</c> and
/// every token looks non-expiring — which defeats the cache-lifetime logic without any error.
/// </remarks>
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.SnakeCaseLower)]
[JsonSerializable(typeof(ShopifyTokenExchanger.TokenExchangeResponse))]
internal sealed partial class TokenExchangeJsonContext : JsonSerializerContext;
