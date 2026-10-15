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

        // ngrok's free plan serves a browser-warning interstitial (ERR_NGROK_6024) instead of your
        // app to requests that look like browser navigations. It answers with HTTP 200 and an HTML
        // body, so without this header JSON.parse gets the warning page and the call fails with a
        // confusing parse error rather than anything that names the cause.
        //
        // The interstitial is shown once per browser and endpoint — clicking "Visit Site" once
        // clears it for the Admin's iframe too — so this header is belt-and-braces for the API
        // calls as well as a convenience when testing with a fresh profile.
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

/*
 * There is deliberately no componentStatus export here.
 *
 * The component library already ships one, reached from C# through
 * IPolarisInterop.GetComponentStatusAsync. A second copy in the app made it easy to invoke this one
 * as a global function (IJSRuntime.InvokeAsync("componentStatus", …)), which cannot work for an ES
 * module export: it throws "could not find componentStatus" and the page reports a broken runtime
 * check that says nothing about the elements.
 */
