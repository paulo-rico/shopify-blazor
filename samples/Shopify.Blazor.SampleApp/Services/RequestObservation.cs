namespace Shopify.Blazor.SampleApp.Services;

/// <summary>How one request to a protected endpoint looked when it arrived.</summary>
/// <param name="At">When it arrived.</param>
/// <param name="Path">The path requested.</param>
/// <param name="AuthorizationHeaderPresent">Whether an <c>Authorization</c> header was sent at all.</param>
/// <param name="AuthorizationScheme">The scheme word in that header, for example <c>Bearer</c>.</param>
/// <param name="TokenLength">Length of the token, which is enough to tell "none" from "one".</param>
/// <param name="Outcome">What happened next.</param>
public sealed record RequestObservation(
    DateTimeOffset At,
    string Path,
    bool AuthorizationHeaderPresent,
    string? AuthorizationScheme,
    int? TokenLength,
    string Outcome);
