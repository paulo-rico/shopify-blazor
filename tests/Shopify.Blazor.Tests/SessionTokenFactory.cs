using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace Shopify.Blazor.Tests;

/// <summary>
/// Mints session tokens the way Shopify does, using a throwaway RSA key.
/// </summary>
/// <remarks>
/// <para>
/// This is the only honest way to test session token validation without a real store: the
/// validator must reject a token whose signature does not verify against the key set it fetched,
/// and the only way to prove it accepts a valid one is to sign it properly.
/// </para>
/// <para>
/// The key set is served from a fake <c>.well-known/jwks.json</c> through the scripted HTTP
/// handler, so the whole path — parse the unvalidated <c>dest</c>, fetch that shop's keys, verify
/// signature, audience, issuer and lifetime — runs for real.
/// </para>
/// </remarks>
public sealed class SessionTokenFactory : IDisposable
{
    private readonly RSA _rsa = System.Security.Cryptography.RSA.Create(2048);
    private readonly RsaSecurityKey _signingKey;

    /// <summary>Creates a factory with a fresh key pair.</summary>
    public SessionTokenFactory()
    {
        _signingKey = new RsaSecurityKey(_rsa) { KeyId = "test-key-1" };
    }

    /// <summary>The default shop domain these tokens are issued for.</summary>
    public const string DefaultShop = "test-shop.myshopify.com";

    /// <summary>The audience, which is the app's API key.</summary>
    public const string DefaultAudience = "test-api-key";

    /// <summary>
    /// The JWKS document for the public key, in the shape Shopify publishes. Register it against
    /// the host the validator will fetch from.
    /// </summary>
    public string BuildJwks()
    {
        var parameters = _rsa.ExportParameters(includePrivateParameters: false);

        var jwk = new Dictionary<string, object?>
        {
            ["kty"] = "RSA",
            ["use"] = "sig",
            ["alg"] = "RS256",
            ["kid"] = _signingKey.KeyId,
            ["n"] = Base64UrlEncoder.Encode(parameters.Modulus!),
            ["e"] = Base64UrlEncoder.Encode(parameters.Exponent!),
        };

        return JsonSerializer.Serialize(new { keys = new[] { jwk } });
    }

    /// <summary>Creates a signed session token.</summary>
    /// <param name="shop">The shop the token is destined for.</param>
    /// <param name="audience">The app's API key.</param>
    /// <param name="userId">The merchant user id.</param>
    /// <param name="expiresIn">How long the token is valid for; negative makes it expired.</param>
    /// <param name="scopes">Scopes to carry in the <c>scp</c> claim.</param>
    /// <param name="tokenId">
    /// The <c>jti</c> claim. Fixed by default so two calls with the same arguments produce the
    /// same token, which is what lets a test reuse one session across several requests — the
    /// access-token cache is keyed by the subject, so a different jti per call would look like a
    /// different merchant.
    /// </param>
    /// <param name="sessionId">The <c>sid</c> claim.</param>
    public string Create(
        string shop = DefaultShop,
        string audience = DefaultAudience,
        string userId = "1234567890",
        TimeSpan? expiresIn = null,
        string? scopes = null,
        string tokenId = "fixed-jti",
        string sessionId = "fixed-sid")
    {
        var now = DateTime.UtcNow;
        var lifetime = expiresIn ?? TimeSpan.FromMinutes(1);

        // The not-before instant is derived from the expiry rather than fixed, so an expired token
        // is coherently expired. A token that is not valid yet AND expired fails validation with a
        // lifetime error that is indistinguishable from a clock problem, which would hide the very
        // thing the expiry test is checking.
        var notBefore = lifetime < TimeSpan.Zero ? now.Add(lifetime) : now.AddMinutes(-1);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = $"https://{shop}/admin",
            Audience = audience,
            IssuedAt = now.Add(lifetime < TimeSpan.Zero ? lifetime : TimeSpan.Zero),
            NotBefore = notBefore,
            Expires = now.Add(lifetime),
            SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = userId,
                ["dest"] = $"https://{shop}",
                ["iss"] = $"https://{shop}/admin",
                ["aud"] = audience,
                ["jti"] = tokenId,
                ["sid"] = sessionId,
            },
        };

        if (scopes is not null)
        {
            descriptor.Claims["scp"] = scopes;
        }

        return CreateTokenWithKid(descriptor, _signingKey.KeyId!);
    }

    /// <summary>
    /// Creates a correctly signed token whose <c>dest</c> claim points somewhere that is not a
    /// myshopify domain, signed with this factory's key so only the domain check can reject it.
    /// </summary>
    public string CreateHostileDestToken(string destination)
    {
        var now = DateTime.UtcNow;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = $"https://{DefaultShop}/admin",
            Audience = DefaultAudience,
            IssuedAt = now,
            NotBefore = now.AddMinutes(-1),
            Expires = now.Add(TimeSpan.FromMinutes(1)),
            SigningCredentials = new SigningCredentials(_signingKey, SecurityAlgorithms.RsaSha256),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = "1234567890",
                ["dest"] = destination,
            },
        };

        return CreateTokenWithKid(descriptor, _signingKey.KeyId!);
    }

    /// <summary>Creates a token signed with a different key, so its signature cannot verify.</summary>
    public static string CreateForgedToken(string shop = DefaultShop, string audience = DefaultAudience)
    {
        using var otherRsa = System.Security.Cryptography.RSA.Create(2048);
        var now = DateTime.UtcNow;

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = $"https://{shop}/admin",
            Audience = audience,
            IssuedAt = now,
            NotBefore = now.AddMinutes(-1),
            Expires = now.Add(TimeSpan.FromMinutes(1)),
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(otherRsa) { KeyId = "test-key-1" }, SecurityAlgorithms.RsaSha256),
            Claims = new Dictionary<string, object>
            {
                ["sub"] = "1234567890",
                ["dest"] = $"https://{shop}",
            },
        };

        return CreateTokenWithKid(descriptor, "test-key-1");
    }

    /// <summary>
    /// Writes the JWT header by hand so it carries a <c>kid</c>.
    /// </summary>
    /// <remarks>
    /// <see cref="SecurityTokenDescriptor.AdditionalHeaderClaims"/> refuses <c>kid</c> and the
    /// handler does not emit the signing key's id on its own. Real Shopify tokens carry one, and a
    /// key set with more than one entry cannot be resolved without it.
    /// </remarks>
    private static string CreateTokenWithKid(SecurityTokenDescriptor descriptor, string kid)
    {
        var header = new Dictionary<string, object>
        {
            ["alg"] = SecurityAlgorithms.RsaSha256,
            ["kid"] = kid,
            ["typ"] = "JWT",
        };

        var payload = new Dictionary<string, object>();

        foreach (var claim in descriptor.Claims ?? new Dictionary<string, object>())
        {
            payload[claim.Key] = claim.Value;
        }

        var now = (descriptor.IssuedAt ?? DateTime.UtcNow).ToUniversalTime();
        var expires = (descriptor.Expires ?? now.AddMinutes(5)).ToUniversalTime();
        var notBefore = (descriptor.NotBefore ?? now).ToUniversalTime();

        payload["iat"] = EpochTime.GetIntDate(now);
        payload["nbf"] = EpochTime.GetIntDate(notBefore);
        payload["exp"] = EpochTime.GetIntDate(expires);
        payload["iss"] = descriptor.Issuer!;
        payload["aud"] = descriptor.Audience!;

        var encodedHeader = Base64UrlEncoder.Encode(JsonSerializer.Serialize(header));
        var encodedPayload = Base64UrlEncoder.Encode(JsonSerializer.Serialize(payload));
        var signingInput = $"{encodedHeader}.{encodedPayload}";

        var signature = ((RsaSecurityKey)descriptor.SigningCredentials!.Key)
            .Rsa
            .SignData(System.Text.Encoding.ASCII.GetBytes(signingInput), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);

        return $"{signingInput}.{Base64UrlEncoder.Encode(signature)}";
    }

    /// <inheritdoc />
    public void Dispose() => _rsa.Dispose();
}
