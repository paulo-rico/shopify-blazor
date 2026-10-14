using Microsoft.AspNetCore.Builder;

namespace Shopify.Blazor;

/// <summary>
/// Makes the app frameable by the Shopify Admin, and by nothing else.
/// </summary>
/// <remarks>
/// <para>
/// An embedded app <em>must</em> be frameable, and by default an ASP.NET Core app that renders
/// Blazor components is not. The anti-forgery middleware — which the Blazor rendering pipeline
/// requires — sets two headers on every response that asks for a token:
/// </para>
/// <list type="bullet">
/// <item><c>X-Frame-Options: SAMEORIGIN</c></item>
/// <item><c>Content-Security-Policy: frame-ancestors 'self'</c></item>
/// </list>
/// <para>
/// Both refuse the Admin's iframe. The browser reports
/// <c>Framing '…' violates the following Content Security Policy directive: "frame-ancestors 'self'"</c>,
/// and the app can never be embedded — while working perfectly in a normal tab, which is what makes
/// this hard to diagnose.
/// </para>
/// <para>
/// <strong>This does not weaken clickjacking protection; it re-aims it.</strong>
/// <c>X-Frame-Options</c> cannot express "these origins only" (<c>ALLOW-FROM</c> is dead and
/// unsupported), so the header is replaced by a <c>frame-ancestors</c> policy that names the Admin
/// origins explicitly. The app stays unframeable by any other site, which is the protection that
/// matters, and it is the replacement Shopify's own guidance points to.
/// </para>
/// <para>
/// Register it first, before anything that writes headers:
/// </para>
/// <code>
/// var app = builder.Build();
/// app.UseShopifyEmbeddedFraming();
/// </code>
/// </remarks>
public static class EmbeddedFramingExtensions
{
    /// <summary>Adds the framing policy an embedded Shopify app requires.</summary>
    /// <param name="app">The application builder.</param>
    /// <param name="configure">Optional configuration.</param>
    public static IApplicationBuilder UseShopifyEmbeddedFraming(
        this IApplicationBuilder app,
        Action<ShopifyEmbeddedFramingOptions>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(app);

        var options = new ShopifyEmbeddedFramingOptions();
        configure?.Invoke(options);

        var policy = "frame-ancestors " + string.Join(' ', options.Resolve());

        return app.Use(async (context, next) =>
        {
            // OnStarting callbacks run last-registered-first, and the anti-forgery middleware
            // registers its own during endpoint execution — later than this one. So this callback
            // runs *after* antiforgery has written its headers, which is the only ordering that
            // lets the framing policy be replaced rather than fought with.
            context.Response.OnStarting(() =>
            {
                context.Response.Headers.Remove("X-Frame-Options");

                // Only the frame-ancestors directive is set. Any other CSP the application has is
                // left alone: a policy that also sets default-src here would silently break the
                // app's own scripts, styles and connections.
                context.Response.Headers["Content-Security-Policy"] = policy;

                return Task.CompletedTask;
            });

            await next().ConfigureAwait(false);
        });
    }
}

/// <summary>Options for <see cref="EmbeddedFramingExtensions.UseShopifyEmbeddedFraming"/>.</summary>
public sealed class ShopifyEmbeddedFramingOptions
{
    private readonly List<string> _frameAncestors = [];

    /// <summary>
    /// Origins allowed to frame the app. Defaults to the Shopify Admin origins.
    /// </summary>
    /// <remarks>
    /// Add an entry only when the app genuinely must be embedded somewhere else — a custom admin
    /// host, for example. Each entry is an origin, not a path.
    /// </remarks>
    public IReadOnlyList<string> FrameAncestors => _frameAncestors;

    /// <summary>
    /// Also allows the common development tunnel hosts, for testing the embedded app from a tunnel.
    /// </summary>
    public bool AllowDevelopmentTunnels { get; set; }

    /// <summary>Allows one more origin to frame the app.</summary>
    /// <param name="origin">An origin such as <c>https://admin.shopify.com</c>.</param>
    public ShopifyEmbeddedFramingOptions AllowFrameAncestor(string origin)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(origin);

        if (!_frameAncestors.Contains(origin, StringComparer.OrdinalIgnoreCase))
        {
            _frameAncestors.Add(origin);
        }

        return this;
    }

    /// <summary>Applies <see cref="AllowDevelopmentTunnels"/>.</summary>
    internal IEnumerable<string> Resolve()
    {
        var origins = new List<string> { "https://admin.shopify.com", "https://*.myshopify.com" };

        if (AllowDevelopmentTunnels)
        {
            origins.Add("https://*.ngrok-free.app");
            origins.Add("https://*.ngrok-free.dev");
            origins.Add("https://*.trycloudflare.com");
        }

        foreach (var origin in _frameAncestors)
        {
            if (!origins.Contains(origin, StringComparer.OrdinalIgnoreCase))
            {
                origins.Add(origin);
            }
        }

        return origins;
    }
}
