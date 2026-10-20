namespace Shopify.Blazor.SampleApp;

/// <summary>
/// One alloy, as the dashboard's grid demonstrates it.
/// </summary>
/// <remarks>
/// A record, so the default key selector — the record itself, compared by value — identifies a row
/// without the page having to supply a <c>Func&lt;TItem, object&gt;</c>. It is the shape a real
/// jewellery screen has: a name, the precious-metal carat, the silver content and the fineness of the
/// alloy.
/// </remarks>
/// <param name="Id">The database identity, used as the row key.</param>
/// <param name="Name">What the workshop calls it.</param>
/// <param name="Carat">Gold carat; zero for silver and the platinum group.</param>
/// <param name="SilverPercentage">Silver content as a percentage; zero for everything else.</param>
/// <param name="Fineness">Precious metal content as a fraction, the way an assay states it.</param>
/// <param name="Active">Whether the workshop still uses it.</param>
public sealed record Alloy(
    int Id,
    string Name,
    int Carat,
    decimal SilverPercentage,
    decimal Fineness,
    bool Active);
