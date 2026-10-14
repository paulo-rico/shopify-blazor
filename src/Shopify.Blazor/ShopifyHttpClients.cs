namespace Shopify.Blazor;

/// <summary>Names of the named <see cref="System.Net.Http.HttpClient"/> instances the toolkit uses.</summary>
public static class ShopifyHttpClients
{
    /// <summary>
    /// Client for the token exchange call. Kept separate from the Admin API client so the
    /// secret-bearing request cannot be confused with an ordinary API call.
    /// </summary>
    public const string TokenExchange = "Shopify.TokenExchange";

    /// <summary>Client for Admin API GraphQL calls.</summary>
    public const string AdminApi = "Shopify.AdminApi";
}
