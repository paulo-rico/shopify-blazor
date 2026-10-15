using System.Text.RegularExpressions;
using Shopify.Blazor.Cli.Abstractions;

namespace Shopify.Blazor.Cli;

/// <summary>What a scan of a project's Razor files found.</summary>
/// <param name="FilesScanned">How many <c>.razor</c> files were read.</param>
/// <param name="FilesWithDirective">The files carrying <c>@rendermode InteractiveServer</c>.</param>
public sealed record RenderModeScan(int FilesScanned, IReadOnlyList<string> FilesWithDirective)
{
    /// <summary>Whether the directive was found anywhere.</summary>
    public bool Found => FilesWithDirective.Count > 0;
}

/// <summary>
/// Looks for the <c>@rendermode InteractiveServer</c> directive in a project's Razor files.
/// </summary>
/// <remarks>
/// This is the highest-value check the CLI performs. A Blazor Web App is static unless a render
/// mode is applied, and the result is not an error: the page renders perfectly and no handler is
/// ever attached, so every button on it is inert (AGENTS.md #2, docs/adr/0001-hosting-model.md).
/// Nothing in a build log, a test or a browser console says so.
/// </remarks>
public static partial class RenderModeScanner
{
    /// <summary>The directive, matched at the start of a line so a mention inside a comment is ignored.</summary>
    public const string Directive = "@rendermode InteractiveServer";

    /// <summary>Directories that never contain the app's own Razor files.</summary>
    private static readonly string[] IgnoredDirectories = ["bin", "obj", "node_modules", ".git", ".vs"];

    /// <summary>Scans <paramref name="projectDirectory"/> for the directive.</summary>
    public static RenderModeScan Scan(IFileSystem fileSystem, string projectDirectory)
    {
        var scanned = 0;
        var withDirective = new List<string>();

        foreach (var path in RazorFiles(fileSystem, projectDirectory))
        {
            scanned++;

            if (HasDirective(fileSystem.ReadAllText(path)))
            {
                withDirective.Add(path);
            }
        }

        return new RenderModeScan(scanned, withDirective);
    }

    /// <summary>
    /// Whether <paramref name="content"/> carries the directive.
    /// </summary>
    /// <remarks>
    /// Razor comments are stripped before the match, and the match is anchored to a line on its own.
    /// A commented-out directive must not count: the template plan itself quotes one inside a
    /// <c>@* … *@</c> block, and treating that as live is exactly the failure this check exists to
    /// catch.
    /// </remarks>
    public static bool HasDirective(string content) => DirectiveRegex().IsMatch(StripComments(content));

    /// <summary>Removes Razor and HTML comments so a commented-out directive cannot be mistaken for one.</summary>
    private static string StripComments(string content)
    {
        var withoutRazorComments = RazorCommentRegex().Replace(content, string.Empty);

        return HtmlCommentRegex().Replace(withoutRazorComments, string.Empty);
    }

    /// <summary>Every <c>.razor</c> file under <paramref name="projectDirectory"/>, skipping build output.</summary>
    public static IEnumerable<string> RazorFiles(IFileSystem fileSystem, string projectDirectory)
    {
        var pending = new Stack<string>();
        pending.Push(projectDirectory);

        while (pending.Count > 0)
        {
            var directory = pending.Pop();

            foreach (var file in fileSystem.EnumerateFiles(directory, "*.razor"))
            {
                yield return file;
            }

            foreach (var child in fileSystem.EnumerateDirectories(directory))
            {
                var name = Path.GetFileName(child);

                if (!IgnoredDirectories.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    pending.Push(child);
                }
            }
        }
    }

    [GeneratedRegex(@"^[ \t]*@rendermode[ \t]+InteractiveServer[ \t]*$",
        RegexOptions.Multiline | RegexOptions.CultureInvariant)]
    private static partial Regex DirectiveRegex();

    [GeneratedRegex(@"@\*.*?\*@", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex RazorCommentRegex();

    [GeneratedRegex(@"<!--.*?-->", RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex HtmlCommentRegex();
}
