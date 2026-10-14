using System.Net;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shopify.Blazor.AdminApi;
using Shopify.Blazor.Authentication;

namespace Shopify.Blazor;

/// <summary>
/// Raised when an Admin API request cannot be completed: authentication failed, the access token
/// was rejected, or GraphQL returned errors.
/// </summary>
public sealed class ShopifyAdminApiException : Exception
{
    /// <summary>Creates the exception.</summary>
    public ShopifyAdminApiException(string message, string? shopDomain = null, AdminGraphQlResponse? response = null, Exception? inner = null)
        : base(message, inner)
    {
        ShopDomain = shopDomain;
        Response = response;
    }

    /// <summary>The shop the call was made against, when known.</summary>
    public string? ShopDomain { get; }

    /// <summary>The raw GraphQL response, when one was received.</summary>
    public AdminGraphQlResponse? Response { get; }

    /// <summary>True when the token was rejected, so the caller may retry after a fresh exchange.</summary>
    public bool IsTokenRejected => Response?.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden;
}

/// <summary>
/// The convenience layer most application code should use: pass the App Bridge session token and
/// a GraphQL document, get data back.
/// </summary>
/// <remarks>
/// Every call re-validates the session token and obtains an access token (from the cache while it
/// is warm), so a service never holds a long-lived credential and a long-running Blazor circuit
/// cannot outlive its authorisation.
/// </remarks>
public interface IShopifyAdminApi
{
    /// <summary>Runs a GraphQL query against the shop that issued <paramref name="sessionToken"/>.</summary>
    /// <exception cref="ShopifyAdminApiException">Authentication failed or the query returned errors.</exception>
    Task<AdminGraphQlResponse> QueryAsync(
        string sessionToken,
        string query,
        object? variables = null,
        CancellationToken cancellationToken = default);

    /// <summary>Runs a GraphQL query and deserializes <c>data</c> into <typeparamref name="T"/>.</summary>
    /// <exception cref="ShopifyAdminApiException">Authentication failed or the query returned errors.</exception>
    Task<T> QueryAsync<T>(
        string sessionToken,
        string query,
        object? variables = null,
        CancellationToken cancellationToken = default);
}

/// <summary>Default <see cref="IShopifyAdminApi"/>.</summary>
public sealed class ShopifyAdminApi : IShopifyAdminApi
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);

    private readonly IShopifyRequestAuthenticator _authenticator;
    private readonly IShopifyTokenExchanger _exchanger;
    private readonly IShopifyAccessTokenStore _store;
    private readonly IAdminGraphQlClient _client;
    private readonly IOptionsMonitor<ShopifyOptions> _options;
    private readonly ILogger<ShopifyAdminApi> _logger;

    /// <summary>Creates the facade.</summary>
    public ShopifyAdminApi(
        IShopifyRequestAuthenticator authenticator,
        IShopifyTokenExchanger exchanger,
        IShopifyAccessTokenStore store,
        IAdminGraphQlClient client,
        IOptionsMonitor<ShopifyOptions> options,
        ILogger<ShopifyAdminApi> logger)
    {
        _authenticator = authenticator;
        _exchanger = exchanger;
        _store = store;
        _client = client;
        _options = options;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<AdminGraphQlResponse> QueryAsync(
        string sessionToken,
        string query,
        object? variables = null,
        CancellationToken cancellationToken = default)
    {
        var (context, accessToken) = await AuthenticateAsync(sessionToken, cancellationToken).ConfigureAwait(false);

        var response = await _client
            .ExecuteAsync(query, variables, accessToken, context.ShopDomain, cancellationToken)
            .ConfigureAwait(false);

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            // A revoked token (an app-secret rotation does this) or a missing scope. Discard it in
            // both caches — the store this facade reads, and the exchanger's own cache — or the
            // next attempt would present the same dead token for ever. Then surface the failure.
            _store.Remove(context.ShopDomain, context.UserId, _options.CurrentValue.AccessTokenKind);
            _exchanger.Invalidate(context.ShopDomain, context.UserId);

            throw new ShopifyAdminApiException(
                $"Shopify rejected the Admin API access token for {context.ShopDomain}. " +
                "The cached token has been discarded; retry the request.",
                context.ShopDomain,
                response);
        }

        if (!response.IsSuccess)
        {
            throw new ShopifyAdminApiException(response.DescribeFailure(), context.ShopDomain, response);
        }

        return response;
    }

    /// <inheritdoc />
    public async Task<T> QueryAsync<T>(
        string sessionToken,
        string query,
        object? variables = null,
        CancellationToken cancellationToken = default)
    {
        var response = await QueryAsync(sessionToken, query, variables, cancellationToken).ConfigureAwait(false);
        return response.Deserialize<T>(SerializerOptions);
    }

    private async Task<(ShopifyShopContext Context, ShopifyAccessToken Token)> AuthenticateAsync(
        string sessionToken,
        CancellationToken cancellationToken)
    {
        var result = await _authenticator.AuthenticateAsync(sessionToken, null, cancellationToken).ConfigureAwait(false);

        if (!result.Succeeded)
        {
            _logger.LogWarning("Admin API call rejected: {Reason}", result.Reason);
            throw new ShopifyAdminApiException(result.Reason ?? "Authentication failed.");
        }

        var context = result.Context!;
        var kind = _options.CurrentValue.AccessTokenKind;

        if (!_store.TryGet(context.ShopDomain, context.UserId, kind, out var token) || token is null)
        {
            throw new ShopifyAdminApiException(
                "Authentication succeeded but no access token was available.",
                context.ShopDomain);
        }

        return (context, token);
    }
}
