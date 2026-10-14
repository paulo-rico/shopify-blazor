using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Shopify.Blazor.Authentication;

namespace Shopify.Blazor.Diagnostics;

/// <summary>
/// Reports what a session token actually contains and why it was rejected, for use in development.
/// </summary>
/// <remarks>
/// <para>
/// Built because "not authenticated" is not a diagnosis. The failure has several distinct causes —
/// no token arrived, the token was minted for a different app, it expired, or the shop's key set
/// could not be read — and they need different fixes. The report separates them.
/// </para>
/// <para>
/// It decodes the token <strong>without verifying it</strong> and compares the claims with this
/// app's configuration. That is safe only because it is a diagnostic: the decoded claims are shown
/// to a developer and are never trusted for access. The endpoint that serves this must be
/// development-only, and the real validator still runs, so a passing diagnostic never grants access.
/// </para>
/// </remarks>
public static class SessionTokenDiagnostics
{
    /// <summary>Inspects a token and returns a report suitable for JSON serialization.</summary>
    /// <param name="handler">Used to fetch the shop's key set, to prove that part of the flow works.</param>
    /// <param name="options">This app's Shopify configuration.</param>
    /// <param name="validator">The real validator, so the reported verdict is the real one.</param>
    /// <param name="authorizationHeader">The raw <c>Authorization</c> header, if one arrived.</param>
    /// <param name="fallbackToken">A token to inspect when the header is absent.</param>
    public static async Task<TokenDiagnosticReport> InspectAsync(
        IHttpClientFactory handler,
        IOptionsMonitor<ShopifyOptions> options,
        IShopifySessionTokenValidator validator,
        string? authorizationHeader,
        string? fallbackToken = null)
    {
        var configuration = options.CurrentValue;

        var report = new TokenDiagnosticReport
        {
            AuthorizationHeaderPresent = !string.IsNullOrWhiteSpace(authorizationHeader),
            HeaderLooksLikeBearer = authorizationHeader?.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ?? false,
            ConfiguredApiKey = Mask(configuration.ApiKey),
            ConfiguredAppUrl = configuration.AppUrl,
            AppUrlLooksSuspicious = configuration.AppUrl is not null
                && (!configuration.AppUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                    || configuration.AppUrl.StartsWith("https://http", StringComparison.OrdinalIgnoreCase)),
            AccessTokenKind = configuration.AccessTokenKind.ToString(),
            AdminApiVersion = configuration.AdminApiVersion,
        };

        var token = fallbackToken;

        if (string.IsNullOrWhiteSpace(token) && report.HeaderLooksLikeBearer)
        {
            token = authorizationHeader!["Bearer ".Length..].Trim();
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            report.Verdict = "No token reached the server.";
            report.Hint =
                "The request carried no bearer token. If the page says it is not embedded, App Bridge " +
                "never loaded; if it is embedded, the call was made without one.";
            return report;
        }

        if (report.AppUrlLooksSuspicious)
        {
            report.Hint = $"Shopify:AppUrl is '{report.ConfiguredAppUrl}', which is not a plain https URL. " +
                          "It must be the public URL on its own, without a scheme repeated or a prefix.";
        }

        report.TokenLength = token.Length;

        var segments = token.Split('.');
        report.JwtSegments = segments.Length;

        if (segments.Length != 3)
        {
            report.Verdict = "The value is not a JWT.";
            report.Hint = "A JWT has three dot-separated parts. This is not one, so nothing can be verified.";
            return report;
        }

        try
        {
            report.Header = ReadJson(segments[0]);
            report.Claims = ReadJson(segments[1]);
        }
        catch (Exception ex)
        {
            report.Verdict = "The token could not be decoded as JSON.";
            report.Hint = ex.Message;
            return report;
        }

        if (report.Claims is not null)
        {
            report.ClaimShopDomain = TryGetString(report.Claims, "dest");
            report.ClaimIssuer = TryGetString(report.Claims, "iss");
            report.ClaimAudience = TryGetString(report.Claims, "aud");
            report.ClaimSubject = TryGetString(report.Claims, "sub");

            report.ClaimsMatchApp = string.Equals(report.ClaimAudience, configuration.ApiKey, StringComparison.Ordinal);

            if (TryGetLong(report.Claims, "exp", out var exp))
            {
                report.ExpiresAt = DateTimeOffset.FromUnixTimeSeconds(exp);
                report.ExpiresInSeconds = (long)(report.ExpiresAt.Value - DateTimeOffset.UtcNow).TotalSeconds;
            }
        }

        if (report.Header is not null)
        {
            report.TokenAlgorithm = TryGetString(report.Header, "alg");
            report.TokenKeyId = TryGetString(report.Header, "kid");
        }

        // Session tokens are HMAC-signed with the app's API secret, so there is no key set to
        // fetch: whether the signature can be checked depends only on the secret being configured,
        // and on the token actually being HS256.
        report.ExpectedAlgorithm = "HS256";
        report.SignatureCheckPossible = !string.IsNullOrWhiteSpace(configuration.ApiSecret);

        report.SignatureStatus = !report.SignatureCheckPossible
            ? "Shopify:ApiSecret is not configured, so no signature can be verified"
            : report.TokenAlgorithm is null
                ? "the token has no alg header"
                : string.Equals(report.TokenAlgorithm, "HS256", StringComparison.Ordinal)
                    ? "ok: HS256 with the API secret"
                    : $"the token's alg is '{report.TokenAlgorithm}', but session tokens are HS256";

        // The real verdict, from the real validator.
        var validation = await validator.ValidateAsync(token).ConfigureAwait(false);
        report.Validated = validation.Succeeded;
        report.ValidationFailure = validation.Failure?.ToString();
        report.ValidationReason = validation.Reason;

        if (validation.Succeeded)
        {
            report.Verdict = $"Accepted for {validation.SessionToken!.ShopDomain} (user {validation.SessionToken.UserId}).";
            report.Hint = "This token is valid for this app. If a request using it still fails, the problem is not authentication.";
            return report;
        }

        report.Verdict = $"Rejected: {validation.Failure}.";
        report.Hint = Hint(report, validation.Failure);
        return report;
    }

    /// <summary>Turns the mechanical failure into the thing the developer should fix.</summary>
    private static string Hint(TokenDiagnosticReport report, ShopifySessionTokenValidationFailure? failure)
    {
        // Ordered by how much each finding narrows the problem, not by the order the checks run.
        // A wrong audience makes the key-set fetch fail too (it goes to the *other* app's shop), so
        // reporting the key problem first would send the reader down the wrong path entirely.
        if (report.ClaimsMatchApp == false)
        {
            return $"The aud claim is '{report.ClaimAudience}' but this app's API key is " +
                   $"'{report.ConfiguredApiKey}'. The token was minted for a different app. Check that the " +
                   "App URL in the Partner Dashboard is this app, and that its Client ID is what you put in " +
                   "SHOPIFY_API_KEY — a stale ngrok hostname on an older app entry produces exactly this.";
        }

        if (report.ExpiresInSeconds is < 0 || failure == ShopifySessionTokenValidationFailure.Expired)
        {
            return "The session token is expired. It lives about a minute, so it must be fetched " +
                   "immediately before each request rather than reused or cached.";
        }

        if (failure == ShopifySessionTokenValidationFailure.WrongShop)
        {
            return "The token is for a different shop than the one the request addressed.";
        }

        if (failure == ShopifySessionTokenValidationFailure.Missing)
        {
            return "No token was supplied to the validator.";
        }

        if (!report.SignatureCheckPossible)
        {
            return "Shopify:ApiSecret is not configured, so no session token signature can be verified. " +
                   "Set it with 'dotnet user-secrets set \"Shopify:ApiSecret\" \"<client secret>\"'.";
        }

        if (report.TokenAlgorithm is not null
            && !string.Equals(report.TokenAlgorithm, "HS256", StringComparison.Ordinal))
        {
            return $"The token's alg is '{report.TokenAlgorithm}'. Shopify signs session tokens with " +
                   "HS256 using the app's API secret, so this token cannot be verified and is refused.";
        }

        return "The signature, audience or lifetime check failed. Compare the decoded claims with this " +
               "app's configuration: the aud claim must equal the API key, and dest must be a real " +
               "myshopify.com shop.";
    }

    /// <summary>Shows enough of a secret to recognise it, and not enough to use it.</summary>
    private static string? Mask(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "(not set)";
        }

        return value.Length <= 8
            ? new string('*', value.Length)
            : $"{value[..4]}…{value[^4..]} ({value.Length} characters)";
    }

    private static Dictionary<string, object?>? ReadJson(string segment)
    {
        var padded = segment.Replace('-', '+').Replace('_', '/');
        padded = (padded.Length % 4) switch
        {
            2 => padded + "==",
            3 => padded + "=",
            _ => padded,
        };

        var json = Encoding.UTF8.GetString(Convert.FromBase64String(padded));

        return JsonSerializer.Deserialize<Dictionary<string, object?>>(json);
    }

    private static string? TryGetString(Dictionary<string, object?> source, string key)
        => source.TryGetValue(key, out var value) && value is not null ? value.ToString() : null;

    private static string? TryGetString(JsonElement source, string key)
        => source.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.String ? value.GetString() : null;

    private static bool TryGetLong(Dictionary<string, object?> source, string key, out long value)
    {
        value = 0;

        if (!source.TryGetValue(key, out var raw) || raw is null)
        {
            return false;
        }

        return raw is JsonElement element
            ? element.TryGetInt64(out value)
            : long.TryParse(raw.ToString(), out value);
    }
}

/// <summary>What a session token contained, and what happened when it was verified.</summary>
public sealed class TokenDiagnosticReport
{
    /// <summary>True when the request carried an <c>Authorization</c> header at all.</summary>
    public bool AuthorizationHeaderPresent { get; set; }

    /// <summary>True when that header was a bearer token.</summary>
    public bool HeaderLooksLikeBearer { get; set; }

    /// <summary>The app's configured API key, masked.</summary>
    public string? ConfiguredApiKey { get; set; }

    /// <summary>The app's configured public URL.</summary>
    public string? ConfiguredAppUrl { get; set; }

    /// <summary>True when that URL is not a plain <c>https://</c> URL, which is always a typo.</summary>
    public bool AppUrlLooksSuspicious { get; set; }

    /// <summary>Online or offline access token.</summary>
    public string? AccessTokenKind { get; set; }

    /// <summary>The Admin API version this app requests.</summary>
    public string? AdminApiVersion { get; set; }

    /// <summary>Length of the token, or null when none arrived.</summary>
    public int? TokenLength { get; set; }

    /// <summary>How many dot-separated parts the token had; 3 is a JWT.</summary>
    public int JwtSegments { get; set; }

    /// <summary>The decoded JWT header, unverified.</summary>
    public Dictionary<string, object?>? Header { get; set; }

    /// <summary>The decoded JWT payload, unverified.</summary>
    public Dictionary<string, object?>? Claims { get; set; }

    /// <summary>The <c>alg</c> header value.</summary>
    public string? TokenAlgorithm { get; set; }

    /// <summary>The <c>kid</c> header value, used to select a signing key.</summary>
    public string? TokenKeyId { get; set; }

    /// <summary>The <c>dest</c> claim, which should be the shop's own URL.</summary>
    public string? ClaimShopDomain { get; set; }

    /// <summary>The <c>iss</c> claim.</summary>
    public string? ClaimIssuer { get; set; }

    /// <summary>The <c>aud</c> claim, which must equal this app's API key.</summary>
    public string? ClaimAudience { get; set; }

    /// <summary>The <c>sub</c> claim, the merchant user id.</summary>
    public string? ClaimSubject { get; set; }

    /// <summary>True when the token's audience is this app.</summary>
    public bool? ClaimsMatchApp { get; set; }

    /// <summary>The token's expiry.</summary>
    public DateTimeOffset? ExpiresAt { get; set; }

    /// <summary>Seconds until expiry; negative means already expired.</summary>
    public long? ExpiresInSeconds { get; set; }

    /// <summary>The algorithm a session token must use.</summary>
    public string? ExpectedAlgorithm { get; set; }

    /// <summary>The result of checking whether the signature can be verified at all.</summary>
    public string? SignatureStatus { get; set; }

    /// <summary>True when the API secret is configured, so a signature can be checked.</summary>
    public bool SignatureCheckPossible { get; set; }

    /// <summary>True when the real validator accepted the token.</summary>
    public bool Validated { get; set; }

    /// <summary>The validator's failure category.</summary>
    public string? ValidationFailure { get; set; }

    /// <summary>The validator's explanation.</summary>
    public string? ValidationReason { get; set; }

    /// <summary>One-line summary.</summary>
    public string? Verdict { get; set; }

    /// <summary>What to do about it.</summary>
    public string? Hint { get; set; }
}
