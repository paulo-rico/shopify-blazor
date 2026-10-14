using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Polaris.Blazor.Interop;

namespace Polaris.Blazor;

/// <summary>Registration helper for the Polaris component library.</summary>
public static class PolarisServiceCollectionExtensions
{
    /// <summary>
    /// Registers the JavaScript interop layer the components depend on.
    /// </summary>
    /// <remarks>
    /// The library has no other service dependencies by design: it wraps Shopify's components and
    /// nothing else, so an app can adopt it without adopting the rest of the toolkit.
    /// </remarks>
    /// <param name="services">The service collection.</param>
    /// <param name="configure">Optional interop configuration.</param>
    public static IServiceCollection AddPolarisBlazor(this IServiceCollection services, Action<PolarisInteropOptions>? configure = null)
    {
        services.AddOptions<PolarisInteropOptions>();

        if (configure is not null)
        {
            services.Configure(configure);
        }

        services.TryAddScoped<IPolarisInterop, PolarisInterop>();

        return services;
    }
}
