using System.Globalization;
using System.Text.RegularExpressions;

namespace Shopify.Blazor.Cli;

/// <summary>How the SDK-version comparison turned out.</summary>
public enum SdkCheckOutcome
{
    /// <summary>The installed SDK satisfies global.json.</summary>
    Satisfied,

    /// <summary>It does not; the installed feature band is below what global.json asks for.</summary>
    NotSatisfied,

    /// <summary>It cannot be decided from what is available, so report WARN rather than guess.</summary>
    Indeterminate,
}

/// <summary>The result of comparing the running SDK against <c>global.json</c>.</summary>
/// <param name="Outcome">What was decided.</param>
/// <param name="Reason">A one-line explanation.</param>
/// <param name="Hint">What to do about it, when there is something to do.</param>
public sealed record SdkCheckResult(SdkCheckOutcome Outcome, string Reason, string? Hint);

/// <summary>
/// Compares the installed .NET SDK against the <c>global.json</c> that governs a project.
/// </summary>
/// <remarks>
/// The comparison is deliberately cautious. <c>rollForward</c> has seven values with subtly
/// different rules, so rather than reimplementing MSBuild's resolution this checks the one thing
/// that is unambiguous — the installed feature band is at least the pinned one — and reports WARN
/// for everything it cannot decide. A false FAIL here sends someone to reinstall an SDK they
/// already have.
/// </remarks>
public static partial class SdkVersionCheck
{
    /// <summary>Evaluates the check.</summary>
    /// <param name="globalJsonPath">Path to the governing <c>global.json</c>, or <c>null</c>.</param>
    /// <param name="globalJsonText">Its contents, or <c>null</c> when there is no file.</param>
    /// <param name="frameworkDescription">The running framework, as the runtime reports it.</param>
    public static SdkCheckResult Evaluate(
        string? globalJsonPath,
        string? globalJsonText,
        string frameworkDescription)
    {
        if (globalJsonText is null)
        {
            return new SdkCheckResult(
                SdkCheckOutcome.Indeterminate,
                "No global.json was found at or above the project, so no SDK version is pinned.",
                "Not a problem on its own. Add a global.json with an sdk.version to pin the SDK for the team.");
        }

        var pinned = ReadVersion(globalJsonText);

        if (pinned is null)
        {
            return new SdkCheckResult(
                SdkCheckOutcome.Indeterminate,
                $"{Path.GetFileName(globalJsonPath)} has no readable sdk.version.",
                "Add \"sdk\": { \"version\": \"10.0.100\" } to global.json.");
        }

        var running = ParseFrameworkVersion(frameworkDescription);

        if (running is null)
        {
            return new SdkCheckResult(
                SdkCheckOutcome.Indeterminate,
                $"global.json pins {pinned}, but the running framework could not be read ('{frameworkDescription}').",
                $"Run 'dotnet --version' and compare it with {pinned} by hand.");
        }

        var comparison = Compare(running, pinned);

        // A runtime version and an SDK version are not the same number: the runtime reports
        // .NET 10.0.x while global.json pins an SDK such as 10.0.1xx, whose feature band is the
        // hundreds digit of its third component. Only the major and minor lines are directly
        // comparable, so anything closer than that is reported as a warning with the command that
        // answers it — a false FAIL here sends someone to reinstall an SDK they already have.
        return comparison switch
        {
            < 0 => new SdkCheckResult(
                SdkCheckOutcome.NotSatisfied,
                $"global.json pins SDK {pinned} (major.minor {pinned[..Math.Min(3, pinned.Length)]}) " +
                $"but the running framework is {running}.",
                "Install the pinned SDK (https://dotnet.microsoft.com/download/dotnet), or relax global.json's " +
                "sdk.version. Confirm what is installed with 'dotnet --version'."),

            > 0 => new SdkCheckResult(
                SdkCheckOutcome.Satisfied,
                $"the running framework {running} is a later release line than global.json's pin {pinned}.",
                null),

            _ when IsAtLeastFeatureBand(running, pinned) => new SdkCheckResult(
                SdkCheckOutcome.Satisfied,
                $"running framework {running} is at least the feature band global.json pins ({pinned}).",
                null),

            _ => new SdkCheckResult(
                SdkCheckOutcome.Indeterminate,
                $"global.json pins SDK {pinned}; the running framework reports {running}, and the runtime " +
                "version and the SDK feature band are not the same number, so this cannot be decided here.",
                "Run 'dotnet --version' and compare it with " + pinned + "."),
        };
    }

    /// <summary>Reads <c>sdk.version</c> from global.json text.</summary>
    public static string? ReadVersion(string globalJsonText)
    {
        var match = PinnedVersionRegex().Match(globalJsonText);

        return match.Success ? match.Groups["version"].Value : null;
    }

    /// <summary>Reads <c>sdk.rollForward</c> from global.json text, or <c>null</c> when unset.</summary>
    public static string? ReadRollForward(string globalJsonText)
    {
        // Scoped to the sdk block, so a rollForward elsewhere in the file cannot be mistaken for this one.
        var sdkIndex = globalJsonText.IndexOf("\"sdk\"", StringComparison.Ordinal);

        if (sdkIndex < 0)
        {
            return null;
        }

        var match = RollForwardRegex().Match(globalJsonText[sdkIndex..]);

        return match.Success ? match.Groups["value"].Value : null;
    }

    /// <summary>
    /// Extracts a comparable version from a runtime description such as <c>.NET 10.0.1</c> or
    /// <c>10.0.1</c>. Returns <c>null</c> for anything else, including .NET Framework.
    /// </summary>
    public static Version? ParseFrameworkVersion(string frameworkDescription)
    {
        var match = FrameworkVersionRegex().Match(frameworkDescription);

        return match.Success && Version.TryParse(match.Groups["version"].Value, out var version)
            ? version
            : null;
    }

    /// <summary>
    /// Compares the running version with the pinned SDK version by major and minor line:
    /// <c>9.0.5</c> against a pin of <c>10.0.100</c> is negative, <c>11.0.0</c> is positive, and the
    /// same line is zero.
    /// </summary>
    private static int Compare(Version running, string pinned)
    {
        var parts = pinned.Split('.');

        if (parts.Length < 2
            || !int.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var major)
            || !int.TryParse(parts[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var minor))
        {
            return 0;
        }

        return running.Major != major ? running.Major.CompareTo(major) : running.Minor.CompareTo(minor);
    }

    /// <summary>
    /// Whether the running version's patch is plausibly the SDK feature band. Only used to upgrade
    /// a "cannot decide" into a PASS, never to produce a FAIL.
    /// </summary>
    private static bool IsAtLeastFeatureBand(Version running, string pinned)
    {
        var parts = pinned.Split('.');

        return parts.Length > 2
            && int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var sdkPatch)
            && running.Build * 100 >= sdkPatch;
    }

    [GeneratedRegex(@"""sdk""\s*:\s*\{[^}]*?""version""\s*:\s*""(?<version>[^""]+)""",
        RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex PinnedVersionRegex();

    [GeneratedRegex(@"""rollForward""\s*:\s*""(?<value>[^""]+)""", RegexOptions.CultureInvariant)]
    private static partial Regex RollForwardRegex();

    [GeneratedRegex(@"(?<version>\d+\.\d+(?:\.\d+)?)", RegexOptions.CultureInvariant)]
    private static partial Regex FrameworkVersionRegex();
}
