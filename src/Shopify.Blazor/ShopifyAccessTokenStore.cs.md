<!-- summary-of: ShopifyAccessTokenStore.cs -->
<!-- source-sha256: f6c9559da215 -->

# ShopifyAccessTokenStore.cs

The cache of exchanged Admin API access tokens, and its default single-process implementation. A token
is keyed by shop, by whether it is offline or online, and — for an online token — by the user it belongs
to, so a shop's offline token is shared while an online token is never handed to a different merchant.

`Shopify.Blazor` · `IShopifyAccessTokenStore` + `sealed class InMemoryShopifyAccessTokenStore` · `ConcurrentDictionary`-backed, the default store

## Members

| Member | Signature | What it does |
|---|---|---|
| `IShopifyAccessTokenStore.TryGet` | `bool TryGet(string shopDomain, string subject, ShopifyAccessTokenKind kind, out ShopifyAccessToken? token)` | Returns a cached token, or `false` with `token` null. An entry past its deadline, or a token whose own `IsExpired` is true, is **removed** and reported as a miss. |
| `IShopifyAccessTokenStore.Set` | `void Set(string shopDomain, string subject, ShopifyAccessTokenKind kind, ShopifyAccessToken token, TimeSpan lifetime)` | Stores the token with a deadline of `UtcNow + lifetime`, shortened to the token's own `ExpiresAt` when that is earlier. Overwrites any existing entry for the same key. |
| `IShopifyAccessTokenStore.Remove` | `void Remove(string shopDomain, string subject, ShopifyAccessTokenKind kind)` | Drops the entry. This is the call a 401 requires, alongside `IShopifyTokenExchanger.Invalidate`. |
| `InMemoryShopifyAccessTokenStore` | `sealed class : IShopifyAccessTokenStore` | The default implementation: one `ConcurrentDictionary<string, Entry>` with `StringComparer.Ordinal`, where `Entry` is a private `readonly record struct` holding the token and its cache deadline. All three members behave as above. |

## Behaviour

- **The cache key drops the subject for offline tokens.** `Offline` keys are `"{shop}|offline"`; `Online`
  keys are `"{shop}|online|{subject}"`. Two different users asking for an offline token therefore get the
  same entry, which is correct for an app-level credential and is why the `subject` argument looks
  redundant in that case but must still be passed.
- **The effective lifetime is the earlier of two deadlines.** Passing `TimeSpan.FromDays(30)` for a token
  Shopify says expires in an hour caches it for an hour, so a caller cannot accidentally keep a dead token
  alive. The reverse is allowed: a short requested lifetime discards a still-valid token earlier than it
  needs to.
- **Expiry is checked twice on read** — the entry's deadline *and* the token's own `IsExpired` — because
  the two are written at different times and either can be the tighter bound.
- **Ordinal comparison throughout**, so a shop domain must already be normalised (lower-case, canonical)
  or the same shop can occupy two entries.
- The interface exists so a deployment can substitute a shared store; nothing in this file depends on
  process-local state except the dictionary itself.

## Gotchas

- **The in-memory store is per process.** A multi-instance deployment that keeps the default has every
  instance exchange its own token on first use: it works, but it churns tokens and loses the cache
  benefit. A shared store (database or distributed cache) is the fix, and it is a substitution of this
  interface, not a change here.
- **`TryGet` writes.** A miss removes the stale entry as a side effect, so two concurrent callers can both
  miss and both exchange. That is benign (the second `Set` wins) but means the method is not a pure query
  and cannot be used to probe without affecting state.
- **Nothing evicts a live entry.** There is no size cap, no sweeper and no sliding expiration, so a long-
  running process accumulates one entry per shop (plus one per user for online tokens) until each expires.
- **`Remove` must be paired with `IShopifyTokenExchanger.Invalidate`.** This store is only one of two
  places a token is cached; clearing just this one leaves the exchanger handing back the revoked token,
  and the 401 repeats for ever. `ShopifyAdminApi` does both.
- **Tokens sit in process memory as plain strings**, with the full bearer value, for as long as they live.
- **`out` is declared `ShopifyAccessToken?` even though this implementation never returns `true` with a
  null token.** A caller still has to prove the value is non-null to the compiler, which is why
  `ShopifyAdminApi` tests the result *and* `token is null` after every hit.

## See also

- [ShopifyAccessToken.cs.md](ShopifyAccessToken.cs.md) — what is stored, including the `ExpiresAt` that
  shortens the cache deadline.
- [ShopifyAdminApi.cs.md](ShopifyAdminApi.cs.md) — reads this store on every call and removes from it
  after a 401 or 403.
- [Authentication/ShopifyTokenExchanger.cs.md](Authentication/ShopifyTokenExchanger.cs.md) — the other
  cache, which must be invalidated at the same time.
