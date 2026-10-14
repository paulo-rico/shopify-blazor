using System.Globalization;
using System.Reflection;

namespace Polaris.Blazor;

/// <summary>
/// Converts .NET parameter values into the attribute forms Shopify's web components expect.
/// </summary>
/// <remarks>
/// Polaris attribute names are camelCase (<c>labelAccessibilityVisibility</c>,
/// <c>borderRadius</c>), so a parameter name is already the attribute name and needs no mapping
/// table. Values, however, need three rules that are easy to get wrong:
/// <list type="bullet">
/// <item>
/// enumerations are lower-case-hyphenated (<see cref="PolarisTone.Critical"/> →
/// <c>critical</c>, <see cref="PolarisTableHeaderFormat.Currency"/> → <c>currency</c>);
/// </item>
/// <item>
/// booleans are presence attributes — <c>false</c> must remove the attribute, because
/// <c>disabled="false"</c> is still true to a custom element;
/// </item>
/// <item>
/// numbers must be formatted with the invariant culture, or a machine with a comma decimal
/// separator emits <c>value="1,5"</c> and Shopify rejects it.
/// </item>
/// </list>
/// </remarks>
public static class PolarisAttributeValue
{
    /// <summary>
    /// Renders a value as an attribute string, or returns <c>null</c> when the attribute should
    /// be omitted entirely.
    /// </summary>
    public static string? Format(object? value) => value switch
    {
        null => null,
        bool b => b ? string.Empty : null,                 // presence attribute
        string s => s,
        Enum e => EnumValue(e),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString(),
    };

    /// <summary>True when <paramref name="value"/> should be written as a bare attribute.</summary>
    public static bool IsBare(object? value) => value is true;

    /// <summary>
    /// Converts an enum member to Shopify's spelling: <c>CamelCase</c>/
    /// <c>PascalCase</c> becomes <c>camel-case</c>.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// The enum member carries no <see cref="PolarisValueAttribute"/> and its name cannot be
    /// converted unambiguously. Failing here is deliberate: an unrecognised variant is a silent
    /// visual bug otherwise.
    /// </exception>
    public static string EnumValue(Enum value)
    {
        var member = value.GetType().GetMember(value.ToString()).FirstOrDefault();

        var explicitValue = member?.GetCustomAttribute<PolarisValueAttribute>();
        if (explicitValue is not null)
        {
            return explicitValue.Value;
        }

        return ToCamelCase(value.ToString());
    }

    /// <summary>
    /// Converts a PascalCase or CamelCase identifier to lower-case camelCase:
    /// <c>SemiBold</c> becomes <c>semiBold</c>, <c>Auto</c> becomes <c>auto</c>.
    /// </summary>
    /// <remarks>
    /// Shopify's values are a mixture of plain words and multi-word camelCase
    /// (<c>large-100</c>, <c>labelAccessibilityVisibility</c>), so this is deliberately a
    /// first-letter operation only. Multi-word values that a convention cannot derive are pinned
    /// with <see cref="PolarisValueAttribute"/> instead of guessed at.
    /// </remarks>
    public static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name) || char.IsLower(name[0]))
        {
            return name;
        }

        return char.ToLowerInvariant(name[0]) + name[1..];
    }
}

/// <summary>
/// Pins an enum member to the exact string Shopify documents, for values a naming convention
/// cannot express (digits, punctuation, or a name that differs from the docs).
/// </summary>
[AttributeUsage(AttributeTargets.Field)]
public sealed class PolarisValueAttribute(string value) : Attribute
{
    /// <summary>The documented value.</summary>
    public string Value { get; } = value;
}
