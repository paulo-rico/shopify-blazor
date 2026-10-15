using Shopify.Blazor.Cli.Abstractions;

namespace Shopify.Blazor.Cli;

/// <summary>What happened when a navigation entry was added, or why it was not.</summary>
/// <param name="Inserted">Whether the file was changed.</param>
/// <param name="FilePath">The file that was changed, when one was.</param>
/// <param name="Message">A line for the developer.</param>
public sealed record NavInsertionResult(bool Inserted, string? FilePath, string Message);

/// <summary>Places a navigation entry into the app's shell without guessing.</summary>
public static class NavMenuInserter
{
    /// <summary>The shell files that are searched, in order.</summary>
    public static readonly IReadOnlyList<string> CandidateFiles =
    [
        "Components/Layout/NavMenu.razor",
        "Components/Layout/MainLayout.razor",
        "Components/NavMenu.razor",
    ];

    /// <summary>How the entry is indented when the shell offers no clue.</summary>
    private const string FallbackIndentation = "        ";

    /// <summary>
    /// Adds an entry for <paramref name="href"/> to the first candidate shell that exists.
    /// </summary>
    /// <remarks>
    /// The insertion point is found by structure, not by rewriting the file: before the first
    /// <c>&lt;PolarisDivider /&gt;</c> if there is one (with the sample layout that puts the entry at the
    /// end of the navigation group and above the divider), otherwise before the last closing tag that
    /// starts its own line. When neither exists, nothing is written and the caller says so — an entry
    /// appended to the bottom of a layout can end up outside the page chrome, which looks like the
    /// entry silently did not work.
    /// </remarks>
    public static NavInsertionResult Insert(
        IFileSystem fileSystem,
        string projectDirectory,
        string label,
        string href)
    {
        var candidate = CandidateFiles
            .Select(relative => Path.Combine(projectDirectory, relative.Replace('/', Path.DirectorySeparatorChar)))
            .FirstOrDefault(fileSystem.FileExists);

        if (candidate is null)
        {
            return new NavInsertionResult(
                false,
                null,
                "No navigation file found. Looked for " + string.Join(", ", CandidateFiles) +
                ". Add the entry by hand — this command will not guess where navigation lives.");
        }

        var content = fileSystem.ReadAllText(candidate);

        if (content.Contains($"Href=\"{href}\"", StringComparison.Ordinal)
            || content.Contains($"href=\"{href}\"", StringComparison.Ordinal))
        {
            return new NavInsertionResult(false, candidate, $"Navigation already links to {href}; nothing to add.");
        }

        var insertionIndex = DividerLineStart(content) ?? LastOwnLineClosingTagStart(content);

        if (insertionIndex is not { } index || index <= 0)
        {
            return new NavInsertionResult(
                false,
                candidate,
                $"Could not find a safe place to insert into {candidate}: it has neither a <PolarisDivider /> nor " +
                "a closing tag on its own line. Add the entry by hand rather than risk putting it outside the " +
                "page chrome.");
        }

        var indentation = IndentationAt(content, index);
        var entry = Render(content, label, href, indentation);
        var updated = content[..index] + entry + content[index..];

        fileSystem.WriteAllText(candidate, updated);

        return new NavInsertionResult(
            true,
            candidate,
            $"Added a navigation entry for {href} to {Path.GetFileName(candidate)}.");
    }

    /// <summary>
    /// Renders the entry in the shell's own idiom: a <c>PolarisButton</c> when the shell uses
    /// Polaris components, an <c>&lt;a&gt;</c> when it does not.
    /// </summary>
    public static string Render(string shellContent, string label, string href, string indentation)
    {
        var usesPolaris = shellContent.Contains("<PolarisPage", StringComparison.Ordinal)
            || shellContent.Contains("<PolarisButton", StringComparison.Ordinal);

        return usesPolaris
            ? $"{indentation}<PolarisButton Href=\"{href}\" Target=\"PolarisLinkTarget.Self\" " +
              $"Variant=\"PolarisButtonVariant.Tertiary\">{label}</PolarisButton>\n"
            : $"{indentation}<a href=\"{href}\">{label}</a>\n";
    }

    /// <summary>The start of the line holding the first divider, or <c>null</c>.</summary>
    private static int? DividerLineStart(string content)
    {
        var divider = content.IndexOf("<PolarisDivider", StringComparison.Ordinal);

        return divider < 0 ? null : content.LastIndexOf('\n', divider) + 1;
    }

    /// <summary>The start of the last line that holds a closing tag and nothing else, or <c>null</c>.</summary>
    private static int? LastOwnLineClosingTagStart(string content)
    {
        for (var search = content.LastIndexOf("</", StringComparison.Ordinal);
             search >= 0;
             search = content.LastIndexOf("</", search - 1))
        {
            var lineStart = content.LastIndexOf('\n', search) + 1;

            if (content[lineStart..search].Trim().Length == 0)
            {
                return lineStart;
            }
        }

        return null;
    }

    /// <summary>The leading whitespace of the line beginning at <paramref name="lineStart"/>.</summary>
    private static string IndentationAt(string content, int lineStart)
    {
        var end = lineStart;

        while (end < content.Length && (content[end] == ' ' || content[end] == '\t'))
        {
            end++;
        }

        return end > lineStart ? content[lineStart..end] : FallbackIndentation;
    }
}
