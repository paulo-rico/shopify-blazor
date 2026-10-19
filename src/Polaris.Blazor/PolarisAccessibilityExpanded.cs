namespace Polaris.Blazor;

/// <summary>
/// The expanded state an <c>s-button</c> announces for whatever it controls.
/// </summary>
/// <remarks>
/// <para>
/// Polaris spells this as a string on an <c>accessibilityExpanded</c> property, and it has a third
/// state that a <c>bool</c> cannot express: leaving the property unset omits <c>aria-expanded</c>
/// altogether, which is what a button that controls nothing needs. Hence an enum with a null
/// default rather than a nullable <c>bool</c>.
/// </para>
/// <para>
/// Only <c>expanded</c> produces <c>aria-expanded="true"</c>; Polaris treats any other value it
/// recognises as collapsed, so <see cref="Collapsed"/> is the element's own spelling for
/// <c>aria-expanded="false"</c> and is not merely "not expanded".
/// </para>
/// </remarks>
public enum PolarisAccessibilityExpanded
{
    /// <summary>Announces <c>aria-expanded="true"</c>.</summary>
    [PolarisValue("expanded")]
    Expanded,

    /// <summary>Announces <c>aria-expanded="false"</c>.</summary>
    [PolarisValue("collapsed")]
    Collapsed,
}
