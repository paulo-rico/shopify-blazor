namespace Shopify.Blazor;

/// <summary>
/// Configuration for a Shopify embedded application.
/// </summary>
/// <remarks>
/// <see cref="ApiKey"/> is the only value that is safe to expose to the browser: it is written
/// into the <c>shopify-api-key</c> meta tag so App Bridge can identify the app. Everything else
/// here is server-side only and must never be rendered into a page.
/// </remarks>
public sealed class ShopifyOptions
{
    /// <summary>Configuration section name used by <c>IConfiguration.GetSection</c>.</summary>
    public const string SectionName = "Shopify";

    /// <summary>
    /// The app's public API key (called "client ID" by Shopify's OAuth documentation).
    /// Sent to the browser in the <c>shopify-api-key</c> meta tag.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// The Admin API version to request, for example <c>2026-10</c>.
    /// </summary>
    /// <remarks>
    /// Shopify releases a version each quarter and retires each one after about twelve months.
    /// A request for a retired version does <strong>not</strong> fail: Shopify falls forward to
    /// the oldest accessible stable release and answers as usual, so behaviour changes with
    /// nothing but an <c>X-Shopify-API-Version</c> response header to show for it. Check the
    /// table at https://shopify.dev/docs/api/usage/versioning when upgrading.
    /// </remarks>
    public string AdminApiVersion { get; set; } = DefaultAdminApiVersion;

    /// <summary>The Admin API version this build was tested against.</summary>
    public const string DefaultAdminApiVersion = "2026-10";

    /// <summary>
    /// The app's API secret key. Used as the OAuth <c>client_secret</c> when exchanging a
    /// session token for an Admin API access token. Server-side only.
    /// </summary>
    public string ApiSecret { get; set; } = string.Empty;

    /// <summary>
    /// The public HTTPS URL the app is served from (for example an ngrok hostname in
    /// development). Used for diagnostics and for building redirect URLs.
    /// </summary>
    public string? AppUrl { get; set; }

    /// <summary>
    /// Access scopes requested at install time, space or comma separated, for example
    /// <c>read_products,write_products</c>. Documentation only: the authoritative list lives in
    /// the Partner Dashboard and in <c>shopify.app.toml</c>.
    /// </summary>
    public string? Scopes { get; set; }

    /// <summary>
    /// Which kind of access token a session token is exchanged for.
    /// </summary>
    public ShopifyAccessTokenKind AccessTokenKind { get; set; } = ShopifyAccessTokenKind.Online;

    /// <summary>
    /// Overrides the public key endpoint used to validate session token signatures.
    /// Leave empty in production so <c>https://{shop}/.well-known/jwks.json</c> is used.
    /// Set it in tests to point at a local key set.
    /// </summary>
    public string? JwksUrlOverride { get; set; }

    /// <summary>
    /// How long an exchanged access token is cached before it is exchanged again. Shopify
    /// rotates access tokens on every exchange, so this is a cache-lifetime setting, not a
    /// token lifetime.
    /// </summary>
    public TimeSpan AccessTokenCacheDuration { get; set; } = TimeSpan.FromMinutes(50);

    /// <summary>
    /// Clock skew tolerated when validating session tokens.
    /// </summary>
    public TimeSpan ClockSkew { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// When true, a session token is still validated but a shop domain that cannot be
    /// validated is rejected. Normally, and intentionally, <c>true</c>.
    /// </summary>
    public bool RequireSessionToken { get; set; } = true;

    /// <summary>
    /// Throws when the configuration is unusable, so the failure happens at startup rather than
    /// on the first request. <paramref name="requireSecret"/> is false for Blazor WebAssembly
    /// clients, which only ever need the API key.
    /// </summary>
    public void Validate(bool requireSecret = true)
    {
        if (string.IsNullOrWhiteSpace(ApiKey))
        {
            throw new InvalidOperationException(
                "Shopify:ApiKey is not configured. Set it from the Shopify Partner Dashboard, " +
                "or with the SHOPIFY_API_KEY environment variable.");
        }

        if (requireSecret && string.IsNullOrWhiteSpace(ApiSecret))
        {
            throw new InvalidOperationException(
                "Shopify:ApiSecret is not configured. Set it from the Shopify Partner Dashboard, " +
                "or with the SHOPIFY_API_SECRET environment variable. Never commit it.");
        }
    }
}

/// <summary>Kind of Admin API access token to request during token exchange.</summary>
public enum ShopifyAccessTokenKind
{
    /// <summary>
    /// A per-user token, valid only while the merchant who granted it is signed in. The
    /// default, and the safer choice for embedded admin apps.
    /// </summary>
    Online,

    /// <summary>
    /// A store-scoped token that outlives the merchant's session. Required for background
    /// work such as webhook processing.
    /// </summary>
    Offline,
}
