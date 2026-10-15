namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-unordered-list&gt;</c>.</summary>
/// <remarks>
/// Polaris accepts only <see cref="PolarisListItem"/> children. Use the ordered list instead when
/// the order of the items carries meaning.
/// </remarks>
public sealed class PolarisUnorderedList : PolarisContentComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-unordered-list";
}
