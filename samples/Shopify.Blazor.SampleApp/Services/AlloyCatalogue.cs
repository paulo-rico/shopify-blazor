namespace Shopify.Blazor.SampleApp.Services;

/// <summary>
/// A stand-in for the database this sample app does not have.
/// </summary>
/// <remarks>
/// <para>
/// The data grid needs a catalogue worth looking at, and this app has no database — so this service
/// generates one: 150 alloys across fifteen metals and ten grades each. Generated
/// <em>deterministically</em>, on purpose: a screenshot, a filter and a bug report then describe the
/// same 150 rows, and "it showed 148 on my machine" is never a mystery.
/// </para>
/// <para>
/// It answers asynchronously with a short delay, because that is what a real source does. That is not
/// decoration either: the grid's loading state, its page reset after new data arrives, and the
/// merchant's ability to filter a catalogue that is still arriving are all things a synchronous array
/// in a Razor file cannot demonstrate.
/// </para>
/// </remarks>
public sealed class AlloyCatalogue
{
    /// <summary>How long a load appears to take, in milliseconds: long enough to see the spinner.</summary>
    public const int LoadDelayMilliseconds = 450;

    /// <summary>How many alloys the catalogue holds. Chosen to be more than one page of any size offered.</summary>
    public const int RowCount = 150;

    private static readonly MetalFamily[] Families =
    [
        new("Fine silver", MetalKind.Silver),
        new("Sterling silver", MetalKind.Silver),
        new("Britannia silver", MetalKind.Silver),
        new("Argentium silver", MetalKind.Silver),
        new("Yellow gold", MetalKind.Gold),
        new("White gold", MetalKind.Gold),
        new("Rose gold", MetalKind.Gold),
        new("Green gold", MetalKind.Gold),
        new("Red gold", MetalKind.Gold),
        new("Grey gold", MetalKind.Gold),
        new("Platinum", MetalKind.PlatinumGroup),
        new("Palladium", MetalKind.PlatinumGroup),
        new("Platinum-iridium", MetalKind.PlatinumGroup),
        new("Platinum-ruthenium", MetalKind.PlatinumGroup),
        new("Palladium-ruthenium", MetalKind.PlatinumGroup),
    ];

    // Ten grades per family, and each family gets the grades its metal is actually held to: silver and
    // the platinum group are stamped in parts per thousand, gold in carats.
    private static readonly int[] SilverParts = [999, 985, 958, 935, 925, 916, 900, 875, 835, 800];
    private static readonly int[] GoldCarats = [8, 9, 10, 12, 14, 16, 18, 20, 22, 24];
    private static readonly int[] PlatinumParts = [999, 970, 950, 900, 850, 800, 750, 700, 600, 500];

    // A pound a gram for pure silver, and the same for the other metals: the alloy's cost is the pure
    // metal's price scaled by its content plus a small fabrication charge. Plausible rather than
    // accurate — this is demo data, and it says so.
    private const decimal PureSilver = 1.15m;
    private const decimal PureGold = 62.40m;
    private const decimal PurePlatinum = 30.80m;
    private const decimal PurePalladium = 26.40m;

    private static readonly DateOnly CostedUpto = new(2026, 10, 20);

    /// <summary>
    /// Loads the catalogue, as a database would: asynchronously, and not instantly.
    /// </summary>
    /// <param name="cancellationToken">Cancels the load, for a page that navigated away.</param>
    public async Task<IReadOnlyList<Alloy>> LoadAsync(CancellationToken cancellationToken = default)
    {
        await Task.Delay(LoadDelayMilliseconds, cancellationToken);

        var rows = new List<Alloy>(RowCount);
        var id = 1;

        foreach (var family in Families)
        {
            foreach (var grade in GradesFor(family.Kind))
            {
                rows.Add(Build(id++, family, grade));
            }
        }

        return rows;
    }

    private static IEnumerable<int> GradesFor(MetalKind kind) => kind switch
    {
        MetalKind.Gold => GoldCarats,
        MetalKind.Silver => SilverParts,
        _ => PlatinumParts,
    };

    private static Alloy Build(int id, MetalFamily family, int grade)
    {
        // Carat and parts per thousand describe the same thing, so one of them is always zero and the
        // other is the grade the alloy is stamped with.
        var carat = family.Kind == MetalKind.Gold ? grade : 0;
        var parts = family.Kind == MetalKind.Gold ? grade * 1000 / 24 : grade;

        // Silver is the only metal this catalogue states as a percentage, and only the silver grades
        // contain any: a gold alloy's silver content is zero, which is what makes a range filter on
        // that column worth demonstrating.
        var silverPercentage = family.Kind == MetalKind.Silver ? grade / 10m : 0m;

        var cost = family.Kind switch
        {
            MetalKind.Gold => parts / 1000m * PureGold,
            MetalKind.Silver => parts / 1000m * PureSilver,
            _ => parts / 1000m * (family.Name.StartsWith("Palladium", StringComparison.Ordinal) ? PurePalladium : PurePlatinum),
        } + 0.35m;

        return new Alloy(
            id,
            $"{family.Name} {Label(family.Kind, grade)}",
            family.Name,
            carat,
            silverPercentage,
            Math.Round(cost, 4),
            CostedUpto.AddDays(-(id % 45)),
            // Every seventh alloy has been retired, so the boolean filter has something to find and a
            // badge column has both states on one page.
            id % 7 != 5);
    }

    private static string Label(MetalKind kind, int grade) => kind == MetalKind.Gold
        ? string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{grade}ct")
        : string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{grade}");

    private enum MetalKind
    {
        Silver,
        Gold,
        PlatinumGroup,
    }

    private sealed record MetalFamily(string Name, MetalKind Kind);
}
