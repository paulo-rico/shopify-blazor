using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Shopify.Blazor.Diagnostics;

/// <summary>
/// Logs where the Shopify configuration came from and whether it is usable.
/// </summary>
/// <remarks>
/// <para>
/// Configuration precedence is the cause of a whole class of "it works in my shell but not from
/// here" failures, and it is invisible: <c>Shopify:ApiKey</c> can come from <c>appsettings.json</c>,
/// <c>appsettings.Development.json</c>, user secrets, or an environment variable, and an environment
/// variable silently wins over user secrets. When the value that wins belongs to a <em>different</em>
/// Shopify app, every session token is rejected as being for someone else, and nothing anywhere says
/// which value was used.
/// </para>
/// <para>
/// The API key is public — it is rendered into the page so App Bridge can read it — so it is logged
/// in full. The secret is not, and only its presence and length are reported.
/// </para>
/// </remarks>
public static class ShopifyConfigurationDiagnostics
{
    /// <summary>
    /// Names the provider that supplied the API key.
    /// </summary>
    /// <remarks>
    /// "configuration" is not an answer: an API key in a committed <c>appsettings.json</c> is a
    /// different situation from one in user secrets or a key vault, and only the latter are safe to
    /// keep there.
    /// </remarks>
    private static string DescribeConfigProvider(WebApplication app, string value)
    {
        // The secret store is registered as an IConfigurationProvider by WebApplicationBuilder. If
        // the app's content root has a secrets id, a value that is not in appsettings came from
        // there; otherwise it is in a file.
        var settings = app.Configuration.GetSection(ShopifyOptions.SectionName);

        foreach (var provider in ((IConfigurationRoot)app.Configuration).Providers)
        {
            if (provider.TryGet($"{ShopifyOptions.SectionName}:ApiKey", out var fromProvider)
                && string.Equals(fromProvider, value, StringComparison.Ordinal))
            {
                var name = provider.GetType().Name;

                return name.Contains("Secrets", StringComparison.Ordinal)
                    ? "the user secrets store"
                    : name.Contains("Environment", StringComparison.Ordinal)
                        ? "the SHOPIFY_API_KEY environment variable"
                        : $"the configuration provider '{name}'";
            }
        }

        return settings.Exists() ? "configuration" : "an unknown source";
    }

    /// <summary>
    /// Writes one summary line per configuration value at startup.
    /// </summary>
    /// <param name="app">The application, after building.</param>
    /// <param name="configuration">The same configuration the app was built from.</param>
    public static void LogShopifyConfiguration(this WebApplication app, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(configuration);

        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("Shopify.Configuration");
        var options = app.Services.GetRequiredService<IOptions<ShopifyOptions>>().Value;

        if (string.IsNullOrWhiteSpace(options.ApiKey))
        {
            logger.LogError(
                "Shopify:ApiKey is not set. Session tokens cannot be verified and the app will not work. " +
                "Set it with 'dotnet user-secrets set \"Shopify:ApiKey\" \"<client id>\"' or SHOPIFY_API_KEY.");

            return;
        }

        // Which layer actually supplied the key. This is the line that explains a mismatch.
        var environmentKey = Environment.GetEnvironmentVariable("SHOPIFY_API_KEY");
        var fromEnvironment = !string.IsNullOrWhiteSpace(environmentKey)
            && string.Equals(environmentKey, options.ApiKey, StringComparison.Ordinal);

        // The environment variable is applied last in AddShopifyBlazor, so if it matches the
        // effective value it is what won. Otherwise the value came from a provider-backed source,
        // and finding out whether that was a committed file or a secret store matters: one is safe
        // to keep, the other is not.
        var configuredKey = configuration["Shopify:ApiKey"];
        var fromConfiguration = !string.IsNullOrWhiteSpace(configuredKey)
            && string.Equals(configuredKey, options.ApiKey, StringComparison.Ordinal);

        var source = fromEnvironment
            ? "the SHOPIFY_API_KEY environment variable"
            : fromConfiguration
                ? DescribeConfigProvider(app, configuredKey!)
                : "an unknown source";

        if (fromEnvironment)
        {
            logger.LogWarning(
                "Shopify:ApiKey came from {Source}. An environment variable overrides user secrets, so if " +
                "this is not the app whose App URL is open in the Admin, every session token will be rejected " +
                "as belonging to another app. Unset SHOPIFY_API_KEY to fall back to user secrets.",
                source);
        }
        else
        {
            logger.LogInformation("Shopify:ApiKey came from {Source}.", source);
        }

        logger.LogInformation(
            "Shopify app: ApiKey={ApiKey} Secret={Secret} AppUrl={AppUrl} AccessTokenKind={Kind} AdminApiVersion={Version}",
            options.ApiKey,
            string.IsNullOrWhiteSpace(options.ApiSecret)
                ? "NOT SET (token exchange will fail)"
                : $"set ({options.ApiSecret.Length} characters)",
            string.IsNullOrWhiteSpace(options.AppUrl) ? "(not set)" : options.AppUrl,
            options.AccessTokenKind,
            options.AdminApiVersion);

        if (string.IsNullOrWhiteSpace(options.ApiSecret))
        {
            logger.LogError(
                "Shopify:ApiSecret is not set, so no session token can be exchanged for an access token. " +
                "Set it with 'dotnet user-secrets set \"Shopify:ApiSecret\" \"<client secret>\"'.");
        }

        // The value the browser will be told, which must match the one the server verifies against.
        logger.LogInformation(
            "The page will render <meta name=\"shopify-api-key\" content=\"{ApiKey}\" />. The Admin must have " +
            "loaded the Shopify app whose Client ID is this value, or App Bridge will mint tokens for a " +
            "different app and this server will reject them.",
            options.ApiKey);
    }
}
