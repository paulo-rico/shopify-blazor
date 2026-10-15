using System.Text.RegularExpressions;
using Shopify.Blazor.Cli.Abstractions;

namespace Shopify.Blazor.Cli;

/// <summary>References and properties read out of one <c>.csproj</c>.</summary>
/// <param name="Projects">The path (or name) of every <c>ProjectReference</c>.</param>
/// <param name="Packages">The id of every <c>PackageReference</c>.</param>
public sealed record ProjectReferences(
    IReadOnlyList<string> Projects,
    IReadOnlyList<string> Packages)
{
    /// <summary>
    /// Whether the project references <paramref name="name"/>, either as a project or as a package.
    /// </summary>
    /// <remarks>
    /// Both forms are accepted on purpose: the generated app takes project references from the
    /// template, and a released app takes package references. Neither is wrong, so flagging one
    /// would train the reader to ignore the check.
    /// </remarks>
    public bool References(string name) =>
        Projects.Any(reference => MatchesName(reference, name))
        || Packages.Any(reference => string.Equals(reference, name, StringComparison.OrdinalIgnoreCase));

    private static bool MatchesName(string reference, string name)
    {
        // A ProjectReference is a relative path such as ../../src/Shopify.Blazor/Shopify.Blazor.csproj;
        // the assembly name is the file name without the extension.
        var normalised = reference.Replace('\\', '/');
        var fileName = normalised[(normalised.LastIndexOf('/') + 1)..];

        return fileName.EndsWith(".csproj", StringComparison.OrdinalIgnoreCase)
            ? string.Equals(fileName[..^".csproj".Length], name, StringComparison.OrdinalIgnoreCase)
            : string.Equals(fileName, name, StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>
/// Reads the handful of facts <c>doctor</c> needs out of a project file.
/// </summary>
/// <remarks>
/// This is deliberately a regular expression over the XML rather than a DOM parse: an MSBuild file
/// may carry conditions, comments and property evaluation that a strict parser rejects, and a
/// doctor run that dies on an unreadable csproj is worse than one that reports what it could read.
/// </remarks>
public static partial class ProjectFileReader
{
    /// <summary>Reads the project references from <paramref name="csproj"/>.</summary>
    public static ProjectReferences ReadReferences(IFileSystem fileSystem, string csproj)
    {
        var content = fileSystem.FileExists(csproj) ? fileSystem.ReadAllText(csproj) : string.Empty;

        return new ProjectReferences(
            ProjectReferenceRegex().Matches(content)
                .Select(match => match.Groups["value"].Value.Trim())
                .Where(value => value.Length > 0)
                .ToList(),
            PackageReferenceRegex().Matches(content)
                .Select(match => match.Groups["value"].Value.Trim())
                .Where(value => value.Length > 0)
                .ToList());
    }

    /// <summary>The relative path of a <c>ProjectReference</c>, resolved against the project directory.</summary>
    /// <remarks>
    /// The <c>../</c> segments are applied by hand rather than handed to
    /// <see cref="Path.GetFullPath(string, string)"/>. On Linux that overload treats a path whose
    /// first segment is <c>..</c> as rooted and ignores the base directory entirely, so
    /// <c>../../src/Polaris.Blazor/Polaris.Blazor.csproj</c> resolves against the filesystem root —
    /// which silently turns a real project reference into a missing one.
    /// </remarks>
    public static ProjectReferencePath ReadProjectReferencePath(string reference, string projectDirectory)
    {
        var normalised = reference.Replace('\\', '/');
        var current = projectDirectory;

        foreach (var part in normalised.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            current = part == ".."
                ? Path.GetDirectoryName(current) ?? current
                : Path.Combine(current, part);
        }

        return new ProjectReferencePath(current, normalised);
    }

    [GeneratedRegex("""<ProjectReference\s[^>]*Include\s*=\s*"(?<value>[^"]+)"[^>]*/?>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ProjectReferenceRegex();

    [GeneratedRegex("""<PackageReference\s[^>]*Include\s*=\s*"(?<value>[^"]+)"[^>]*/?>""",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex PackageReferenceRegex();
}

/// <summary>A <c>ProjectReference</c> as an absolute path plus the original text.</summary>
/// <param name="FullPath">The absolute path the reference resolves to.</param>
/// <param name="Original">The text as written in the project file.</param>
public sealed record ProjectReferencePath(string FullPath, string Original);
