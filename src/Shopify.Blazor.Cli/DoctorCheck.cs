namespace Shopify.Blazor.Cli;

/// <summary>The outcome of one <c>doctor</c> check.</summary>
public enum CheckStatus
{
    /// <summary>The check ran and the project is fine.</summary>
    Pass,

    /// <summary>Something looks wrong but could not be decided, or is worth a look.</summary>
    Warn,

    /// <summary>A definite problem that will cost time if it is not fixed.</summary>
    Fail,
}

/// <summary>One <c>doctor</c> check result.</summary>
/// <param name="Name">The check's name, as printed.</param>
/// <param name="Status">PASS, WARN or FAIL.</param>
/// <param name="Reason">One line saying what was found.</param>
/// <param name="Hint">What to do about it, printed for anything that is not PASS.</param>
public sealed record DoctorCheck(string Name, CheckStatus Status, string Reason, string? Hint)
{
    /// <summary>The status as the report and the JSON both spell it.</summary>
    public string StatusText => Status switch
    {
        CheckStatus.Pass => "PASS",
        CheckStatus.Warn => "WARN",
        _ => "FAIL",
    };

    /// <summary>A passing check.</summary>
    public static DoctorCheck Pass(string name, string reason) => new(name, CheckStatus.Pass, reason, null);

    /// <summary>A warning, with a hint.</summary>
    public static DoctorCheck Warn(string name, string reason, string? hint = null) =>
        new(name, CheckStatus.Warn, reason, hint);

    /// <summary>A failure, with a hint.</summary>
    public static DoctorCheck Fail(string name, string reason, string? hint = null) =>
        new(name, CheckStatus.Fail, reason, hint);
}
