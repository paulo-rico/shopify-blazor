using Polaris.Blazor;
using Shopify.Blazor;
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

// The Polaris component library's JavaScript interop layer.
builder.Services.AddPolarisBlazor();

builder.Services.AddProblemDetails();
builder.Services.AddScoped<ProductsService>();

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    app.UseHsts();
}

// Shopify embeds the app in an iframe on the shop's own domain, so the app must not tell the
// browser to refuse framing. Weakening this is a common "fix" that breaks embedding entirely.
app.UseStatusCodePages();
app.UseStaticFiles();
app.UseAntiforgery();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

// The one authenticated backend endpoint the reference app demonstrates. It never trusts a query
// string: the App Bridge session token in the Authorization header is verified against Shopify's
// published keys before the shop is believed, and the Admin API call happens server-side.
app.MapGet("/api/products", async (
        HttpContext context,
        ProductsService products,
        CancellationToken cancellationToken) =>
    {
        var authorization = context.Request.Headers.Authorization.ToString();

        if (!authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Results.Unauthorized();
        }

        var sessionToken = authorization["Bearer ".Length..].Trim();

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
