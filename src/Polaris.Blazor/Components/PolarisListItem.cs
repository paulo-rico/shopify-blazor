namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-list-item&gt;</c>.</summary>
/// <remarks>
/// Only valid inside a <see cref="PolarisOrderedList"/> or a <see cref="PolarisUnorderedList"/>; it
/// has no properties of its own and takes content only.
/// </remarks>
public sealed class PolarisListItem : PolarisContentComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-list-item";
}
