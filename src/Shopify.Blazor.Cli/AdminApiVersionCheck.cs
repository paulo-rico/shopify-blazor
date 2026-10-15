using System.Globalization;

namespace Shopify.Blazor.Cli;

/// <summary>How a project's Admin API version compares with the known stable releases.</summary>
public enum ApiVersionOutcome
{
    /// <summary>The version is one Shopify has released as stable and it is the newest known.</summary>
    Current,

    /// <summary>The version is stable but an older one exists.</summary>
    Outdated,

    /// <summary>The version is not a stable release at all (wrong shape, or a future/unknown month).</summary>
    Unknown,

    /// <summary>No version could be found, so the toolkit's default is assumed.</summary>
    Missing,
}

/// <summary>The result of checking the Admin API version.</summary>
/// <param name="Outcome">What was decided.</param>
/// <param name="Version">The version that was checked, if any.</param>
/// <param name="Reason">One line explaining it.</param>
/// <param name="Hint">What to do about it.</param>
public sealed record ApiVersionCheckResult(
    ApiVersionOutcome Outcome,
    string? Version,
    string Reason,
    string? Hint);

/// <summary>
/// Decides whether a project's Admin API version is a current Shopify stable release.
/// </summary>
/// <remarks>
/// <para>
/// Shopify ships a stable version every quarter and names it <c>YYYY-MM</c>; each one is retired
/// about a year later. Asking for a retired version does <strong>not</strong> fail — Shopify falls
/// forward to an older stable release and answers normally, with only the
/// <c>X-Shopify-API-Version</c> response header to show for it (AGENTS.md #16). That is why an
/// outdated version has to be reported here: nothing else in the app will say a word.
/// </para>
/// <para>
/// The table below is deliberately small and hand-maintained. It is a hint, not an oracle: a
/// version newer than the newest entry is reported as unknown rather than wrong, so an out-of-date
/// CLI never tells anyone that a working configuration is broken. Keep it in step with
/// <see href="https://shopify.dev/docs/api/usage/versioning"/>.
/// </para>
/// </remarks>
public static class AdminApiVersionCheck
{
    /// <summary>Where Shopify publishes the version table this list tracks.</summary>
    public const string VersioningUrl = "https://shopify.dev/docs/api/usage/versioning";

    /// <summary>
    /// The stable versions this CLI knows about, oldest first. Shopify releases one per quarter, so
    /// four a year; a gap here only costs a WARN, never a FAIL.
    /// </summary>
    public static readonly IReadOnlyList<string> KnownStableVersions =
    [
        "2024-01",
        "2024-04",
        "2024-07",
        "2024-10",
        "2025-01",
        "2025-04",
        "2025-07",
        "2025-10",
        "2026-01",
        "2026-04",
        "2026-07",
        "2026-10",
    ];

    /// <summary>The newest stable version in the table above.</summary>
    public static string NewestKnownVersion => KnownStableVersions[^1];

    /// <summary>Evaluates <paramref name="version"/> against the table.</summary>
    public static ApiVersionCheckResult Evaluate(string? version)
    {
        if (string.IsNullOrWhiteSpace(version))
        {
            return new ApiVersionCheckResult(
                ApiVersionOutcome.Missing,
                null,
                $"no AdminApiVersion is set in appsettings.json, so the toolkit default ({NewestKnownVersion}) applies.",
                null);
        }

        var trimmed = version.Trim();
        var parsed = Parse(trimmed);

        if (parsed is null)
        {
            return new ApiVersionCheckResult(
                ApiVersionOutcome.Unknown,
                trimmed,
                $"'{trimmed}' is not a Shopify version name; stable versions look like 2026-10.",
                $"Set Shopify:AdminApiVersion to a version from {VersioningUrl}.");
        }

        var index = IndexOf(trimmed);

        if (index >= 0 && index == KnownStableVersions.Count - 1)
        {
            return new ApiVersionCheckResult(
                ApiVersionOutcome.Current,
                trimmed,
                $"{trimmed} is the newest stable version this CLI knows about.",
                null);
        }

        if (index >= 0)
        {
            return new ApiVersionCheckResult(
                ApiVersionOutcome.Outdated,
                trimmed,
                $"{trimmed} is stable but older than {NewestKnownVersion}.",
                $"Shopify retires a version about a year after it ships and then falls forward silently. " +
                $"Move to {NewestKnownVersion} when convenient; see {VersioningUrl}.");
        }

        // Not in the table. Future-dated, or a month Shopify does not release in.
        return ApiVersionTupleComparer.Compare(parsed.Value, Parse(NewestKnownVersion)!.Value) > 0
            ? new ApiVersionCheckResult(
                ApiVersionOutcome.Unknown,
                trimmed,
                $"{trimmed} is newer than the newest stable version this CLI knows about ({NewestKnownVersion}).",
                $"Confirm it exists in {VersioningUrl}; if it does, update this CLI so the table is current.")
            : new ApiVersionCheckResult(
                ApiVersionOutcome.Unknown,
                trimmed,
                $"{trimmed} is not one of the stable versions this CLI knows about " +
                $"({KnownStableVersions[0]} to {NewestKnownVersion}).",
                $"Shopify releases a stable version each quarter. Check the list at {VersioningUrl}.");
    }

    /// <summary>Parses a <c>YYYY-MM</c> version name into a sortable value, or <c>null</c>.</summary>
    public static (int Year, int Month)? Parse(string version)
    {
        var parts = version.Split('-');

        if (parts.Length != 2
            || parts[0].Length != 4
            || parts[1].Length != 2
            || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var year)
            || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var month)
            || month is < 1 or > 12)
        {
            return null;
        }

        return (year, month);
    }

    private static int IndexOf(string version)
    {
        for (var index = 0; index < KnownStableVersions.Count; index++)
        {
            if (string.Equals(KnownStableVersions[index], version, StringComparison.Ordinal))
            {
                return index;
            }
        }

        return -1;
    }
}

/// <summary>Orders two <c>YYYY-MM</c> values.</summary>
internal static class ApiVersionTupleComparer
{
    /// <summary>Orders <c>(year, month)</c> tuples chronologically.</summary>
    public static int Compare((int Year, int Month) left, (int Year, int Month) right) =>
        left.Year != right.Year ? left.Year.CompareTo(right.Year) : left.Month.CompareTo(right.Month);
}
