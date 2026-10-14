using Microsoft.Extensions.DependencyInjection;
using Shopify.Blazor.Authentication;
using Xunit;

namespace Shopify.Blazor.Tests;

/// <summary>
/// Tests for App Bridge session token validation.
/// </summary>
/// <remarks>
/// This is the security boundary of the whole toolkit. Every claim in a session token is believed
/// only after this passes, and the shop domain it yields is then interpolated into the outbound
/// URL that carries the API secret — so the negative cases here matter more than the positive one.
/// </remarks>
public sealed class SessionTokenValidationTests
{
    private const string Shop = SessionTokenFactory.DefaultShop;

    /// <summary>
    /// Runs the real validator. No HTTP handler is registered, on purpose: session tokens are
    /// HMAC-signed with the app's API secret, so verification is entirely local. If the validator
    /// ever tried to fetch a key set again, these tests would fail on the unstubbed request rather
    /// than silently passing against a fake one.
    /// </summary>
    private static async Task<(ShopifySessionTokenValidationResult Result, ScriptedHttpMessageHandler Handler)> ValidateAsync(
        string token,
        string? expectedShop = null,
        Action<ShopifyOptions>? configure = null)
    {
        var handler = new ScriptedHttpMessageHandler();

        await using var provider = TestServices.Create(handler, configure);
        var validator = provider.GetRequiredService<IShopifySessionTokenValidator>();

        var result = await validator.ValidateAsync(token, expectedShop);
        return (result, handler);
    }

    [Fact]
    public async Task A_correctly_signed_token_is_accepted()
    {
        var token = SessionTokenFactory.Create(scopes: "read_products,write_products");

        var (result, _) = await ValidateAsync(token);

        Assert.True(result.Succeeded, result.Reason);
        Assert.Equal(Shop, result.SessionToken!.ShopDomain);
        Assert.Equal("1234567890", result.SessionToken.UserId);
        Assert.Equal(["read_products", "write_products"], result.SessionToken.Scopes);
    }

    [Fact]
    public async Task The_validated_shop_domain_comes_from_the_dest_claim()
    {
        // Not from a query string, a header, or anything else the caller controls.
        const string otherShop = "some-other-shop.myshopify.com";

        var (result, _) = await ValidateAsync(SessionTokenFactory.Create(shop: otherShop));

        Assert.True(result.Succeeded, result.Reason);
        Assert.Equal(otherShop, result.SessionToken!.ShopDomain);
    }

    [Fact]
    public async Task A_token_signed_by_a_different_key_is_rejected()
    {
        // Same claims, wrong secret: the forged-token attempt this whole path exists to stop.
        var (result, _) = await ValidateAsync(SessionTokenFactory.CreateForgedToken());

        Assert.False(result.Succeeded);
        Assert.Equal(ShopifySessionTokenValidationFailure.Invalid, result.Failure);
    }

    [Fact]
    public async Task An_unsigned_token_with_alg_none_is_rejected()
    {
        // The classic downgrade: strip the signature and claim no algorithm. Verification must not
        // be talked into skipping the signature check, so the accepted algorithm is pinned.
        var (result, _) = await ValidateAsync(SessionTokenFactory.CreateUnsignedToken());

        Assert.False(result.Succeeded);
        Assert.Equal(ShopifySessionTokenValidationFailure.Invalid, result.Failure);
    }

    [Fact]
    public async Task A_token_signed_with_a_different_algorithm_is_rejected()
    {
        // Correctly formed and correctly addressed, but RS256. Session tokens are HS256, so this
        // cannot be verified with the API secret and must be refused rather than waved through.
        var (result, _) = await ValidateAsync(SessionTokenFactory.Create(
            algorithm: Microsoft.IdentityModel.Tokens.SecurityAlgorithms.RsaSha256));

        Assert.False(result.Succeeded);
        Assert.Equal(ShopifySessionTokenValidationFailure.Invalid, result.Failure);
    }

    [Fact]
    public async Task A_short_api_secret_is_reported_as_a_configuration_problem()
    {
        // HMAC-SHA256 needs 128 bits. A secret that is too short would otherwise fail every token
        // with a message about key size, which reads like a token problem and is not one.
        var (result, _) = await ValidateAsync(
            SessionTokenFactory.Create(),
            configure: options => options.ApiSecret = "too-short");

        Assert.False(result.Succeeded);
        Assert.Contains("shorter than", result.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task A_token_for_a_different_app_is_rejected()
    {
        var (result, _) = await ValidateAsync(SessionTokenFactory.Create(audience: "someone-elses-api-key"));

        // The audience is this app's API key. A token minted for another app must not be usable
        // here, or any app could act on this one's shop.
        Assert.False(result.Succeeded);
        Assert.Equal(ShopifySessionTokenValidationFailure.Invalid, result.Failure);
    }

    [Fact]
    public async Task An_expired_token_is_rejected()
    {
        var (result, _) = await ValidateAsync(SessionTokenFactory.Create(expiresIn: TimeSpan.FromMinutes(-5)));

        Assert.False(result.Succeeded);
        Assert.Equal(ShopifySessionTokenValidationFailure.Expired, result.Failure);
    }

    [Fact]
    public async Task A_token_for_a_different_shop_than_the_one_addressed_is_rejected()
    {
        var (result, _) = await ValidateAsync(
            SessionTokenFactory.Create(shop: "actual-shop.myshopify.com"),
            expectedShop: "expected-shop.myshopify.com");

        Assert.False(result.Succeeded);
        Assert.Equal(ShopifySessionTokenValidationFailure.WrongShop, result.Failure);
    }

    [Fact]
    public async Task A_missing_token_reports_missing_rather_than_invalid()
    {
        var (result, _) = await ValidateAsync(string.Empty);

        Assert.False(result.Succeeded);
        Assert.Equal(ShopifySessionTokenValidationFailure.Missing, result.Failure);
    }

    [Fact]
    public async Task Nonsense_is_rejected_without_fetching_any_keys()
    {
        var (result, handler) = await ValidateAsync("not-a-jwt");

        Assert.False(result.Succeeded);
        Assert.Empty(handler.Requests);
    }

    [Theory]
    [InlineData("https://evil.example.com")]
    [InlineData("https://shop.myshopify.com.evil.example.com")]
    [InlineData("https://../evil")]
    [InlineData("https://not a domain")]
    [InlineData("http://test-shop.myshopify.com@evil.example.com")]
    public async Task A_dest_claim_that_is_not_a_myshopify_domain_is_rejected(string destination)
    {
        // The shop domain is interpolated into https://{shop}/admin/oauth/access_token, a request
        // that carries the app secret. A dest that is not a myshopify host must never get that far,
        // so the check has to happen before the outbound call rather than as a result of it.
        // Correctly signed, so only the domain check can be what rejects it.
        var (result, handler) = await ValidateAsync(SessionTokenFactory.CreateHostileDestToken(destination));

        Assert.False(result.Succeeded);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Keys_are_fetched_once_and_reused()
    {
        var handler = new ScriptedHttpMessageHandler();
        await using var provider = TestServices.Create(handler);
        var validator = provider.GetRequiredService<IShopifySessionTokenValidator>();

        await validator.ValidateAsync(SessionTokenFactory.Create());
        await validator.ValidateAsync(SessionTokenFactory.Create());
        await validator.ValidateAsync(SessionTokenFactory.Create());

        // Verification is local and symmetric, so it makes no network call at all. That matters:
        // a password-protected storefront cannot obstruct something that never asks the store.
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task An_api_key_that_is_not_configured_rejects_every_token()
    {
        // With no API key there is no audience to check, so nothing can be trusted.
        var (result, handler) = await ValidateAsync(
            SessionTokenFactory.Create(),
            configure: options => options.ApiKey = string.Empty);

        Assert.False(result.Succeeded);
        Assert.Empty(handler.Requests);
    }
}
