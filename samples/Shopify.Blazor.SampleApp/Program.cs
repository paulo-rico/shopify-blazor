using Microsoft.AspNetCore.Mvc;
using Polaris.Blazor;
using Shopify.Blazor;
using Shopify.Blazor.Diagnostics;
using Shopify.Blazor.SampleApp.Components;
using Shopify.Blazor.SampleApp.Services;

var builder = WebApplication.CreateBuilder(args);

// Blazor Web App with Interactive Server. See docs/adr/0001-hosting-model.md for why.
builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

// Shopify infrastructure: configuration, session token validation, token exchange, the Admin API
// client and the App Bridge authentication scheme.
//
// Startup validation is skipped when no API key is configured yet, so a developer can clone the
// repository and run the app before creating a Shopify app. Nothing that needs the key will work,
// and the app says so on screen rather than failing at boot with no explanation.
var shopifyApiKey = builder.Configuration["Shopify:ApiKey"]
    ?? Environment.GetEnvironmentVariable("SHOPIFY_API_KEY");

var shopifyConfigured = !string.IsNullOrWhiteSpace(shopifyApiKey)
    && !string.IsNullOrWhiteSpace(
        builder.Configuration["Shopify:ApiSecret"] ?? Environment.GetEnvironmentVariable("SHOPIFY_API_SECRET"));

builder.Services.AddShopifyBlazor(builder.Configuration, validateOnStart: shopifyConfigured);

// In Development a rejected session token answers with the reason, so the page can show
// "the aud claim is for a different app" instead of a bare "Unauthorized". Off elsewhere: a
// production response should not tell an unauthenticated client why its credential failed.
builder.Services.Configure<Shopify.Blazor.Authentication.ShopifyAuthenticationOptions>(options =>
{
    options.IncludeFailureDetail = builder.Environment.IsDevelopment();
});

// The Polaris component library's JavaScript interop layer.
builder.Services.AddPolarisBlazor();

builder.Services.AddProblemDetails();
builder.Services.AddScoped<ProductsService>();

var app = builder.Build();

// Says which configuration layer supplied the API key, because an environment variable silently
// overrides user secrets and a key from a different Shopify app rejects every session token.
app.LogShopifyConfiguration(builder.Configuration);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// Shopify embeds the app in an iframe, so the app has to permit framing — but only by the Admin.
// The anti-forgery middleware that Blazor's rendering pipeline requires sets X-Frame-Options and a
// frame-ancestors policy that refuse it, which makes the app impossible to embed while looking
// perfect in a normal tab. This replaces those headers with a policy naming the Admin origins.
//
// Registered first, on purpose: its OnStarting callback has to run *after* antiforgery has written
// its own headers, and OnStarting callbacks run last-registered-first.
// Before authorization, so the "no Authorization header" case is recorded too.
app.UseMiddleware<RequestObservationMiddleware>();

app.UseShopifyEmbeddedFraming(options =>
{
    // The tunnel hostname is how a developer opens the embedded app during setup. It is not a
    // production origin, so it is only allowed while running in Development.
    options.AllowDevelopmentTunnels = app.Environment.IsDevelopment();

    // Lets a locally served page frame the app during development, which is how the framing policy
    // is verified without a development store. Set EMBED_PROBE_ORIGIN to an origin such as
    // http://localhost:3479. Never set in a deployed app.
    if (app.Environment.IsDevelopment()
        && Environment.GetEnvironmentVariable("EMBED_PROBE_ORIGIN") is { Length: > 0 } probeOrigin)
    {
        options.AllowFrameAncestor(probeOrigin);
    }
});

app.UseStatusCodePages();
app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Development-only diagnostics. Answers "which token arrived, and why was it rejected?", which the
// generic 401 cannot. Never mapped outside Development: it decodes tokens without verifying them,
// so it is a debugging aid and not part of the app's security surface.
if (app.Environment.IsDevelopment())
{
    app.MapGet("/api/diagnostics/session-token", async (
            HttpContext context,
            IHttpClientFactory httpClientFactory,
            Microsoft.Extensions.Options.IOptionsMonitor<ShopifyOptions> options,
            Shopify.Blazor.Authentication.IShopifySessionTokenValidator validator,
            string? token) => Results.Ok(await Shopify.Blazor.Diagnostics.SessionTokenDiagnostics.InspectAsync(
                httpClientFactory,
                options,
                validator,
                context.Request.Headers.Authorization.ToString(),
                token)))
        .AllowAnonymous()
        .WithSummary("Reports what a session token contains and why it was rejected (development only).");

    // Whether a token even arrived at the protected endpoint. "Was the header sent?" is the first
    // question when a call comes back 401, and the browser can answer it only from its own side.
    app.MapGet("/api/diagnostics/last-request", () => Results.Ok(new
    {
        observations = RequestObservationLog.Recent(5),
        hint = "authorizationHeaderPresent=false means the browser sent no Authorization header at all; " +
               "tokenLength=0 means it sent an empty one. Neither is a token-validation failure.",
    }))
        .AllowAnonymous()
        .WithSummary("How the last requests to protected endpoints looked (development only).");

    // Why the last requests were refused, as the server saw them. The browser only ever sees a 401,
    // so this is the only way the page can explain itself.
    app.MapGet("/api/diagnostics/rejections", (
            [FromServices] Shopify.Blazor.Diagnostics.ISessionTokenRejectionLog log,
            int? count) => Results.Ok(log.Recent(count ?? 5)))
        .AllowAnonymous()
        .WithSummary("The most recent session token rejections (development only).");
}

// The one authenticated backend endpoint the reference app demonstrates. It never trusts a query
// string: the App Bridge session token in the Authorization header is verified against Shopify's
// published keys before the shop is believed, and the Admin API call happens server-side.
app.MapGet("/api/products", async (
        HttpContext context,
        ProductsService products,
        CancellationToken cancellationToken) =>
    {
        // Note the ordering: authorization has already run by the time this executes, so reaching
        // here with no bearer token means the auth handler *accepted* the request without one,
        // which is a routing or scheme problem rather than a credential problem. Say so, instead of
        // returning a bare 401 that is indistinguishable from a rejected token.
        if (BearerTokens.Read(context) is not { } sessionToken)
        {
            return Results.Problem(
                title: "No session token",
                detail: "This endpoint requires an App Bridge session token in the Authorization header. " +
                        "If you are calling it from the app and seeing this, the browser did not attach one: " +
                        $"see /api/diagnostics/last-request. Header present: {context.Request.Headers.ContainsKey("Authorization")}.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        try
        {
            var result = await products.ListAsync(sessionToken, cancellationToken);

            // The shop domain is included so the page can show which store is connected without
            // needing a second round trip.
            return Results.Ok(new
            {
                shop = result.ShopDomain,
                products = result.Products,
                apiVersion = result.ApiVersion,
            });
        }
        catch (ShopifyAdminApiException ex)
        {
            // Distinguish "your token was rejected" from "Shopify said no" so the page can tell the
            // merchant something actionable.
            var status = ex.IsTokenRejected ? StatusCodes.Status401Unauthorized : StatusCodes.Status502BadGateway;

            return Results.Problem(
                title: ex.IsTokenRejected ? "Session rejected" : "Shopify request failed",
                detail: ex.Message,
                statusCode: status);
        }
    })
    .RequireAuthorization();

app.Run();
