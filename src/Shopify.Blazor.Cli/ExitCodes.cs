namespace Shopify.Blazor.Cli;

/// <summary>The exit codes every command uses.</summary>
/// <remarks>
/// Documented in docs/cli-reference.md and asserted by the tests, because a script that gates a
/// build on <c>shopify-blazor doctor</c> depends on these numbers and not on the wording.
/// </remarks>
public static class ExitCodes
{
    /// <summary>Everything asked for happened.</summary>
    public const int Success = 0;

    /// <summary>A check failed, or the command did something that needs attention.</summary>
    public const int Failure = 1;

    /// <summary>The command line itself was wrong: an unknown option, a missing value, a bad name.</summary>
    public const int Usage = 2;

    /// <summary>A file already exists and <c>--force</c> was not given.</summary>
    public const int AlreadyExists = 3;

    /// <summary>An external process the command depends on failed.</summary>
    public const int ProcessFailed = 4;

    /// <summary>A required piece of the CLI's own data — the catalogue, say — is missing.</summary>
    public const int InternalError = 5;
}
