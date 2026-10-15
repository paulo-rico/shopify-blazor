using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Shopify.Blazor.Tests;

/// <summary>
/// Tests for the Admin API client and the convenience facade over it.
/// </summary>
/// <remarks>
/// The trap this file exists for: a GraphQL failure is <strong>not</strong> an HTTP failure.
/// Shopify answers most query problems with <c>200 OK</c> and an <c>errors</c> array, so code that
/// checks only the status code treats a failed query as an empty result.
/// </remarks>
public sealed class AdminApiTests
{
    private const string Shop = SessionTokenFactory.DefaultShop;

    private static string TokenBody()
        => """{"access_token":"shpat_access","scope":"read_products","expires_in":3600}""";

    private static string ProductsBody(string title = "T-shirt")
        => $$"""
            {
              "data": {
                "products": {
                  "nodes": [
                    { "id": "gid://shopify/Product/1", "title": "{{title}}", "status": "ACTIVE",
                      "variants": { "nodes": [ { "price": "19.99" } ] } }
                  ]
                }
              }
            }
            """;

    private static ScriptedHttpMessageHandler HappyHandler(string graphQlBody)
    {
        var handler = new ScriptedHttpMessageHandler();

        // The key set is registered per test rather than here, because the factory that owns the
        // key has to be the one that signs the session token.
        handler.OnPath("/admin/oauth/access_token", ScriptedResponse.Json(TokenBody()));
        handler.OnPath("/admin/api/2026-10/graphql.json", ScriptedResponse.Json(graphQlBody));
        return handler;
    }

    [Fact]
    public async Task A_successful_query_returns_the_data_element()
    {
        var handler = HappyHandler(ProductsBody());

        await using var provider = TestServices.Create(handler);
        var api = provider.GetRequiredService<IShopifyAdminApi>();

        var response = await api.QueryAsync(SessionTokenFactory.Create(), "{ products(first: 1) { nodes { id title } } }");

        Assert.True(response.IsSuccess, response.DescribeFailure());
        Assert.Equal("T-shirt", response.Data!.Value.GetProperty("products").GetProperty("nodes")[0].GetProperty("title").GetString());
    }

    [Fact]
    public async Task The_request_carries_the_access_token_and_the_configured_api_version()
    {
        var handler = HappyHandler(ProductsBody());

        await using var provider = TestServices.Create(handler);
        var api = provider.GetRequiredService<IShopifyAdminApi>();

        await api.QueryAsync(SessionTokenFactory.Create(), "{ shop { name } }");

        var request = Assert.Single(handler.Requests, r => r.Uri.AbsolutePath.Contains("/graphql.json", StringComparison.Ordinal));

        Assert.Equal(HttpMethod.Post, request.Method);
        Assert.Equal("/admin/api/2026-10/graphql.json", request.Uri.AbsolutePath);
        Assert.Equal("shpat_access", request.Headers["X-Shopify-Access-Token"]);
        Assert.Contains("\"query\"", request.Body!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Graphql_errors_on_a_200_response_are_treated_as_a_failure()
    {
        var handler = HappyHandler("""{"errors":[{"message":"Field 'nope' doesn't exist on type 'QueryRoot'","extensions":{"code":"undefinedField"}}]}""");

        await using var provider = TestServices.Create(handler);
        var api = provider.GetRequiredService<IShopifyAdminApi>();

        var error = await Assert.ThrowsAsync<ShopifyAdminApiException>(() => api.QueryAsync(SessionTokenFactory.Create(), "{ nope }"));

        // HTTP 200 with an errors array: exactly the case a status-code check misses.
        Assert.Equal(HttpStatusCode.OK, error.Response!.StatusCode);
        Assert.Contains("doesn't exist", error.Message, StringComparison.Ordinal);
        Assert.Equal("undefinedField", error.Response.Errors[0].Code);
    }

    [Fact]
    public async Task A_missing_scope_is_reported_as_access_denied()
    {
        // What a scope the merchant never granted looks like: HTTP 200, an errors array, and
        // ACCESS_DENIED in extensions. It is indistinguishable from a gateway fault by status code,
        // and retrying cannot fix it, so the exception has to say which kind of failure this is.
        var handler = HappyHandler(
            """{"errors":[{"message":"Access denied for products field.","extensions":{"code":"ACCESS_DENIED"}}]}""");

        await using var provider = TestServices.Create(handler);
        var api = provider.GetRequiredService<IShopifyAdminApi>();

        var error = await Assert.ThrowsAsync<ShopifyAdminApiException>(
            () => api.QueryAsync(SessionTokenFactory.Create(), "{ products { nodes { id } } }"));

        Assert.True(error.IsAccessDenied);

        // A 200 means the token was fine, so this must not be mistaken for a token rejection: a
        // caller that retried on IsTokenRejected would loop for ever without changing the scopes.
        Assert.False(error.IsTokenRejected);
        Assert.Contains("Access denied", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_ordinary_graphql_error_is_not_reported_as_access_denied()
    {
        // Guards the guard: if IsAccessDenied were true for every GraphQL error, the route would
        // tell merchants to change scopes for a typo in a query.
        var handler = HappyHandler("""{"errors":[{"message":"Field 'nope' doesn't exist","extensions":{"code":"undefinedField"}}]}""");

        await using var provider = TestServices.Create(handler);
        var api = provider.GetRequiredService<IShopifyAdminApi>();

        var error = await Assert.ThrowsAsync<ShopifyAdminApiException>(() => api.QueryAsync(SessionTokenFactory.Create(), "{ nope }"));

        Assert.False(error.IsAccessDenied);
    }

    [Fact]
    public async Task Throttling_is_reported_with_shopifys_error_code()
    {
        var handler = HappyHandler("""{"errors":[{"message":"Throttled","extensions":{"code":"THROTTLED"}}]}""");

        await using var provider = TestServices.Create(handler);
        var api = provider.GetRequiredService<IShopifyAdminApi>();

        var error = await Assert.ThrowsAsync<ShopifyAdminApiException>(() => api.QueryAsync(SessionTokenFactory.Create(), "{ shop { name } }"));

        Assert.Equal("THROTTLED", error.Response!.Errors[0].Code);
    }

    [Fact]
    public async Task A_rejected_access_token_is_discarded_so_the_next_call_re_exchanges()
    {
        var handler = new ScriptedHttpMessageHandler()
            .OnPath(
                "/admin/oauth/access_token",
                ScriptedResponse.Json(TokenBody()),
                ScriptedResponse.Json(TokenBody().Replace("shpat_access", "shpat_fresh", StringComparison.Ordinal)))
            .OnPath(
                "/admin/api/2026-10/graphql.json",
                ScriptedResponse.Json("{}", HttpStatusCode.Unauthorized),
                ScriptedResponse.Json(ProductsBody("Fresh")));

        await using var provider = TestServices.Create(handler);
        var api = provider.GetRequiredService<IShopifyAdminApi>();

        // One session token, used twice. It has to be genuinely the same session and subject, or
        // a fresh cache entry would make the test pass without the invalidation working at all.
        var session = SessionTokenFactory.Create();

        var error = await Assert.ThrowsAsync<ShopifyAdminApiException>(() => api.QueryAsync(session, "{ shop { name } }"));
        Assert.True(error.IsTokenRejected);

        // An app-secret rotation revokes the stored token. Recovering without a restart is the
        // whole point of dropping it here rather than surfacing a permanent error.
        var response = await api.QueryAsync(session, "{ shop { name } }");

        Assert.True(response.IsSuccess, response.DescribeFailure());
        // Two GraphQL attempts, both against a freshly exchanged token: the first exchange, then
        // one more after the cached token was dropped. The second attempt's token comes from that
        // same exchange, so there is no third call to Shopify's token endpoint.
        // Two GraphQL attempts and, crucially, two exchanges: the rejected token has to be
        // discarded in the exchanger's cache too, or the retry would present the same dead token
        // and fail identically for ever.
        Assert.Equal(2, handler.Requests.Count(r => r.Uri.AbsolutePath.EndsWith("/graphql.json", StringComparison.Ordinal)));
        Assert.Equal(2, handler.TokenExchangeRequests.Count);

        // And the retry really did use the new token, rather than the dead one.
        var attempts = handler.Requests
            .Where(r => r.Uri.AbsolutePath.EndsWith("/graphql.json", StringComparison.Ordinal))
            .ToList();

        Assert.Equal("shpat_access", attempts[0].Headers["X-Shopify-Access-Token"]);
        Assert.Equal("shpat_fresh", attempts[1].Headers["X-Shopify-Access-Token"]);
    }

    [Fact]
    public async Task A_retired_api_version_is_noticed_even_though_shopify_answers_normally()
    {
        var handler = new ScriptedHttpMessageHandler()
            .OnPath("/admin/oauth/access_token", ScriptedResponse.Json(TokenBody()))
            .OnPath(
                "/admin/api/2026-10/graphql.json",
                new ScriptedResponse(_ =>
                {
                    var response = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(ProductsBody(), System.Text.Encoding.UTF8, "application/json"),
                    };

                    // Shopify falls forward silently, so this header is the only signal.
                    response.Headers.Add("X-Shopify-API-Version", "2026-01");
                    return response;
                }));

        await using var provider = TestServices.Create(handler);
        var api = provider.GetRequiredService<IShopifyAdminApi>();

        var response = await api.QueryAsync(SessionTokenFactory.Create(), "{ shop { name } }");

        Assert.True(response.IsSuccess);
        Assert.Equal("2026-01", response.ApiVersionServed);
    }

    [Fact]
    public async Task An_unauthenticated_request_never_reaches_shopify()
    {
        var handler = new ScriptedHttpMessageHandler();
        await using var provider = TestServices.Create(handler);
        var api = provider.GetRequiredService<IShopifyAdminApi>();

        await Assert.ThrowsAsync<ShopifyAdminApiException>(() => api.QueryAsync("not-a-token", "{ shop { name } }"));

        Assert.Empty(handler.Requests);
    }

    [Fact]
    public async Task Typed_queries_deserialize_the_data_element()
    {
        var handler = HappyHandler(ProductsBody("Snowboard"));

        await using var provider = TestServices.Create(handler);
        var api = provider.GetRequiredService<IShopifyAdminApi>();

        var result = await api.QueryAsync<ProductPage>(SessionTokenFactory.Create(), "{ products(first: 1) { nodes { id title } } }");

        var product = Assert.Single(result.Products.Nodes);
        Assert.Equal("Snowboard", product.Title);
        Assert.Equal("gid://shopify/Product/1", product.Id);
    }

    private sealed record ProductPage(ProductConnection Products);

    private sealed record ProductConnection(IReadOnlyList<Product> Nodes);

    private sealed record Product(string Id, string Title);
}
