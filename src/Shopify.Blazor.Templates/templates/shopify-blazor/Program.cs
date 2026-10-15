using Polaris.Blazor;
using Shopify.Blazor;
using Shopify.Blazor.Diagnostics;
using Shopify.Blazor.SampleApp.Components;
using Shopify.Blazor.SampleApp.Services;

var builder = WebApplication.CreateBuilder(args);

// Blazor Web App with Interactive Server. A Blazor Web App is *static* unless a render mode is
// applied, and a static page renders perfect markup with no event handler attached — which looks
// like a styling problem and is not one. The render mode is set in Components/Routes.razor.
builder.Services
    .AddRazorComponents()
    .AddInteractiveServerComponents();

// Shopify infrastructure: configuration, session-token validation, token exchange, the Admin API
// client and the App Bridge authentication scheme.
//
// Startup validation is skipped while no API key is configured, so the app can be run before the
// Shopify app exists. Nothing that needs the key works in that state, and the page says so rather
// than failing at boot with no explanation.
var shopifyApiKey = builder.Configuration["Shopify:ApiKey"]
    ?? Environment.GetEnvironmentVariable("SHOPIFY_API_KEY");

var shopifyConfigured = !string.IsNullOrWhiteSpace(shopifyApiKey)
    && !string.IsNullOrWhiteSpace(
        builder.Configuration["Shopify:ApiSecret"] ?? Environment.GetEnvironmentVariable("SHOPIFY_API_SECRET"));

builder.Services.AddShopifyBlazor(builder.Configuration, validateOnStart: shopifyConfigured);

// In Development a rejected session token answers with the reason, so the page can say "the aud
// claim is for a different app" instead of a bare Unauthorized. Off elsewhere: a production
// response must not tell an unauthenticated client why its credential failed.
builder.Services.Configure<Shopify.Blazor.Authentication.ShopifyAuthenticationOptions>(options =>
{
    options.IncludeFailureDetail = builder.Environment.IsDevelopment();
});

// The Polaris component library's JavaScript interop layer. Without it, components render and
// nothing they raise ever reaches C#.
builder.Services.AddPolarisBlazor();

builder.Services.AddProblemDetails();
//#if (include-admin-api-example)
builder.Services.AddScoped<ProductsService>();
//#endif

var app = builder.Build();

// Says which configuration layer supplied the API key. An environment variable silently overrides
// user secrets, and a key from a different Shopify app rejects every session token, so the answer
// to "which key is this?" belongs in the startup log.
app.LogShopifyConfiguration(builder.Configuration);

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    //app.UseHsts();
}

// Shopify embeds the app in an iframe, so the app has to permit framing — but only by the Admin.
// The anti-forgery middleware that Blazor's rendering pipeline requires sets X-Frame-Options and a
// frame-ancestors policy that both refuse it, which makes the app impossible to embed while looking
// perfect in a normal browser tab.
//
// Registered before UseAntiforgery, on purpose: its OnStarting callback has to run *after*
// antiforgery has written its own headers, and OnStarting callbacks run last-registered-first.
//
// No configuration is needed: the Admin origins are allowed and nothing else. To embed the app
// somewhere else as well — a custom admin host, or a locally served probe page — add that origin:
//
//     options.AllowFrameAncestor("https://admin.example.com");
app.UseShopifyEmbeddedFraming();

app.UseStatusCodePages();
app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// Development-only diagnostics. They answer "which token arrived, and why was it rejected?", which
// a generic 401 cannot. Never mapped outside Development: they decode tokens without verifying
// them, so they are a debugging aid and not part of the app's security surface.
if (app.Environment.IsDevelopment())
{
    app.MapGet("/api/diagnostics/session-token", async (
            HttpContext context,
            IHttpClientFactory httpClientFactory,
            Microsoft.Extensions.Options.IOptionsMonitor<ShopifyOptions> options,
            Shopify.Blazor.Authentication.IShopifySessionTokenValidator validator,
            string? token) => Results.Ok(await SessionTokenDiagnostics.InspectAsync(
                httpClientFactory,
                options,
                validator,
                context.Request.Headers.Authorization.ToString(),
                token)))
        .AllowAnonymous()
        .WithSummary("Reports what a session token contains and why it was rejected (development only).");

    // Why the last requests were refused, as the server saw them. The browser only ever sees a 401,
    // so this is the only way the page can explain itself.
    app.MapGet("/api/diagnostics/rejections", (ISessionTokenRejectionLog log, int? count) =>
            Results.Ok(log.Recent(count ?? 5)))
        .AllowAnonymous()
        .WithSummary("The most recent session token rejections (development only).");
}
//#if (include-admin-api-example)

// The authenticated backend endpoint. It never trusts a query string or a cookie: the App Bridge
// session token in the Authorization header is verified against Shopify's published keys before
// the shop is believed, and the Admin API call happens server-side, where the access token lives.
app.MapGet("/api/products", async (
        HttpContext context,
        ProductsService products,
        CancellationToken cancellationToken) =>
    {
        // Authorization has already run by the time this executes, so reaching here with no bearer
        // token means the auth handler accepted the request *without* one — a routing or scheme
        // problem, not a credential problem. Say which, instead of returning a bare 401 that is
        // indistinguishable from a rejected token.
        if (BearerTokens.Read(context) is not { } sessionToken)
        {
            return Results.Problem(
                title: "No session token",
                detail: "This endpoint requires an App Bridge session token in the Authorization " +
                        "header. Open the app from the Shopify Admin, or check " +
                        "/api/diagnostics/rejections in Development.",
                statusCode: StatusCodes.Status401Unauthorized);
        }

        try
        {
            var result = await products.ListAsync(sessionToken, cancellationToken);

            return Results.Ok(new
            {
                shop = result.ShopDomain,
                products = result.Products,
                apiVersion = result.ApiVersion,
            });
        }
        catch (ShopifyAdminApiException ex)
        {
            // Three outcomes, and they need different actions from the person reading the page:
            //   token rejected -> fetch a fresh token and retry (401)
            //   access denied  -> a scope was never granted; retrying cannot help (403)
            //   anything else  -> Shopify refused the query (502)
            var status = ex switch
            {
                { IsTokenRejected: true } => StatusCodes.Status401Unauthorized,
                { IsAccessDenied: true } => StatusCodes.Status403Forbidden,
                _ => StatusCodes.Status502BadGateway,
            };

            var title = ex switch
            {
                { IsTokenRejected: true } => "Session rejected",
                { IsAccessDenied: true } => "Missing permission",
                _ => "Shopify request failed",
            };

            // A missing scope arrives as HTTP 200 with an errors array, so it is reported from the
            // exception rather than inferred from a status code. It is also the one failure a retry
            // cannot fix, so the message points at the cause.
            var detail = ex.IsAccessDenied
                ? $"{ex.Message} The app is asking Shopify for data it has not been granted access " +
                  "to. Add the scope to the app's configuration in the Partner Dashboard " +
                  "(read_products for this page), then reopen the app so the merchant approves the " +
                  "new permissions."
                : ex.Message;

            return Results.Problem(title: title, detail: detail, statusCode: status);
        }
    })
    .RequireAuthorization();
//#endif

app.Run();
