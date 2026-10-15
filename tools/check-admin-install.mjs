#!/usr/bin/env node
/*
 * Checks the two things that break an embedded Shopify install, from outside the app.
 *
 *   node tools/check-admin-install.mjs https://<your-server-hostname>
 *
 * 1. Does the served page carry the shopify-api-key meta tag, and does that key match the one the
 *    server has (via /api/diagnostics/session-token)? A mismatch means App Bridge mints tokens for
 *    one app and the server rejects them as another — the single most common cause of
 *    "ShopifySessionToken was not authenticated".
 * 2. Does the URL answer with X-Frame-Options or a CSP frame-ancestors that would stop Shopify
 *    embedding the app at all.
 */

const url = process.argv[2];

if (!url || !/^https?:\/\//.test(url)) {
    console.error("Usage: node tools/check-admin-install.mjs https://<your-server-hostname>");
    process.exit(2);
}

const base = url.replace(/\/+$/, "");
const problems = [];
const notes = [];

console.log(`Checking ${base}\n`);

// --- the page itself -------------------------------------------------------------------------
let html;
try {
    const response = await fetch(base + "/", { redirect: "follow" });
    html = await response.text();

    console.log(`GET /            -> ${response.status}`);

    const framing = ["x-frame-options", "content-security-policy"]
        .map((h) => [h, response.headers.get(h)])
        .filter(([, v]) => v);

    if (framing.length === 0) {
        console.log("frame headers    -> none (good: nothing stops Shopify embedding the app)");
    } else {
        for (const [header, value] of framing) {
            console.log(`frame headers    -> ${header}: ${value}`);

            if (header === "x-frame-options" && !/allowall/i.test(value)) {
                problems.push(
                    `${header}: ${value} stops the Admin from embedding the app. ` +
                    "Shopify needs the app to be frameable by the shop's admin origin."
                );
            }

            const ancestors = /frame-ancestors\s+([^;]+)/i.exec(value ?? "");
            if (ancestors && !/admin\.shopify\.com|\*/.test(ancestors[1])) {
                problems.push(
                    `CSP frame-ancestors is '${ancestors[1].trim()}', which does not include ` +
                    "https://admin.shopify.com. The app cannot be embedded."
                );
            }
        }
    }
} catch (error) {
    console.error(`GET /            -> failed: ${error.message}`);
    process.exit(1);
}

const metaKey = /<meta[^>]+name="shopify-api-key"[^>]*content="([^"]*)"/i.exec(html)?.[1]
    ?? /<meta[^>]+content="([^"]*)"[^>]*name="shopify-api-key"/i.exec(html)?.[1];

if (metaKey === undefined) {
    problems.push(
        "The page has no shopify-api-key meta tag. App Bridge reads it to inject window.shopify, so " +
        "without it no session token is ever minted. Is <ShopifyAppBridgeHead /> inside <head>?"
    );
} else if (!metaKey) {
    problems.push(
        "The shopify-api-key meta tag is present but empty. Shopify:ApiKey is not reaching the app; " +
        "check user secrets and environment variables."
    );
} else {
    console.log(`shopify-api-key  -> ${metaKey}`);
}

const scripts = [...html.matchAll(/<script[^>]+src="([^"]+)"/gi)].map((m) => m[1]);
const hasAppBridge = scripts.some((s) => s.includes("app-bridge.js"));
const hasPolaris = scripts.some((s) => s.includes("polaris-1"));

console.log(`app-bridge.js    -> ${hasAppBridge ? "present" : "MISSING"}`);
console.log(`polaris-1.js     -> ${hasPolaris ? "present" : "MISSING"}`);

if (!hasAppBridge) {
    problems.push("app-bridge.js is not in the page. window.shopify will be undefined and no token can be minted.");
}

// --- what the server thinks its key is --------------------------------------------------------
try {
    const response = await fetch(base + "/api/diagnostics/session-token");
    console.log(`\nGET /api/diagnostics/session-token -> ${response.status}`);

    if (response.status === 404) {
        notes.push(
            "The diagnostics endpoint is not mapped. It only exists when the app runs in the " +
            "Development environment, so start it with ASPNETCORE_ENVIRONMENT=Development."
        );
    } else {
        const report = await response.json();
        console.log(`server api key   -> ${report.configuredApiKey}`);

        const serverKey = report.configuredApiKey ?? "";
        const mask = serverKey.includes("\u2026") ? serverKey.split("\u2026") : null;

        if (mask && metaKey) {
            // The masked form keeps the first and last four characters.
            const matches = metaKey.startsWith(mask[0]) && metaKey.endsWith(mask[1].split(" ")[0]);

            console.log(`keys match       -> ${matches ? "yes" : "NO"}`);

            if (!matches) {
                problems.push(
                    "The API key in the page's shopify-api-key meta tag does not match the server's " +
                    "Shopify:ApiKey. App Bridge will mint session tokens for one app and the server will " +
                    "reject them as another. Point the Partner Dashboard App URL at this app, or correct " +
                    "SHOPIFY_API_KEY."
                );
            }
        }
    }
} catch (error) {
    notes.push(`Could not read the diagnostics endpoint: ${error.message}`);
}

// --- verdict ---------------------------------------------------------------------------------
console.log("");

if (problems.length === 0) {
    console.log("No install-level problems found.");
} else {
    console.log("Problems:");
    for (const problem of problems) {
        console.log(`  - ${problem}`);
    }
}

for (const note of notes) {
    console.log(`Note: ${note}`);
}

process.exit(problems.length === 0 ? 0 : 1);
