namespace Shopify.Blazor.SampleApp.Services;

/// <summary>
/// A stand-in for a store's product list.
/// </summary>
/// <remarks>
/// <para>
/// The dashboard's grid needs products to show, and this app is not always inside the Admin — so this
/// service generates a catalogue: 150 products across eight types, five vendors and three statuses.
/// Generated <em>deterministically</em>, on purpose: a screenshot, a filter and a bug report then
/// describe the same 150 rows, and "it showed 148 on my machine" is never a mystery.
/// </para>
/// <para>
/// It answers asynchronously with a short delay, because that is what the Admin API does. That is not
/// decoration: the grid's loading state, its page reset after new data arrives, and the merchant's
/// ability to filter a catalogue that is still arriving are all things a synchronous array in a Razor
/// file cannot demonstrate.
/// </para>
/// <para>
/// This is demo data and the page says so. The same grid renders <c>/api/products</c> unchanged when the
/// app is opened inside the Admin, where that endpoint is authenticated; the Products page is the one
/// that makes that call.
/// </para>
/// </remarks>
public sealed class ProductCatalogue
{
    /// <summary>How long a load appears to take, in milliseconds: long enough to see the spinner.</summary>
    public const int LoadDelayMilliseconds = 450;

    /// <summary>How many products the catalogue holds. More than one page of any size the pager offers.</summary>
    public const int RowCount = 150;

    private static readonly string[] Types =
        ["Ring", "Necklace", "Earring", "Bracelet", "Pendant", "Bangle", "Brooch", "Cufflink"];

    private static readonly string[] Styles =
        ["Victorian", "Edwardian", "Art Deco", "Modern", "Georgian", "Contemporary"];

    private static readonly string[] Stones =
        ["Diamond", "Sapphire", "Ruby", "Emerald", "Opal", "Morganite", "Aquamarine", "Signet"];

    private static readonly string[] Vendors =
        ["De Laci & Co", "Sheffield Bench", "Hatton Garden", "Antwerp Setting", "Our workshop"];

    private static readonly DateOnly ChangedUpto = new(2026, 10, 20);

    /// <summary>Loads the catalogue, as the Admin API would: asynchronously, and not instantly.</summary>
    /// <param name="cancellationToken">Cancels the load, for a page that navigated away.</param>
    public async Task<IReadOnlyList<SampleProduct>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(LoadDelayMilliseconds, cancellationToken);

        var products = new List<SampleProduct>(RowCount);

        for (var index = 0; index < RowCount; index++)
        {
            // Style changes every row, stone every six and type every forty-eight, so 150 rows are 150
            // distinct combinations without a random number generator in sight.
            var style = Styles[index % Styles.Length];
            var stone = Stones[index / Styles.Length % Stones.Length];
            var type = Types[index / (Styles.Length * Stones.Length) % Types.Length];

            products.Add(new SampleProduct(
                index + 1,
                $"{style} {stone.ToLowerInvariant()} {type.ToLowerInvariant()}",
                // Seven in ten are live, two in ten are drafts, one in ten is archived — so a status
                // filter and a badge column both have all three states on one page.
                index % 10 < 7 ? "Active" : index % 10 < 9 ? "Draft" : "Archived",
                Vendors[index % Vendors.Length],
                type,
                Math.Round(45m + ((index % Types.Length) * 185m) + ((index / Styles.Length % Stones.Length) * 95m) + ((index % 7) * 23m) + 0.95m, 2),
                // Every eleventh product is out of stock: zero is a state the merchant acts on, and a
                // numeric filter should be able to find it.
                (index * 7) % 121,
                ChangedUpto.AddDays(-(index % 90))));
        }

        return products;
    }
}
