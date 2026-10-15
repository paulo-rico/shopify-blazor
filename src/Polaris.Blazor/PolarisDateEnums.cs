namespace Polaris.Blazor;

/// <summary>Selection behaviour of an <c>s-date-picker</c>.</summary>
public enum PolarisDatePickerType
{
    /// <summary>One date. The default.</summary>
    [PolarisValue("single")]
    Single,

    /// <summary>A date range, with the two dates separated by <c>--</c> in the value.</summary>
    [PolarisValue("range")]
    Range,
}

/// <summary>How many months an <c>s-date-picker</c> shows at once.</summary>
public enum PolarisDatePickerVisibleMonths
{
    /// <summary>Chosen by the available width. The default.</summary>
    [PolarisValue("auto")]
    Auto,

    /// <summary>One month.</summary>
    [PolarisValue("1")]
    One,

    /// <summary>Two months.</summary>
    [PolarisValue("2")]
    Two,
}
