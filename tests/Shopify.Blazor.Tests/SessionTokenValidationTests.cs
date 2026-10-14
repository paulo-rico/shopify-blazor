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
    /// Runs the real validator against a real key set served over the scripted handler, so the
    /// whole path is exercised: read the unvalidated dest, fetch that shop's JWKS, verify the
    /// signature, audience, issuer and lifetime.
    /// </summary>
    private static async Task<(ShopifySessionTokenValidationResult Result, ScriptedHttpMessageHandler Handler)> ValidateAsync(
        string token,
        string? expectedShop = null,
        Action<ShopifyOptions>? configure = null,
        SessionTokenFactory? factory = null)
    {
        factory ??= new SessionTokenFactory();
        var handler = new ScriptedHttpMessageHandler()
            .OnHost(Shop, ScriptedResponse.Json(factory.BuildJwks()));

        await using var provider = TestServices.Create(handler, configure);
        var validator = provider.GetRequiredService<IShopifySessionTokenValidator>();

        var result = await validator.ValidateAsync(token, expectedShop);
        return (result, handler);
    }

    [Fact]
    public async Task A_correctly_signed_token_is_accepted()
    {
        using var factory = new SessionTokenFactory();
        var token = factory.Create(scopes: "read_products,write_products");

        var (result, _) = await ValidateAsync(token, factory: factory);

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

        using var factory = new SessionTokenFactory();
        var handler = new ScriptedHttpMessageHandler()
            .OnHost(otherShop, ScriptedResponse.Json(factory.BuildJwks()));

        await using var provider = TestServices.Create(handler);
        var validator = provider.GetRequiredService<IShopifySessionTokenValidator>();

        var result = await validator.ValidateAsync(factory.Create(shop: otherShop));

        Assert.True(result.Succeeded, result.Reason);
        Assert.Equal(otherShop, result.SessionToken!.ShopDomain);
    }

    [Fact]
    public async Task A_token_signed_by_a_different_key_is_rejected()
    {
        using var keyOwner = new SessionTokenFactory();
        var handler = new ScriptedHttpMessageHandler()
            .OnHost(Shop, ScriptedResponse.Json(keyOwner.BuildJwks()));

        await using var provider = TestServices.Create(handler);
        var validator = provider.GetRequiredService<IShopifySessionTokenValidator>();

        // Same claims, wrong signature: the forged-token attempt this whole path exists to stop.
        var result = await validator.ValidateAsync(SessionTokenFactory.CreateForgedToken());

        Assert.False(result.Succeeded);
        Assert.Equal(ShopifySessionTokenValidationFailure.Invalid, result.Failure);
    }

    [Fact]
    public async Task A_token_for_a_different_app_is_rejected()
    {
        using var factory = new SessionTokenFactory();

        var (result, _) = await ValidateAsync(factory.Create(audience: "someone-elses-api-key"), factory: factory);

        // The audience is this app's API key. A token minted for another app must not be usable
        // here, or any app could act on this one's shop.
        Assert.False(result.Succeeded);
        Assert.Equal(ShopifySessionTokenValidationFailure.Invalid, result.Failure);
    }

    [Fact]
    public async Task An_expired_token_is_rejected()
    {
        using var factory = new SessionTokenFactory();

        var (result, _) = await ValidateAsync(factory.Create(expiresIn: TimeSpan.FromMinutes(-5)), factory: factory);

        Assert.False(result.Succeeded);
        Assert.Equal(ShopifySessionTokenValidationFailure.Expired, result.Failure);
    }

    [Fact]
    public async Task A_token_for_a_different_shop_than_the_one_addressed_is_rejected()
    {
        using var factory = new SessionTokenFactory();
        var handler = new ScriptedHttpMessageHandler()
            .OnHost("actual-shop.myshopify.com", ScriptedResponse.Json(factory.BuildJwks()));

        await using var provider = TestServices.Create(handler);
        var validator = provider.GetRequiredService<IShopifySessionTokenValidator>();

        var result = await validator.ValidateAsync(
            factory.Create(shop: "actual-shop.myshopify.com"),
            expectedShopDomain: "expected-shop.myshopify.com");

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
        var handler = new ScriptedHttpMessageHandler();
        await using var provider = TestServices.Create(handler);
        var validator = provider.GetRequiredService<IShopifySessionTokenValidator>();

        var result = await validator.ValidateAsync("not-a-jwt");

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
        using var factory = new SessionTokenFactory();
        var handler = new ScriptedHttpMessageHandler();
        await using var provider = TestServices.Create(handler);
        var validator = provider.GetRequiredService<IShopifySessionTokenValidator>();

        // Signed with this factory's own key, so only the domain check can be what rejects it.
        var result = await validator.ValidateAsync(factory.CreateHostileDestToken(destination));

        Assert.False(result.Succeeded);
        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Keys_are_fetched_once_and_reused()
    {
        using var factory = new SessionTokenFactory();
        var handler = new ScriptedHttpMessageHandler()
            .OnHost(Shop, ScriptedResponse.Json(factory.BuildJwks()));

        await using var provider = TestServices.Create(handler);
        var validator = provider.GetRequiredService<IShopifySessionTokenValidator>();

        await validator.ValidateAsync(factory.Create());
        await validator.ValidateAsync(factory.Create());
        await validator.ValidateAsync(factory.Create());

        // Shopify's key set changes rarely, and fetching it per request would add a round trip to
        // every single API call the app makes.
        Assert.Single(handler.Requests);
    }

    [Fact]
    public async Task An_api_key_that_is_not_configured_rejects_every_token()
    {
        using var factory = new SessionTokenFactory();

        // With no API key there is no audience to check, so nothing can be trusted.
        var (result, handler) = await ValidateAsync(
            factory.Create(),
            configure: options => options.ApiKey = string.Empty,
            factory: factory);

        Assert.False(result.Succeeded);
        Assert.Empty(handler.Requests);
    }
}
