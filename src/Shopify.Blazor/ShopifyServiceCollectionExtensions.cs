using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Shopify.Blazor.AdminApi;
using Shopify.Blazor.Authentication;

namespace Shopify.Blazor;

/// <summary>Registration helpers for the Shopify infrastructure.</summary>
public static class ShopifyServiceCollectionExtensions
{
    /// <summary>
    /// Registers Shopify configuration, session token validation, token exchange, the Admin API
    /// client and the App Bridge authentication scheme.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional inline configuration, applied after the configuration section.</param>
    /// <param name="configuration">Configuration read for the <c>Shopify</c> section.</param>
    /// <param name="validateOnStart">
    /// When true (the default) the API key and secret are required to be present at startup, so a
    /// misconfigured deployment fails immediately instead of on the first merchant request.
    /// </param>
    public static IServiceCollection AddShopifyBlazor(
        this IServiceCollection services,
        IConfiguration? configuration = null,
        Action<ShopifyOptions>? configure = null,
        bool validateOnStart = true)
    {
        var builder = services.AddOptions<ShopifyOptions>();

        if (configuration is not null)
        {
            builder.Bind(configuration.GetSection(ShopifyOptions.SectionName));
        }

        // Environment variables win over appsettings, which is what a container deployment needs.
        builder.Configure(options =>
        {
            options.ApiKey = Environment.GetEnvironmentVariable("SHOPIFY_API_KEY") ?? options.ApiKey;
            options.ApiSecret = Environment.GetEnvironmentVariable("SHOPIFY_API_SECRET") ?? options.ApiSecret;
            options.AppUrl = Environment.GetEnvironmentVariable("SHOPIFY_APP_URL") ?? options.AppUrl;
        });

        if (configure is not null)
        {
            builder.Configure(configure);
        }

        if (validateOnStart)
        {
            builder.Validate(o =>
            {
                o.Validate(requireSecret: true);
                return true;
            }, "Shopify configuration is incomplete: ApiKey and ApiSecret are both required on the server.");
        }

        services.AddMemoryCache();

        services.AddHttpClient(ShopifyHttpClients.Public, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        services.AddHttpClient(ShopifyHttpClients.TokenExchange, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(15);
        });

        services.AddHttpClient(ShopifyHttpClients.AdminApi, client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.TryAddSingleton<IShopifyAccessTokenStore, InMemoryShopifyAccessTokenStore>();
        services.TryAddSingleton<IShopifySessionTokenValidator, ShopifySessionTokenValidator>();
        services.TryAddSingleton<IShopifyTokenExchanger, ShopifyTokenExchanger>();
        services.TryAddSingleton<IAdminGraphQlClient, AdminGraphQlClient>();
        services.TryAddScoped<IShopifyRequestAuthenticator, ShopifyRequestAuthenticator>();
        services.TryAddScoped<IShopifyAdminApi, ShopifyAdminApi>();
        services.TryAddScoped<AppBridge.IAppBridgeInterop, AppBridge.AppBridgeInterop>();
        services.TryAddSingleton(Microsoft.Extensions.Options.Options.Create(new AppBridge.AppBridgeOptions()));

        if (configuration is not null)
        {
            services.Configure<AppBridge.AppBridgeOptions>(configuration.GetSection("Shopify:AppBridge"));
        }

        services
            .AddAuthentication(ShopifyAuthenticationDefaults.Scheme)
            .AddScheme<ShopifyAuthenticationOptions, ShopifySessionTokenAuthenticationHandler>(
                ShopifyAuthenticationDefaults.Scheme,
                _ => { });

        services.AddAuthorization();

        return services;
    }
}
