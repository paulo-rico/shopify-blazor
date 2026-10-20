namespace Shopify.Blazor.SampleApp;

/// <summary>
/// One product, as the dashboard's data grid demonstrates it.
/// </summary>
/// <remarks>
/// A record, so the default key selector — the record itself, compared by value — identifies a row
/// without the page having to supply a <c>Func&lt;TItem, object&gt;</c>. The shape deliberately mirrors
/// what the Admin API returns: a title, a status, a vendor, a product type, a price, an inventory count
/// and when it last changed. Rendered by <see cref="Services.ProductCatalogue"/>, which stands in for
/// the store this app is not always connected to.
/// </remarks>
/// <param name="Id">The product identity, used as the row key.</param>
/// <param name="Title">What the product is called.</param>
/// <param name="Status">Active, Draft or Archived, as Shopify states it.</param>
/// <param name="Vendor">Who makes it.</param>
/// <param name="ProductType">Ring, necklace, earring and so on.</param>
/// <param name="Price">The price, in pounds.</param>
/// <param name="Inventory">Units on hand. Zero is a real state, not a missing one.</param>
/// <param name="UpdatedAt">When the product last changed.</param>
public sealed record SampleProduct(
    int Id,
    string Title,
    string Status,
    string Vendor,
    string ProductType,
    decimal Price,
    int Inventory,
    DateOnly UpdatedAt);
