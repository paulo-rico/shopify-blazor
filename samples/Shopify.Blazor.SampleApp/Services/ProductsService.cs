
namespace Shopify.Blazor.SampleApp.Services;

/// <summary>Products for one shop, fetched through the authenticated Admin API.</summary>
/// <param name="ShopDomain">The shop the data came from.</param>
/// <param name="Products">The products.</param>
/// <param name="ApiVersion">The Admin API version Shopify actually served.</param>
public sealed record ProductListResult(string ShopDomain, IReadOnlyList<ProductSummary> Products, string? ApiVersion);

/// <summary>A product, flattened for display.</summary>
/// <param name="Id">The GraphQL global id.</param>
/// <param name="Title">The product title.</param>
/// <param name="Status">Draft, active or archived.</param>
/// <param name="Price">The first variant's price, as Shopify returns it: a string.</param>
/// <param name="UpdatedAt">When the product was last changed.</param>
public sealed record ProductSummary(string Id, string Title, string Status, string? Price, DateTimeOffset? UpdatedAt);

/// <summary>
/// The one authenticated Admin API read the reference app demonstrates.
/// </summary>
/// <remarks>
/// Note what this class does <em>not</em> do: it never reads a shop from a query string, never
/// touches an access token itself, and never exposes a token to the caller. It hands the verified
/// session token to <see cref="IShopifyAdminApi"/>, which validates the signature, exchanges the
/// token and caches the result.
/// </remarks>
public sealed class ProductsService
{
    private const string ProductsQuery = """
        query SampleProducts($first: Int!) {
          shop {
            name
            myshopifyDomain
          }
          products(first: $first, sortKey: UPDATED_AT, reverse: true) {
            nodes {
              id
              title
              status
              updatedAt
              variants(first: 1) {
                nodes {
                  price
                }
              }
            }
          }
        }
        """;

    private readonly IShopifyAdminApi _api;
    private readonly ILogger<ProductsService> _logger;

    /// <summary>Creates the service.</summary>
    public ProductsService(IShopifyAdminApi api, ILogger<ProductsService> logger)
    {
        _api = api;
        _logger = logger;
    }

    /// <summary>Lists the most recently updated products.</summary>
    /// <exception cref="ShopifyAdminApiException">Authentication failed, or Shopify refused the query.</exception>
    public async Task<ProductListResult> ListAsync(string sessionToken, CancellationToken cancellationToken = default)
    {
        var response = await _api
            .QueryAsync(sessionToken, ProductsQuery, new { first = 10 }, cancellationToken)
            .ConfigureAwait(false);

        // GraphQL errors arrive with HTTP 200, so QueryAsync has already turned them into an
        // exception by this point; there is nothing to double-check here.
        var data = response.Data!.Value;
        var products = new List<ProductSummary>();

        if (data.TryGetProperty("products", out var connection)
            && connection.TryGetProperty("nodes", out var nodes)
            && nodes.ValueKind == System.Text.Json.JsonValueKind.Array)
        {
            foreach (var node in nodes.EnumerateArray())
            {
                string? price = null;

                if (node.TryGetProperty("variants", out var variants)
                    && variants.TryGetProperty("nodes", out var variantNodes)
                    && variantNodes.ValueKind == System.Text.Json.JsonValueKind.Array
                    && variantNodes.GetArrayLength() > 0)
                {
                    // Money serialises as a string in the Admin API. Reading it as a number would
                    // lose precision on some currencies.
                    price = variantNodes[0].TryGetProperty("price", out var p) ? p.GetString() : null;
                }

                DateTimeOffset? updatedAt = null;
                if (node.TryGetProperty("updatedAt", out var updated) && updated.ValueKind == System.Text.Json.JsonValueKind.String)
                {
                    updatedAt = updated.GetDateTimeOffset();
                }

                products.Add(new ProductSummary(
                    node.GetProperty("id").GetString() ?? string.Empty,
                    node.TryGetProperty("title", out var title) ? title.GetString() ?? "(untitled)" : "(untitled)",
                    node.TryGetProperty("status", out var status) ? status.GetString() ?? "UNKNOWN" : "UNKNOWN",
                    price,
                    updatedAt));
            }
        }

        var shop = data.TryGetProperty("shop", out var shopNode)
            && shopNode.TryGetProperty("myshopifyDomain", out var domain)
                ? domain.GetString() ?? "unknown"
                : "unknown";

        _logger.LogInformation("Loaded {Count} products for {Shop} in the sample app.", products.Count, shop);

        return new ProductListResult(shop, products, response.ApiVersionServed);
    }
}
