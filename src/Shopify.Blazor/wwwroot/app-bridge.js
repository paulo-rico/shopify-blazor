/*
 * Shopify.Blazor — App Bridge 4 interop.
 *
 * App Bridge is CDN-only and injects a single global, `window.shopify`, which exists *inside the
 * Shopify Admin and nowhere else*. Opening the app URL directly leaves it undefined, so every
 * function here is guarded: an unguarded `shopify.idToken()` turns a working app into a blank
 * page during debugging.
 *
 * There is no npm package and no app instance to create. The deprecated v3 surface
 * (`createApp`, `getSessionToken`, `@shopify/app-bridge-utils`) is deliberately absent.
 */

/** The global App Bridge object, or undefined outside the Admin. */
function bridge() {
    return window.shopify;
}

/** True when the page is embedded in the Shopify Admin and App Bridge is available. */
export function isAvailable() {
    return Boolean(bridge());
}

/** Rejects with a message that says what to do, rather than a TypeError from undefined. */
function requireBridge(api) {
    const shopify = bridge();

    if (!shopify?.[api]) {
        throw new Error(
            `App Bridge '${api}' is not available. This page is not running inside the Shopify Admin, ` +
            "or the shopify-api-key meta tag and app-bridge.js script are missing from <head>."
        );
    }

    return shopify;
}

/**
 * Returns a fresh App Bridge session token (an ID token) for the current shop.
 *
 * Tokens are short lived — on the order of a minute — so callers fetch one per request rather
 * than caching it. The backend validates the signature before trusting any claim in it.
 *
 * @returns {Promise<string>} the serialized JWT
 */
export async function idToken() {
    return await requireBridge("idToken").idToken();
}

/**
 * Calls the app's own backend with an App Bridge session token as a bearer credential.
 *
 * The point of routing through here rather than fetching from C# is that the token is minted
 * immediately before the request, so a long-lived Blazor circuit cannot present a stale one.
 *
 * @param {string} url absolute or relative URL on this app's origin
 * @param {{method?: string, body?: unknown, headers?: Record<string,string>}} options
 * @returns {Promise<{ok: boolean, status: number, data: unknown, error: string|null}>}
 */
export async function fetchWithSessionToken(url, options = {}) {
    const token = await idToken();

    const headers = {
        Accept: "application/json",
        Authorization: `Bearer ${token}`,
        ...(options.headers ?? {}),
    };

    let body = options.body;
    if (body !== undefined && body !== null && typeof body !== "string") {
        headers["Content-Type"] = "application/json";
        body = JSON.stringify(body);
    }

    const response = await fetch(url, {
        method: options.method ?? (body === undefined ? "GET" : "POST"),
        headers,
        body,
    });

    const text = await response.text();
    let data = null;

    if (text) {
        try {
            data = JSON.parse(text);
        } catch {
            data = text;
        }
    }

    return {
        ok: response.ok,
        status: response.status,
        data,
        error: response.ok ? null : describeError(response.status, data),
    };
}

function describeError(status, data) {
    if (data && typeof data === "object") {
        if (typeof data.error === "string") return data.error;
        if (typeof data.title === "string") return data.title;
        if (typeof data.detail === "string") return data.detail;
    }

    if (typeof data === "string" && data.length < 400) return data;

    return `The request failed with HTTP ${status}.`;
}

/**
 * Shows the Admin's header progress bar.
 *
 * It persists until explicitly stopped, so a missed `false` leaves a progress bar running in the
 * merchant's admin indefinitely. Callers must pair the two calls in a `finally`.
 */
export function loading(isLoading) {
    const shopify = bridge();
    if (!shopify?.loading) return false;

    shopify.loading(Boolean(isLoading));
    return true;
}

/**
 * Shows a toast.
 *
 * @param {string} message
 * @param {{isError?: boolean, duration?: number}} options
 */
export function toast(message, options = {}) {
    const shopify = bridge();
    if (!shopify?.toast || !message) return false;

    shopify.toast.show(message, {
        isError: Boolean(options.isError),
        ...(options.duration ? { duration: options.duration } : {}),
    });

    return true;
}

/**
 * Navigates within the Admin. Outside the Admin it falls back to a normal location change, so a
 * standalone render still works.
 *
 * @param {string} url
 */
export async function navigate(url) {
    const shopify = bridge();

    if (shopify?.navigation?.navigate) {
        await shopify.navigation.navigate(url);
        return true;
    }

    window.location.assign(url);
    return false;
}

/** The merchant using the app, or null outside the Admin. */
export async function currentUser() {
    const shopify = bridge();
    if (!shopify?.user) return null;

    try {
        return await shopify.user();
    } catch {
        return null;
    }
}

/** The scopes currently granted to the app, or null outside the Admin. */
export async function grantedScopes() {
    const shopify = bridge();
    if (!shopify?.scopes?.query) return null;

    try {
        const scopes = await shopify.scopes.query();
        return Array.isArray(scopes) ? scopes : null;
    } catch {
        return null;
    }
}

/** Context Shopify provides about the embedding: shop, host, locale, platform. */
export function context() {
    const shopify = bridge();
    if (!shopify) return null;

    return {
        shop: shopify.config?.shop ?? null,
        host: shopify.config?.host ?? null,
        locale: shopify.config?.locale ?? null,
        apiKey: shopify.config?.apiKey ?? null,
    };
}
