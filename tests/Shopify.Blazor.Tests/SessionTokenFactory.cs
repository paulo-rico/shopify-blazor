using System.Text;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Shopify.Blazor.Tests;

/// <summary>
/// Mints session tokens the way Shopify does: HMAC-SHA256 signed with the app's API secret.
/// </summary>
/// <remarks>
/// <para>
/// The algorithm is the point of this class. Shopify signs admin session tokens with the app's
/// client secret — symmetric — so there is no key set and no per-shop public key. An earlier
/// version of these tests signed RS256 tokens against a throwaway RSA key and a fake JWKS, and it
/// passed against an equally wrong validator: both were built on the belief that
/// <c>/.well-known/jwks.json</c> serves admin session token keys. It does not. That endpoint
/// belongs to the Storefront and Customer Account APIs, and it sits behind the storefront, which
/// every dev store password-protects.
/// </para>
/// <para>
/// So a valid token here is signed with <see cref="ApiSecret"/>, and the validator must accept it
/// with nothing but that secret. If these tests pass, they pass for the same reason the real app
/// works.
/// </para>
/// </remarks>
public static class SessionTokenFactory
{
    /// <summary>The default shop these tokens are issued for.</summary>
    public const string DefaultShop = "test-shop.myshopify.com";

    /// <summary>The audience, which is the app's API key. Must match <c>TestServices</c>.</summary>
    public const string DefaultAudience = "test-api-key";

    /// <summary>
    /// The signing secret. Must match the <c>Shopify:ApiSecret</c> that <c>TestServices</c>
    /// configures, because that is the key the validator checks the signature with.
    /// </summary>
    public const string ApiSecret = "0123456789abcdef0123456789abcdef";

    /// <summary>Creates a signed session token that the validator should accept.</summary>
    /// <param name="shop">The shop the token is destined for.</param>
    /// <param name="audience">The app's API key.</param>
    /// <param name="userId">The merchant user id.</param>
    /// <param name="expiresIn">How long the token is valid for; negative makes it expired.</param>
    /// <param name="scopes">Scopes to carry in the <c>scp</c> claim.</param>
    /// <param name="secret">The signing secret; a different value simulates another app's token.</param>
    /// <param name="algorithm">The algorithm to sign with; used to prove non-HS256 is refused.</param>
    public static string Create(
        string shop = DefaultShop,
        string audience = DefaultAudience,
        string userId = "1234567890",
        TimeSpan? expiresIn = null,
        string? scopes = null,
        string secret = ApiSecret,
        string algorithm = SecurityAlgorithms.HmacSha256)
    {
        var now = DateTime.UtcNow;
        var lifetime = expiresIn ?? TimeSpan.FromMinutes(1);

        // For an expired token the whole window has to sit in the past: setting nbf after exp makes
        // the token malformed, and it would then be rejected for the wrong reason.
        var notBefore = lifetime < TimeSpan.Zero ? now.Add(lifetime).AddMinutes(-1) : now.AddMinutes(-1);

        var claims = new Dictionary<string, object>
        {
            ["sub"] = userId,
            ["dest"] = $"https://{shop}",
            ["iss"] = $"https://{shop}/admin",
            ["aud"] = audience,
            ["jti"] = "fixed-jti",
            ["sid"] = "fixed-sid",
        };

        if (scopes is not null)
        {
            claims["scp"] = scopes;
        }

        var descriptor = new SecurityTokenDescriptor
        {
            IssuedAt = now.Add(lifetime < TimeSpan.Zero ? lifetime : TimeSpan.Zero),
            NotBefore = notBefore,
            Expires = now.Add(lifetime),
            Claims = claims,
            SigningCredentials = CreateCredentials(algorithm, secret),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    /// <summary>
    /// Creates a correctly formed token signed with a different, valid-length secret.
    /// </summary>
    /// <remarks>
    /// The length matters: HMAC-SHA256 needs at least 256 bits, so a short "wrong secret" would be
    /// rejected for being unusable rather than for being wrong, and the test would pass while
    /// proving nothing about signature checking.
    /// </remarks>
    public static string CreateForgedToken(string shop = DefaultShop, string audience = DefaultAudience)
        => Create(shop: shop, audience: audience, secret: "ffffffffffffffffffffffffffffffff");

    /// <summary>
    /// Creates an unsigned token with <c>alg: none</c>, the classic downgrade attempt.
    /// </summary>
    public static string CreateUnsignedToken(string shop = DefaultShop, string audience = DefaultAudience)
    {
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();

        // Written by hand: no serializer produces an unsigned JWT for you, which is exactly why the
        // attack is worth testing for.
        var header = Base64Url("""{"alg":"none","typ":"JWT"}""");

        var payload = Base64Url(JsonSerializer.Serialize(new Dictionary<string, object>
        {
            ["sub"] = "1234567890",
            ["dest"] = $"https://{shop}",
            ["iss"] = $"https://{shop}/admin",
            ["aud"] = audience,
            ["iat"] = now - 5,
            ["nbf"] = now - 5,
            ["exp"] = now + 60,
        }));

        return $"{header}.{payload}.";
    }

    /// <summary>Creates a token whose <c>dest</c> claim is not a myshopify domain.</summary>
    public static string CreateHostileDestToken(string destination)
    {
        var now = DateTime.UtcNow;

        var descriptor = new SecurityTokenDescriptor
        {
            IssuedAt = now,
            NotBefore = now.AddMinutes(-1),
            Expires = now.AddMinutes(1),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = "1234567890",
                ["dest"] = destination,
                ["iss"] = $"https://{DefaultShop}/admin",
                ["aud"] = DefaultAudience,
            },
            SigningCredentials = CreateCredentials(SecurityAlgorithms.HmacSha256, ApiSecret),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }

    private static SigningCredentials CreateCredentials(string algorithm, string secret) => algorithm switch
    {
        SecurityAlgorithms.HmacSha256 => new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret)),
            SecurityAlgorithms.HmacSha256),

        // For the "an asymmetric token must be refused" case. The key is throwaway and its signature
        // can never verify against a symmetric secret, which is the point.
        SecurityAlgorithms.RsaSha256 => new SigningCredentials(
            new RsaSecurityKey(System.Security.Cryptography.RSA.Create(2048)) { KeyId = "throwaway" },
            SecurityAlgorithms.RsaSha256),

        _ => throw new ArgumentOutOfRangeException(nameof(algorithm), algorithm, "Unsupported test algorithm."),
    };

    private static string Base64Url(string json)
        => Base64UrlEncoder.Encode(Encoding.UTF8.GetBytes(json));
}
