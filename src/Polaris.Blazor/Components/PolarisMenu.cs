namespace Polaris.Blazor.Components;

/// <summary>Wraps <c>&lt;s-menu&gt;</c>, a list of actions that apply to one resource.</summary>
/// <remarks>
/// <para>
/// A menu has no trigger of its own. Give it an <see cref="PolarisElementComponentBase.Id"/> and
/// point a button's <see cref="PolarisButton.CommandFor"/> at it, which opens it with no
/// JavaScript.
/// </para>
/// <para>
/// Polaris accepts only <see cref="PolarisButton"/> and <see cref="PolarisSection"/> children, and
/// there are no nested submenus. Beyond roughly a dozen items a menu is hard to scan, so consider a
/// page of its own instead.
/// </para>
/// </remarks>
public sealed class PolarisMenu : PolarisSlottedComponentBase
{
    /// <inheritdoc />
    protected override string TagName => "s-menu";
}
