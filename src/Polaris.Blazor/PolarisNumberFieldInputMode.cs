namespace Polaris.Blazor;

/// <summary>Keyboard hinted by an <c>s-number-field</c>.</summary>
/// <remarks>
/// This is a hint to the on-screen keyboard only. It does not restrict what can be typed, so a
/// server-side parse must still handle junk. Use a text field for a value that may contain letters,
/// such as a SKU.
/// </remarks>
public enum PolarisNumberFieldInputMode
{
    /// <summary>A keyboard with a decimal separator. The default.</summary>
    [PolarisValue("decimal")]
    Decimal,

    /// <summary>A digits-only keyboard.</summary>
    [PolarisValue("numeric")]
    Numeric,
}
