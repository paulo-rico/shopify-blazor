using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Shopify.Blazor.Authentication;
using Xunit;

namespace Shopify.Blazor.Tests;

/// <summary>
/// Tests for the token exchange grant: turning a verified session token into an Admin API access
/// token.
/// </summary>
public sealed class TokenExchangeTests
{
    private const string Shop = SessionTokenFactory.DefaultShop;

    private static string SuccessBody(string token = "shpat_test_access_token", int? expiresIn = 3600, string scope = "read_products,write_products")
    {
        var expiry = expiresIn is null ? string.Empty : $",\n  \"expires_in\": {expiresIn}";

        return $"{{\n  \"access_token\": \"{token}\",\n  \"scope\": \"{scope}\"{expiry}\n}}";
    }

    private static async Task<(ShopifyAccessToken? Token, ShopifyTokenExchangeException? Error, ScriptedHttpMessageHandler Handler, ServiceProvider Provider)> ExchangeAsync(
        ScriptedResponse response,
        Action<ShopifyOptions>? configure = null)
    {
        var handler = new ScriptedHttpMessageHandler()
            .OnPath("/admin/oauth/access_token", response);

        var provider = TestServices.Create(handler, configure);
        var validator = provider.GetRequiredService<IShopifySessionTokenValidator>();
        var exchanger = provider.GetRequiredService<IShopifyTokenExchanger>();

        var validation = await validator.ValidateAsync(SessionTokenFactory.Create());
        Assert.True(validation.Succeeded, validation.Reason);

        try
        {
            var token = await exchanger.ExchangeAsync(validation.SessionToken!);
            return (token, null, handler, provider);
        }
        catch (ShopifyTokenExchangeException ex)
        {
            return (null, ex, handler, provider);
        }
    }

    [Fact]
    public async Task A_successful_exchange_returns_the_access_token_and_its_scopes()
    {
        var (token, error, _, provider) = await ExchangeAsync(ScriptedResponse.Json(SuccessBody()));
        await using var _ = provider;

        Assert.Null(error);
        Assert.NotNull(token);
        Assert.Equal("shpat_test_access_token", token!.AccessToken);
        Assert.Equal(Shop, token.ShopDomain);
        Assert.Equal(["read_products", "write_products"], token.Scopes);
        Assert.NotNull(token.ExpiresAt);
    }

    [Fact]
    public async Task The_exchange_posts_the_rfc8693_parameters_shopify_expects()
    {
        var (_, _, handler, provider) = await ExchangeAsync(ScriptedResponse.Json(SuccessBody()));
        await using var _ = provider;

        var request = Assert.Single(handler.TokenExchangeRequests);

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal($"/admin/oauth/access_token", request.Uri.AbsolutePath);

        var body = request.Body!;
        Assert.Contains("grant_type=urn%3Aietf%3Aparams%3Aoauth%3Agrant-type%3Atoken-exchange", body, StringComparison.Ordinal);
        Assert.Contains("subject_token_type=urn%3Aietf%3Aparams%3Aoauth%3Atoken-type%3Aid_token", body, StringComparison.Ordinal);
        Assert.Contains("requested_token_type=urn%3Ashopify%3Aparams%3Aoauth%3Atoken-type%3Aonline-access-token", body, StringComparison.Ordinal);
        Assert.Contains("client_id=test-api-key", body, StringComparison.Ordinal);

        // The secret must travel in the body of a request to the shop's own host, never in a URL,
        // a header that could be logged, or anything the browser can see.
        Assert.Contains($"client_secret={SessionTokenFactory.ApiSecret}", body, StringComparison.Ordinal);
        Assert.EndsWith(".myshopify.com", request.Uri.Host, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_offline_exchange_asks_for_an_offline_token()
    {
        var (_, _, handler, provider) = await ExchangeAsync(
            ScriptedResponse.Json(SuccessBody()),
            options => options.AccessTokenKind = ShopifyAccessTokenKind.Offline);
        await using var _ = provider;

        Assert.Contains(
            "requested_token_type=urn%3Ashopify%3Aparams%3Aoauth%3Atoken-type%3Aoffline-access-token",
            Assert.Single(handler.TokenExchangeRequests).Body!,
            StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_cached_token_is_reused_until_it_is_near_expiry()
    {
        var handler = new ScriptedHttpMessageHandler()
            .OnPath("/admin/oauth/access_token", ScriptedResponse.Json(SuccessBody(expiresIn: 3600)));

        await using var provider = TestServices.Create(handler);
        var validator = provider.GetRequiredService<IShopifySessionTokenValidator>();
        var exchanger = provider.GetRequiredService<IShopifyTokenExchanger>();

        var session = (await validator.ValidateAsync(SessionTokenFactory.Create())).SessionToken!;

        await exchanger.ExchangeAsync(session);
        await exchanger.ExchangeAsync(session);
        await exchanger.ExchangeAsync(session);

        // Shopify rotates the access token on every exchange and a session token lives about a
        // minute, so exchanging per request would churn tokens for no benefit.
        Assert.Single(handler.TokenExchangeRequests);
    }

    [Fact]
    public async Task Expires_in_is_read_from_shopifys_snake_case_response()
    {
        // Shopify answers with snake_case keys. If `expires_in` fails to bind, every token looks
        // non-expiring and the cache-lifetime rule quietly stops working — with nothing in the log
        // to say so. This asserts the value actually arrives.
        var (token, error, _, provider) = await ExchangeAsync(ScriptedResponse.Json(SuccessBody(expiresIn: 900)));
        await using var _ = provider;

        Assert.Null(error);
        Assert.NotNull(token!.ExpiresAt);

        var remaining = token.ExpiresAt!.Value - DateTimeOffset.UtcNow;
        Assert.InRange(remaining, TimeSpan.FromMinutes(14), TimeSpan.FromMinutes(15));
    }

    [Fact]
    public async Task A_non_expiring_token_is_cached()
    {
        // `expires_in: 0` means the token does not expire, not that it has already expired, so it
        // is cached for the configured duration rather than exchanged again.
        var handler = new ScriptedHttpMessageHandler()
            .OnPath(
                "/admin/oauth/access_token",
                ScriptedResponse.Json(SuccessBody(expiresIn: 0)),
                ScriptedResponse.Json(SuccessBody(token: "second")));

        await using var provider = TestServices.Create(handler);
        var validator = provider.GetRequiredService<IShopifySessionTokenValidator>();
        var exchanger = provider.GetRequiredService<IShopifyTokenExchanger>();

        var session = (await validator.ValidateAsync(SessionTokenFactory.Create())).SessionToken!;

        var first = await exchanger.ExchangeAsync(session);
        var second = await exchanger.ExchangeAsync(session);

        Assert.Null(first.ExpiresAt);
        Assert.Equal("shpat_test_access_token", first.AccessToken);
        Assert.Equal("shpat_test_access_token", second.AccessToken);
        Assert.Single(handler.TokenExchangeRequests);
    }

    [Fact]
    public async Task Two_merchants_on_one_shop_do_not_share_an_online_token()
    {
        var handler = new ScriptedHttpMessageHandler()
            .OnPath(
                "/admin/oauth/access_token",
                ScriptedResponse.Json(SuccessBody(token: "merchant-one")),
                ScriptedResponse.Json(SuccessBody(token: "merchant-two")));

        await using var provider = TestServices.Create(handler);
        var validator = provider.GetRequiredService<IShopifySessionTokenValidator>();
        var exchanger = provider.GetRequiredService<IShopifyTokenExchanger>();

        var first = (await validator.ValidateAsync(SessionTokenFactory.Create(userId: "111"))).SessionToken!;
        var second = (await validator.ValidateAsync(SessionTokenFactory.Create(userId: "222"))).SessionToken!;

        Assert.Equal("merchant-one", (await exchanger.ExchangeAsync(first)).AccessToken);
        Assert.Equal("merchant-two", (await exchanger.ExchangeAsync(second)).AccessToken);
        Assert.Equal(2, handler.TokenExchangeRequests.Count);
    }

    [Fact]
    public async Task A_refused_exchange_surfaces_shopifys_error_without_leaking_the_secret()
    {
        var (token, error, _, provider) = await ExchangeAsync(
            ScriptedResponse.Json("""{"error":"invalid_subject_token","error_description":"Token is invalid"}""", HttpStatusCode.BadRequest));
        await using var _ = provider;

        Assert.Null(token);
        Assert.NotNull(error);
        Assert.Equal("invalid_subject_token", error!.ErrorCode);
        Assert.Equal(HttpStatusCode.BadRequest, error.StatusCode);

        // The message is logged, so it must not carry the API secret or the session token.
        Assert.DoesNotContain("test-api-secret", error.Message, StringComparison.Ordinal);
        Assert.DoesNotContain("client_secret", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_response_with_no_access_token_is_treated_as_a_failure()
    {
        var (token, error, _, provider) = await ExchangeAsync(ScriptedResponse.Json("""{"scope":"read_products"}"""));
        await using var _ = provider;

        Assert.Null(token);
        Assert.NotNull(error);
        Assert.Contains("no access token", error!.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_non_json_response_is_treated_as_a_failure()
    {
        var (token, error, _, provider) = await ExchangeAsync(
            ScriptedResponse.Json("<html>gateway error</html>", HttpStatusCode.BadGateway));
        await using var _ = provider;

        Assert.Null(token);
        Assert.NotNull(error);
        Assert.Equal(HttpStatusCode.BadGateway, error!.StatusCode);
    }
}
