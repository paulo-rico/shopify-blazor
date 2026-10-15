using System.Text;

namespace Shopify.Blazor.Cli;

/// <summary>Turns developer-supplied names into the spellings the toolkit expects.</summary>
public static class NameHelper
{
    /// <summary>
    /// Converts a PascalCase or camelCase identifier to kebab-case: <c>Products</c> becomes
    /// <c>products</c>, <c>ProductEditor</c> becomes <c>product-editor</c>, and a run of capitals
    /// stays together until the next word starts, so <c>APIKeys</c> becomes <c>api-keys</c>.
    /// </summary>
    /// <remarks>
    /// A route spelled differently from the identifier is not a build error, it is a 404 — or
    /// worse, a route that shadows another page's. This is the one place the spelling is decided.
    /// </remarks>
    public static string ToKebabCase(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(name.Length + 4);
        var characters = name.Trim().ToCharArray();

        for (var index = 0; index < characters.Length; index++)
        {
            var character = characters[index];

            // Anything that cannot start an identifier becomes a separator rather than vanishing.
            if (!char.IsLetterOrDigit(character))
            {
                if (builder.Length > 0 && builder[^1] != '-')
                {
                    builder.Append('-');
                }

                continue;
            }

            if (char.IsUpper(character) && builder.Length > 0 && builder[^1] != '-')
            {
                var previousIsLowerOrDigit = char.IsLower(characters[index - 1]) || char.IsDigit(characters[index - 1]);
                var nextIsLower = index + 1 < characters.Length && char.IsLower(characters[index + 1]);

                // Break before an uppercase letter that starts a new word: after a lowercase
                // letter or digit (ProductEditor), or before the last capital of a run that is
                // followed by lowercase (APIKeys -> api-keys).
                if (previousIsLowerOrDigit || nextIsLower)
                {
                    builder.Append('-');
                }
            }

            builder.Append(char.ToLowerInvariant(character));
        }

        return builder.ToString().Trim('-');
    }

    /// <summary>
    /// Converts a name to a PascalCase C# identifier, so <c>product-editor</c> and
    /// <c>product editor</c> both become <c>ProductEditor</c>.
    /// </summary>
    public static string ToPascalCase(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(name.Length);
        var capitaliseNext = true;

        foreach (var character in name.Trim())
        {
            if (!char.IsLetterOrDigit(character))
            {
                capitaliseNext = true;
                continue;
            }

            builder.Append(capitaliseNext ? char.ToUpperInvariant(character) : character);
            capitaliseNext = false;
        }

        return builder.ToString();
    }

    /// <summary>
    /// Whether <paramref name="name"/> can become a C# type name and a file name without a
    /// surprise. Rejecting early beats writing a file called <c>My Page.razor</c>.
    /// </summary>
    public static bool IsValidComponentName(string name, out string reason)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            reason = "The name is empty.";
            return false;
        }

        var trimmed = name.Trim();
        var identifier = ToPascalCase(trimmed);

        if (identifier.Length == 0 || !char.IsLetter(identifier[0]))
        {
            reason = $"'{name}' does not contain any letters, so it cannot become a component name.";
            return false;
        }

        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            if (trimmed.Contains(invalid))
            {
                reason = $"'{name}' contains '{invalid}', which is not valid in a file name.";
                return false;
            }
        }

        reason = string.Empty;
        return true;
    }
}
