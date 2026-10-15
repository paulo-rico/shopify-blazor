# Shopify.Blazor.Cli

A convenience wrapper for the [Shopify Blazor Toolkit](../..) as a .NET global tool. It scaffolds
an app from the `shopify-blazor` template, adds pages and component usage, validates local
configuration, and diagnoses the mistakes that fail silently in an embedded Blazor app.

```sh
dotnet tool install --global Shopify.Blazor.Cli
shopify-blazor doctor
```

Every command maps to a plain `dotnet`, filesystem or configuration action that you can take
yourself — the CLI is never the only way to do anything. See
[docs/cli-reference.md](../../docs/cli-reference.md).
