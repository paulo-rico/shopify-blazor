using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Http;
using Xunit;

namespace Shopify.Blazor.Tests;

/// <summary>
/// Tests for the framing policy an embedded app requires.
/// </summary>
/// <remarks>
/// This exists because the failure it prevents is invisible: the app renders perfectly in a browser
/// tab and can never be embedded in the Shopify Admin, because the anti-forgery middleware that
/// Blazor's rendering pipeline requires sets <c>X-Frame-Options: SAMEORIGIN</c> and
/// <c>frame-ancestors 'self'</c>. The browser reports a CSP violation and shows an empty frame; the
/// server log says nothing at all.
/// </remarks>
public sealed class EmbeddedFramingTests
{
    /// <summary>
    /// Starts a response for a request and returns the headers that would be sent, after the
    /// app's OnStarting callbacks have run.
    /// </summary>
    private static async Task<IHeaderDictionary> RunAsync(
        Action<ShopifyEmbeddedFramingOptions>? configure = null,
        Action<HttpContext>? duringRequest = null)
    {
        var (context, responseFeature) = TestHttpContext.Create();

        // Simulate the anti-forgery middleware, which writes these during endpoint execution —
        // after the app's middleware has registered its callback.
        duringRequest ??= ctx =>
        {
            ctx.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
            ctx.Response.Headers["Content-Security-Policy"] = "frame-ancestors 'self'";
        };

        var services = new Microsoft.Extensions.DependencyInjection.ServiceCollection()
            .BuildServiceProvider();

        // `IApplicationBuilder.Use` returns a *new* builder rather than mutating this one, so the
        // result has to be carried forward. Discarding it silently drops the middleware, and the
        // test then asserts against headers nothing had touched.
        // `IApplicationBuilder` throughout: Use returns that interface, not the concrete type, and
        // earlier attempts to keep an ApplicationBuilder dropped the middleware on the floor.
        IApplicationBuilder builder = new ApplicationBuilder(services);

        builder = builder.UseShopifyEmbeddedFraming(configure);
        builder.Run(ctx =>
        {
            duringRequest(ctx);
            return Task.CompletedTask;
        });

        await builder.Build()(context);

        // A real server starts the response after the pipeline returns and runs the OnStarting
        // callbacks newest-first. DefaultHttpContext does not do that, so a feature that does is
        // installed by TestHttpContext.Create.
        await responseFeature.StartAsync();

        return context.Response.Headers;
    }

    [Fact]
    public async Task The_framing_headers_antiforgery_sets_are_replaced()
    {
        var headers = await RunAsync();

        // X-Frame-Options cannot express "these origins only", so it is removed in favour of a
        // frame-ancestors policy. Leaving it would block the Admin no matter what the CSP said.
        Assert.False(headers.ContainsKey("X-Frame-Options"));

        var policy = headers["Content-Security-Policy"].ToString();
        Assert.Contains("frame-ancestors", policy, StringComparison.Ordinal);
        Assert.DoesNotContain("'self'", policy, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_admin_origins_are_allowed()
    {
        var policy = (await RunAsync())["Content-Security-Policy"].ToString();

        Assert.Contains("https://admin.shopify.com", policy, StringComparison.Ordinal);
        Assert.Contains("https://*.myshopify.com", policy, StringComparison.Ordinal);
    }

    [Fact]
    public async Task An_arbitrary_site_is_not_allowed_to_frame_the_app()
    {
        // The protection that matters: re-aiming the policy must not open the app to clickjacking
        // from anywhere. Only the named origins are permitted.
        var policy = (await RunAsync())["Content-Security-Policy"].ToString();

        Assert.DoesNotContain("*", policy.Replace("https://*.myshopify.com", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        Assert.DoesNotContain("https://evil.example.com", policy, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_default_policy_names_only_the_admin_origins()
    {
        // The app is frameable by the Admin and by nothing else. A wildcard for the common
        // development tunnel hosts used to be added here behind a flag; the flag is gone, and this
        // test keeps the policy from growing wildcards again. An origin that fronts the app during
        // development is an ordinary HTTPS origin, added explicitly with AllowFrameAncestor — see
        // the test below.
        var policy = (await RunAsync())["Content-Security-Policy"].ToString();

        Assert.Contains("https://admin.shopify.com", policy, StringComparison.Ordinal);

        // Exactly one wildcard is permitted, and it is Shopify's own.
        var wildcards = policy
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Where(token => token.Contains('*', StringComparison.Ordinal))
            .ToArray();

        Assert.Equal(["https://*.myshopify.com"], wildcards);
    }

    [Fact]
    public async Task An_extra_origin_can_be_allowed()
    {
        var policy = (await RunAsync(options => options.AllowFrameAncestor("https://admin.example.com")))
            ["Content-Security-Policy"].ToString();

        Assert.Contains("https://admin.example.com", policy, StringComparison.Ordinal);

        // And the Admin still works, because adding an origin must not replace the defaults.
        Assert.Contains("https://admin.shopify.com", policy, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Any_other_content_security_policy_the_app_set_is_left_alone()
    {
        // The middleware owns exactly one directive. Replacing a whole policy would silently break
        // an app's scripts, styles and connections.
        var headers = await RunAsync(duringRequest: ctx =>
        {
            ctx.Response.Headers["X-Frame-Options"] = "SAMEORIGIN";
            ctx.Response.Headers["Content-Security-Policy"] = "frame-ancestors 'self'; default-src 'none'";
        });

        var policy = headers["Content-Security-Policy"].ToString();

        Assert.Contains("frame-ancestors", policy, StringComparison.Ordinal);
        Assert.Contains("https://admin.shopify.com", policy, StringComparison.Ordinal);
    }

    [Fact]
    public async Task The_options_object_is_fluent_and_deduplicates()
    {
        var options = new ShopifyEmbeddedFramingOptions()
            .AllowFrameAncestor("https://admin.example.com")
            .AllowFrameAncestor("https://admin.example.com")
            .AllowFrameAncestor("HTTPS://ADMIN.EXAMPLE.COM");

        Assert.Single(options.FrameAncestors);
    }
}
