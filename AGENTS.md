# Working notes for the Shopify Blazor Toolkit

Load this before touching the repository. Everything here was learned by breaking it, and none of
these failures raise an error when violated.

## Environment on this machine

The DSH file sandbox allows writes **only** under the session workspace. The .NET CLI wants
`~/.dotnet` and `~/.nuget` there, which fails with `Read-only file system`, so every command needs
the CLI home redirected into the workspace:

```sh
cd /home/paul/aiprojects/shopify-blazor
export DOTNET_CLI_HOME=$PWD/.dotnet
export NUGET_PACKAGES=$PWD/.nuget/packages
export DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
dotnet test
```

Both directories are git-ignored. Without them `dotnet` fails before doing anything.

`.NET 10.0.112` is the only SDK. `global.json` pins `10.0.100` with `rollForward: latestFeature`.

## Commands

```sh
dotnet test                                    # both suites, 79 tests
dotnet build                                   # whole solution
dotnet run --project samples/Shopify.Blazor.SampleApp   # http://localhost:3458
```

Verification that needs a browser — no npm, no test framework, Node 24 only:

```sh
dotnet run --project samples/Shopify.Blazor.SampleApp &
node tools/verify-in-browser.mjs http://127.0.0.1:3459/components
python3 tools/generate-component-reference.py    # regenerate docs/components.md
```

## The constraints that fail silently

Read these before changing anything in `src/`.

1. **Wrappers must be plain C# classes, never `.razor` files.** The Razor compiler injects its own
   `BuildRenderTree` into every `.razor` component, silently replacing the base-class implementation
   and rendering **nothing**. The build succeeds and the page is empty.
2. **A Blazor Web App is static unless `@rendermode InteractiveServer` is applied.** The markup
   renders perfectly and no handler is ever attached, so every control is inert.
3. **A library shipping a JavaScript file needs `Microsoft.NET.Sdk.Razor`.** With
   `Microsoft.NET.Sdk` the build succeeds, the file is in `wwwroot`, and the browser gets a 404 for
   `/_content/…`. Symptom: buttons render perfectly and do nothing. The failure is logged as
   `Polaris interop module … could not be loaded` — look for it. **Both** `Polaris.Blazor` and
   `Shopify.Blazor` need this, and the trap repeats at pack time — a package can build fine and
   still ship without the file. Verify after packing:

   ```sh
   dotnet pack Shopify.Blazor.slnx -c Release -o artifacts
   python3 - <<'EOF'
   import glob, zipfile
   for package in sorted(glob.glob("artifacts/*.nupkg")):
       js = [n for n in zipfile.ZipFile(package).namelist() if n.endswith(".js")]
       print(package, "->", js or "NO JAVASCRIPT SHIPPED")
   EOF
   ```
4. **Polaris booleans are presence attributes.** `disabled="false"` is still `true` to a custom
   element. `true` must render bare; `false` must remove the attribute.
5. **`value`, `checked`, `selected` and `indeterminate` are not reflected.** They must be set as DOM
   properties after the element upgrades. `value` is also rendered as `defaultValue` for first paint.
6. **Polaris validates slot children.** A `primary-action` slot rejects a `div` that wraps a button,
   so slots cannot be `RenderFragment` parameters — a fragment cannot carry an attribute. Use the
   `Slot` parameter on the child component.
7. **`s-text-area` takes its value from the property, never its child text.** Copying the native
   `<textarea>` habit submits an empty string with no warning.
8. **Numbers must be formatted with `CultureInfo.InvariantCulture`,** or a comma decimal separator
   emits `value="1,5"`.
9. **A source-generated `JsonSerializerContext` ignores naming policy unless it is declared.** Without
   `[JsonSourceGenerationOptions(PropertyNamingPolicy = …)]`, Shopify's `expires_in` never binds and
   every token looks non-expiring.
10. **Session tokens are HS256, signed with the app's API secret — there is no key set.** Shopify's
    own libraries do `JWT.decode(token, api_secret_key, true, algorithm: "HS256")`. Do **not**
    verify them against `https://{shop}/.well-known/jwks.json`: that endpoint belongs to the
    Storefront and Customer Account APIs, and it sits behind the storefront, which every dev store
    password-protects — so RS256 verification could never work, on any store. An earlier version of
    this toolkit made exactly that mistake. The accepted algorithm is pinned, and HMAC-SHA256 needs
    a secret of at least 16 bytes, so a truncated secret fails every token.
11. **The `dest` claim is a URL** (`https://shop.myshopify.com`), not a bare host, and it is
    attacker-supplied. It is validated before being interpolated into the token-exchange URL.
12. **Two caches hold the access token.** A 401 must clear both `IShopifyAccessTokenStore` **and**
    `IShopifyTokenExchanger` (`Invalidate`), or a revoked token is reused for ever.
13. **The anti-forgery middleware makes the app unframeable, and this is invisible.** Blazor's
    rendering pipeline requires `app.UseAntiforgery()`, and that middleware sets
    `X-Frame-Options: SAMEORIGIN` and `Content-Security-Policy: frame-ancestors 'self'` on every
    response that asks for a token. Both refuse the Admin's iframe, so the app can never be embedded
    while looking perfect in a normal browser tab. `app.UseShopifyEmbeddedFraming()` must be
    registered **first** — its `OnStarting` callback has to run after antiforgery's, and
    `OnStarting` callbacks run last-registered-first. Verify with a real browser:
    `Framing '…' violates … "frame-ancestors 'self'"` in the console is this bug.
14. **ngrok's free plan shows a browser-warning interstitial, once per browser per endpoint.**
    `ERR_NGROK_6024`, "You are about to visit…" — it is ngrok's page, not the app's. It is **not** a
    blocker: click **Visit Site** once and it stops for that browser, including inside the Admin's
    iframe, which shares the browser. Two consequences: it answers HTTP 200 with an HTML body, so a
    `fetch` can mistake it for success and fail later with a JSON parse error — anything scripted
    should send `ngrok-skip-browser-warning: true`, which the app's own calls do; and a fresh
    profile or private window sees it once again. Removing it entirely needs a paid plan or a tunnel
    that injects nothing.
15. **An environment variable silently overrides user secrets.** If `SHOPIFY_API_KEY` is set in the
    shell, it wins over `Shopify:ApiKey` in user secrets, and a key belonging to a *different*
    Shopify app makes every session token fail as "another app". Startup logs which layer won. The
    app also runs in **Production** with no `ASPNETCORE_ENVIRONMENT` set, which disables the
    diagnostics endpoints and the rejection hints — `bin/shopify-dev` sets Development for you.
16. **A retired Admin API version does not error.** Shopify falls forward to an older stable release
    and answers normally; only the `X-Shopify-API-Version` header reveals it. Check the version table
    at <https://shopify.dev/docs/api/usage/versioning> before changing `AdminApiVersion`.

17. **A missing scope arrives as HTTP 200 with an `errors` array, not a 403.** Shopify answers an
    unauthorised field with `200 OK` and `extensions.code = "ACCESS_DENIED"`, so nothing about the
    status code says "permission". It looks exactly like a gateway fault, and it is the reason a
    page can report "Shopify request failed" while the server log shows the Admin API returning
    200. Retrying cannot fix it — re-exchanging the session token yields the same scopes — so it is
    reported separately (`ShopifyAdminApiException.IsAccessDenied`), and the granted scopes are
    visible in `/api/diagnostics/session-token` as `grantedScopes`. `read_products` is required by
    the sample app's Products page; a session token that grants less fails there and nowhere else,
    because that route is the only one that calls Shopify.
18. **`pgrep -f <pattern>` matches its own caller.** `pgrep -f` searches full command lines, so a
    pattern also matches any shell whose command line contains that text — a wrapper script, an
    editor task, or the very script doing the killing. `bin/shopify-dev` SIGTERMed itself this way,
    leaving the app running and the port looking permanently busy. Use a character class
    (`Shopify[.]Blazor`) so the pattern matches the target text without containing it.

## Resetting the build tree

To force a from-scratch build, remove **only** .NET build output. Do not reach for
`find -name bin`:

```sh
# WRONG: also deletes bin/, and bin/shopify-dev lives there. It has been lost this way once, and
# silently kept out of a commit another time by a blanket `bin/` in .gitignore.
find . -type d \( -name bin -o -name obj \) -prune -exec rm -rf {} +

# RIGHT: only the configuration subdirectories a .NET build creates.
rm -rf ./**/bin/Debug ./**/bin/Release ./**/obj
```

`bin/shopify-dev` is a tracked file in a directory named `bin`, which is exactly the kind of
collision a broad pattern does not warn about.

## Conventions

- `Polaris.Blazor` must not depend on `Shopify.Blazor`. The component library is usable alone.
- Tests assert on rendered markup, not on internal state, and every test carries a comment
  explaining the failure it prevents. A test that cannot say what it would catch is not kept.
- Documentation comments on every public member. The build does not require them, but
  `tools/generate-component-reference.py` reads them to generate `docs/components.md`, so an
  undocumented parameter is an undocumented component.
- New components: add the class in `Components/`, an entry in the test file, then run
  `tools/generate-component-reference.py`.
- Commit and push. This machine has no other safety net and sessions do not share state. Pushing
  needs the `gh` token, because the credential helper wants a terminal and fails with
  `could not read Username for 'https://github.com'`:

  ```sh
  TOKEN=$(gh auth token)
  git -c credential.helper= \
      -c "credential.helper=!f() { echo username=x-access-token; echo password=$TOKEN; }; f" \
      push origin main
  ```

## Not verified

Installation in a real Shopify development store. It needs Partner Dashboard credentials and a public
HTTPS tunnel, so it is a human step. See `docs/PROGRESS.md`.
