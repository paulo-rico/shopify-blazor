/*
 * Sample-app browser interop.
 *
 * Deliberately small. Everything here exists because it has to run in the browser: minting an
 * App Bridge session token, and showing the Admin's own progress bar while a request is in flight.
 * Application logic — what a product list means, what to do when a request fails — stays in C#.
 *
 * The token is fetched immediately before each call rather than cached, because session tokens
 * live about a minute and a Blazor circuit can outlive several of them.
 */

/** True when App Bridge is present, which is only inside the Shopify Admin. */
export function isEmbedded() {
    return Boolean(window.shopify);
}

/**
 * Calls one of this app's own endpoints with a session token as a bearer credential.
 *
 * @param {string} url endpoint on this origin
 * @param {string} method HTTP method
 * @param {unknown} body optional JSON body
 * @returns {Promise<{ok: boolean, status: number, data: unknown, error: string|null}>}
 */
export async function call(url, method = "GET", body = null) {
    if (!window.shopify?.idToken) {
        return {
            ok: false,
            status: 0,
            data: null,
            error: "App Bridge is not available. Open this page from inside the Shopify Admin.",
        };
    }

    const token = await window.shopify.idToken();

    const headers = {
        Accept: "application/json",
        Authorization: `Bearer ${token}`,

        // Development tunnels in front of the app — ngrok's free plan in particular — serve a
        // browser-warning interstitial instead of your app to requests that look like browser
        // navigations, and ngrok decides that from the User-Agent. A fetch from this page is an
        // XHR rather than a navigation, but ngrok still inspects the User-Agent, so without this
        // header the response is that HTML page with HTTP 200 and JSON.parse then fails.
        //
        // This only helps requests this app makes. It cannot help the Admin's iframe: no script of
        // ours runs before that frame document is fetched, so the interstitial appears in the
        // embedded view and only the tunnel operator can remove it.
        "ngrok-skip-browser-warning": "true",
    };

    let payload;
    if (body !== null && body !== undefined) {
        headers["Content-Type"] = "application/json";
        payload = typeof body === "string" ? body : JSON.stringify(body);
    }

    const response = await fetch(url, { method, headers, body: payload });
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
        error: response.ok ? null : describe(status, data),
    };
}

function describe(status, data) {
    if (data && typeof data === "object") {
        if (typeof data.detail === "string" && data.detail) return data.detail;
        if (typeof data.title === "string" && data.title) return data.title;
    }

    return `The request failed with HTTP ${status}.`;
}

/**
 * Starts or stops the Admin's header progress bar.
 *
 * It persists until stopped, so every caller must pair the two calls. The C# side owns that via
 * IAppBridgeInterop.WithLoadingAsync.
 */
export function loading(isLoading) {
    if (!window.shopify?.loading) return false;

    window.shopify.loading(Boolean(isLoading));
    return true;
}

/**
 * Reports which Polaris custom elements are registered and upgraded.
 *
 * An element that never upgrades renders as an empty box while the markup looks perfectly
 * correct, so the Components page surfaces this rather than leaving it to guesswork.
 */
export function componentStatus(tagNames) {
    const result = {};

    for (const name of tagNames) {
        const definition = window.customElements?.get(name);
        const elements = Array.from(document.querySelectorAll(name));

        result[name] = {
            defined: Boolean(definition),
            count: elements.length,
            upgraded: Boolean(definition) && elements.every((el) => el instanceof definition),
        };
    }

    return result;
}
