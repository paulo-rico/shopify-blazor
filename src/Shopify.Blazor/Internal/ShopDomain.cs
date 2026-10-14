namespace Shopify.Blazor.Internal;

/// <summary>
/// The single place shop domains are validated.
/// </summary>
/// <remarks>
/// <para>
/// Every value that reaches an outbound URL or a token cache key goes through here, because a shop
/// domain is attacker-supplied: it arrives in the <c>dest</c> claim of a session token and is then
/// interpolated into <c>https://{shop}/admin/oauth/access_token</c>, a request that carries the
/// app secret. Without this check a crafted domain could redirect the token exchange, and the
/// secret with it, to another host.
/// </para>
/// <para>
/// The input is not always a bare host. Shopify's <c>dest</c> claim is a full URL such as
/// <c>https://shop.myshopify.com</c>, and older documentation shows bare names such as
/// <c>shop.myshopify.com</c> or even <c>shop</c>. All three forms are accepted; anything else,
/// including a URL whose effective host merely ends in the suffix, is not.
/// </para>
/// </remarks>
internal static class ShopDomain
{
    /// <summary>The suffix every Shopify shop domain ends with.</summary>
    public const string MyshopifySuffix = ".myshopify.com";

    /// <summary>Longest a shop's own label may be.</summary>
    private const int MaxLabelLength = 60;

    /// <summary>
    /// Returns the canonical lower-case <c>name.myshopify.com</c> form, or <c>null</c> when the
    /// value is not a usable myshopify shop.
    /// </summary>
    public static string? TryNormalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var candidate = value.Trim();

        // A bare shop name, which Shopify itself accepts in several places. Without this the value
        // would not parse as a URL at all.
        if (!candidate.Contains('.', StringComparison.Ordinal))
        {
            return IsValidLabel(candidate) ? candidate.ToLowerInvariant() + MyshopifySuffix : null;
        }

        if (!candidate.Contains("://", StringComparison.Ordinal))
        {
            candidate = "https://" + candidate;
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var uri))
        {
            return null;
        }

        // Only a plain DNS host is acceptable. UriHostNameType also reports IPv4, IPv6 and
        // "basic" for forms such as `http://user@host`; all of those are rejected outright rather
        // than reasoned about.
        if (uri.HostNameType != UriHostNameType.Dns)
        {
            return null;
        }

        if (uri.Scheme is not ("http" or "https"))
        {
            return null;
        }

        // `uri.Host` is already normalised: lower-case, with any user-info stripped and no port.
        var host = uri.Host;

        if (!host.EndsWith(MyshopifySuffix, StringComparison.Ordinal) || host.Length <= MyshopifySuffix.Length)
        {
            return null;
        }

        var label = host[..^MyshopifySuffix.Length];

        return IsValidLabel(label) ? host : null;
    }

    /// <summary>
    /// Validates the part before <c>.myshopify.com</c>.
    /// </summary>
    /// <remarks>
    /// Deliberately strict. Anything containing a slash, colon, percent, at-sign or whitespace is
    /// rejected, which is what stops a value such as <c>shop.myshopify.com.evil.example.com</c> or
    /// <c>shop%2f..%2fevil.myshopify.com</c> from reaching an interpolated URL.
    /// </remarks>
    private static bool IsValidLabel(string label)
    {
        if (label.Length is 0 or > MaxLabelLength)
        {
            return false;
        }

        if (label.StartsWith('-') || label.EndsWith('-') || label.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        foreach (var c in label)
        {
            var allowed = c is >= 'a' and <= 'z' or >= 'A' and <= 'Z' or >= '0' and <= '9' or '-' or '.';

            if (!allowed)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Returns the canonical form or throws, for call sites with no sensible fallback.</summary>
    public static string Normalize(string? value)
        => TryNormalize(value) ?? throw new ArgumentException($"'{value}' is not a valid myshopify.com shop domain.", nameof(value));
}
