namespace Shopify.Blazor.SampleApp;

/// <summary>
/// One alloy, as the dashboard's data grid demonstrates it.
/// </summary>
/// <remarks>
/// A record, so the default key selector — the record itself, compared by value — identifies a row
/// without the page having to supply a <c>Func&lt;TItem, object&gt;</c>. It is the shape a real
/// jewellery screen has: the workshop's name for the alloy, the metal it is made of, the grade that
/// metal is held to, what a gram of it costs, when it was last costed, and whether it is still in use.
/// </remarks>
/// <param name="Id">The database identity, used as the row key.</param>
/// <param name="Name">What the workshop calls it, such as "Yellow gold 18ct".</param>
/// <param name="Metal">The metal family, such as "Yellow gold" or "Platinum".</param>
/// <param name="Carat">Gold carat; zero for silver and the platinum group.</param>
/// <param name="SilverPercentage">Silver content as a percentage; zero for everything else.</param>
/// <param name="CostPerGram">What a gram of the alloy costs, in pounds.</param>
/// <param name="LastCosted">The day the cost was last recalculated from the metal price.</param>
/// <param name="Active">Whether the workshop still uses it.</param>
public sealed record Alloy(
    int Id,
    string Name,
    string Metal,
    int Carat,
    decimal SilverPercentage,
    decimal CostPerGram,
    DateOnly LastCosted,
    bool Active);
