#!/usr/bin/env node
/*
 * Verifies the app in a real browser, with no test framework and no npm dependencies.
 *
 * Node 24 has a global WebSocket, so this talks to Chrome's DevTools Protocol directly. That
 * matters because the thing being checked cannot be checked any other way: Shopify's Polaris
 * components arrive from a CDN and upgrade asynchronously, and an element that never upgrades
 * renders as an empty box while the markup looks perfectly correct. Only a browser can say
 * whether the elements are really there.
 *
 * Usage: node tools/verify-in-browser.mjs [url] [--chrome <path>] [--assert]
 *
 * Without --assert it prints the full report and exits 0, which is what you want while developing.
 * With --assert it also checks every expectation the toolkit makes of a live page and exits non-zero
 * if any of them is false, which is what CI wants: a report nobody reads is not a test.
 */

import { spawn } from "node:child_process";
import { mkdtemp, rm } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";

const assertMode = process.argv.includes("--assert");
const url = process.argv.slice(2).find((argument) => argument.startsWith("http")) ?? "http://127.0.0.1:3459/";
const chromeFlagIndex = process.argv.indexOf("--chrome");
const chromePath = chromeFlagIndex >= 0 ? process.argv[chromeFlagIndex + 1] : "google-chrome";

const profileDir = await mkdtemp(join(tmpdir(), "polaris-probe-"));
const port = 9222 + Math.floor(Math.random() * 500);

const chrome = spawn(chromePath, [
    "--headless=new",
    "--disable-gpu",
    "--no-first-run",
    "--no-default-browser-check",
    `--remote-debugging-port=${port}`,
    `--user-data-dir=${profileDir}`,
    "about:blank",
], { stdio: ["ignore", "ignore", "pipe"] });

let chromeStderr = "";
chrome.stderr.on("data", (chunk) => { chromeStderr += chunk.toString(); });

const sleep = (ms) => new Promise((resolve) => setTimeout(resolve, ms));

/** Waits for the DevTools endpoint, then returns the first page target's WebSocket URL. */
async function findPageTarget() {
    for (let attempt = 0; attempt < 60; attempt++) {
        try {
            const response = await fetch(`http://127.0.0.1:${port}/json/list`);
            const targets = await response.json();
            const page = targets.find((t) => t.type === "page" && t.webSocketDebuggerUrl);

            if (page) return page.webSocketDebuggerUrl;
        } catch {
            // Chrome is not listening yet.
        }

        await sleep(250);
    }

    throw new Error(`Chrome never exposed a page target on port ${port}.\n${chromeStderr}`);
}

/** Minimal CDP client: send a command, await its result. */
class DevTools {
    #socket;
    #nextId = 1;
    #pending = new Map();

    static async connect(wsUrl) {
        const client = new DevTools();
        client.#socket = new WebSocket(wsUrl);

        await new Promise((resolve, reject) => {
            client.#socket.addEventListener("open", resolve, { once: true });
            client.#socket.addEventListener("error", reject, { once: true });
        });

        client.#socket.addEventListener("message", (event) => {
            const message = JSON.parse(event.data);
            const entry = client.#pending.get(message.id);

            if (!entry) return;

            client.#pending.delete(message.id);

            if (message.error) entry.reject(new Error(message.error.message));
            else entry.resolve(message.result);
        });

        return client;
    }

    send(method, params = {}) {
        const id = this.#nextId++;

        return new Promise((resolve, reject) => {
            this.#pending.set(id, { resolve, reject });
            this.#socket.send(JSON.stringify({ id, method, params }));
        });
    }

    /** Starts collecting console output and uncaught page errors. */
    async startDiagnostics() {
        this.consoleMessages = [];
        this.pageErrors = [];

        this.#socket.addEventListener("message", (event) => {
            const message = JSON.parse(event.data);

            if (message.method === "Runtime.consoleAPICalled") {
                const text = (message.params.args ?? [])
                    .map((a) => a.value ?? a.description ?? a.type)
                    .join(" ");
                this.consoleMessages.push(`${message.params.type}: ${text}`);
            }

            if (message.method === "Runtime.exceptionThrown") {
                // The whole detail object matters. `exception.description` alone is absent for an
                // error thrown by a script the page loaded from a CDN, which reported as the word
                // "unknown" — a failure nobody can act on.
                const details = message.params.exceptionDetails ?? {};
                this.pageErrors.push({
                    text: details.text ?? "",
                    description: details.exception?.description ?? "",
                    url: details.url ?? "",
                    line: details.lineNumber ?? null,
                    column: details.columnNumber ?? null,
                });
            }
        });

        await this.send("Runtime.enable");
    }

    /** Navigates and waits for the load event. */
    async navigate(target) {
        const loaded = new Promise((resolve) => {
            const listener = (event) => {
                const message = JSON.parse(event.data);
                if (message.method === "Page.loadEventFired") {
                    this.#socket.removeEventListener("message", listener);
                    resolve();
                }
            };
            this.#socket.addEventListener("message", listener);
        });

        await this.send("Page.enable");
        await this.send("Page.navigate", { url: target });
        await loaded;
    }

    /** Evaluates an async expression in the page and returns its resolved value. */
    async evaluate(expression) {
        const result = await this.send("Runtime.evaluate", {
            expression,
            awaitPromise: true,
            returnByValue: true,
        });

        if (result.exceptionDetails) {
            throw new Error(result.exceptionDetails.exception?.description ?? "evaluation failed");
        }

        return result.result.value;
    }

    close() {
        this.#socket.close();
    }
}

/**
 * Counts elements the interactive renderer has claimed.
 *
 * Blazor stamps a per-circuit `_bl_<guid>` attribute on every element it owns. Without one, the
 * page is static: the markup is correct and no handler is attached, which is the most confusing
 * way for an interactive component to "not work".
 */
const markedElementHelper = `
    const markedElementCount = () => Array.from(document.querySelectorAll("*"))
        .filter((el) => Array.from(el.attributes).some((a) => a.name.startsWith("_bl_"))).length;
`;

/**
 * The script run inside the page.
 *
 * Everything here is something no unit test can answer: whether Shopify's components really
 * upgraded, whether a form field's value really reaches the request, and whether a DOM event
 * really reaches a C# handler over the Blazor circuit.
 */
const probe = markedElementHelper + `(async () => {
    // The list comes from the page, not from this file. Components.razor renders it, so a component
    // added to the gallery is verified here without anyone remembering to update a second list — and
    // a component listed but never rendered fails the count check instead of passing vacuously.
    const marker = document.getElementById("polaris-probe-elements");
    const elements = (marker?.dataset.elements ?? "").split(",").map((name) => name.trim()).filter(Boolean);

    if (elements.length === 0) {
        return { error: "the page did not render #polaris-probe-elements, so there is nothing to verify" };
    }

    // Polaris defines its elements asynchronously after the CDN script runs, so this waits rather
    // than reading customElements too early and reporting everything as missing.
    const deadline = Date.now() + 20000;
    while (Date.now() < deadline) {
        if (customElements.get("s-page") && customElements.get("s-modal")) break;
        await new Promise((r) => setTimeout(r, 100));
    }

    const status = {};
    for (const name of elements) {
        const definition = customElements.get(name);
        const found = Array.from(document.querySelectorAll(name));
        status[name] = {
            defined: Boolean(definition),
            count: found.length,
            upgraded: Boolean(definition) && found.every((el) => el instanceof definition),
            hasShadowRoot: Boolean(definition) && found.length > 0 && found.every((el) => el.shadowRoot !== null),
        };
    }

    // Three separate failure modes, and they need different fixes: an element that is not defined at
    // all (the CDN script did not load), one that is defined but never rendered (the gallery forgot
    // it), and one that is rendered but never upgraded (the markup looks right and does nothing).
    const counts = {
        listed: elements.length,
        notDefined: elements.filter((name) => !status[name].defined),
        notUpgraded: elements.filter((name) => status[name].defined && !status[name].upgraded),
        notRendered: elements.filter((name) => status[name].count === 0),
        withoutShadowRoot: elements.filter((name) => status[name].defined && !status[name].hasShadowRoot && status[name].count > 0),
        instances: Object.fromEntries(elements.map((name) => [name, status[name].count])),
    };

    // The static assets each renderer must serve. A package that ships JavaScript without the Razor
    // SDK answers 404 here while the page looks perfect, which is the failure that makes every
    // button inert; the reconnect files are plain static files that must also resolve.
    const assets = {};
    for (const path of [
        "css/reconnect-modal.css",
        "js/reconnect-modal.js",
        "_content/Polaris.Blazor/polaris-interop.js",
        "_content/Shopify.Blazor/app-bridge.js",
    ]) {
        try {
            const response = await fetch(path, { cache: "no-store" });
            assets[path] = response.status;
        } catch (error) {
            assets[path] = String(error);
        }
    }

    // The reconnection dialog has to exist before it is needed: it is shown from JavaScript when the
    // circuit is already gone, so a missing element is a page nobody can recover but by reloading.
    const reconnect = (() => {
        const modal = document.getElementById("components-reconnect-modal");
        const href = (selector, attribute) => Array.from(document.querySelectorAll(selector))
            .map((el) => el.getAttribute(attribute))
            .find((value) => value && value.includes("reconnect-modal"));

        return {
            present: modal !== null,
            isDialog: (modal?.tagName ?? "").toLowerCase() === "dialog",
            stylesheet: href('link[rel="stylesheet"]', "href") ?? null,
            script: href("script[src]", "src") ?? null,
        };
    })();

    // A form built from Polaris fields, submitted for real. This is the check that catches a field
    // whose value never reaches the request: the failure is silent and the page looks normal.
    let form = null;
    try {
        const probeForm = document.createElement("form");
        probeForm.method = "get";
        probeForm.action = "/search";
        probeForm.innerHTML =
            '<s-text-field name="q" label="Query" value="signet"></s-text-field>' +
            '<s-select name="status" label="Status" placeholder="Any">' +
            '<s-option value="draft" selected>Draft</s-option>' +
            '<s-option value="active">Active</s-option>' +
            '</s-select>';
        document.body.appendChild(probeForm);

        await customElements.whenDefined("s-text-field");
        await new Promise((r) => setTimeout(r, 300));

        let submitted = null;
        const originalSubmit = HTMLFormElement.prototype.submit;
        HTMLFormElement.prototype.submit = function () {
            submitted = Object.fromEntries(new FormData(this));
        };

        try {
            probeForm.submit();
        } finally {
            HTMLFormElement.prototype.submit = originalSubmit;
        }

        form = {
            submitted,
            textFieldValue: probeForm.querySelector("s-text-field")?.value ?? null,
            selectValue: probeForm.querySelector("s-select")?.value ?? null,
        };

        probeForm.remove();
    } catch (error) {
        form = { failed: String(error) };
    }

    // A real click, through the circuit, into a C# handler, and back. This covers the path that
    // component unit tests cannot: Polaris dispatches the DOM event, the interop module forwards
    // it, and the server re-renders.
    const counterButton = () => Array.from(document.querySelectorAll("s-button"))
        .find((b) => (b.textContent ?? "").trim().startsWith("Primary ("));

    let counter = null;
    const button = counterButton();

    if (button) {
        const before = (button.textContent ?? "").trim();
        let after = before;

        button.click();

        const waited = Date.now() + 10000;
        while (Date.now() < waited) {
            await new Promise((r) => setTimeout(r, 200));
            after = (counterButton()?.textContent ?? "").trim();
            if (after !== before) break;
        }

        counter = { before, after, roundTripped: after !== before };
    }

    return {
        title: document.title,
        counter,
        apiKeyMeta: document.querySelector('meta[name="shopify-api-key"]')?.getAttribute("content") ?? null,
        scripts: Array.from(document.querySelectorAll("script[src]")).map((s) => s.getAttribute("src")),
        appBridgeGlobal: typeof window.shopify,
        // Blazor's per-circuit marker. Zero means the page is static and nothing is clickable.
        markedElements: markedElementCount(),
        counts,
        reconnect,
        assets,
        status,
        form,
        commandForWired: document.querySelector("s-button[commandfor]") !== null,
        tableStructure: (() => {
            const tables = document.querySelectorAll("s-table");
            if (tables.length === 0) return null;

            return {
                headers: document.querySelectorAll("s-table-header").length,
                rows: document.querySelectorAll("s-table-row").length,
                cells: document.querySelectorAll("s-table-cell").length,
                // Rows must live inside s-table-body; appending them to s-table renders nothing.
                rowsInsideBody: Array.from(document.querySelectorAll("s-table-row"))
                    .every((row) => row.parentElement?.tagName.toLowerCase() === "s-table-body"),
            };
        })(),
    };
})()`;

/**
 * The expectations the toolkit makes of a live page.
 *
 * Each one is a failure that unit tests cannot see, because it depends on Shopify's CDN, on static
 * web assets being served, on the render mode, or on the Blazor circuit actually being alive.
 *
 * @param {object} result the probe's output
 * @returns {string[]} one description per failed expectation
 */
function evaluateAssertions(result) {
    const failures = [];
    const check = (ok, description) => {
        console.log(`  ${ok ? "PASS" : "FAIL"}  ${description}`);
        if (!ok) failures.push(description);
    };

    if (result.error) {
        console.log(`  FAIL  the probe could not run: ${result.error}`);
        return [result.error];
    }

    const counts = result.counts ?? {};
    const reconnect = result.reconnect ?? {};
    const assets = result.assets ?? {};

    console.log("assertions:");

    check(result.markedElements > 0, "the Blazor circuit is connected (interactive markers are present)");
    check(
        (counts.notDefined ?? []).length === 0,
        `every listed element is defined by Polaris (not defined: ${(counts.notDefined ?? []).join(", ") || "none"})`,
    );
    check(
        (counts.notUpgraded ?? []).length === 0,
        `every rendered element has upgraded (not upgraded: ${(counts.notUpgraded ?? []).join(", ") || "none"})`,
    );
    check(
        (counts.notRendered ?? []).length === 0,
        `every listed element is actually on the page (never rendered: ${(counts.notRendered ?? []).join(", ") || "none"})`,
    );

    check(result.form?.submitted?.q === "signet", "a Polaris text field's value reaches the submitted form");
    check(result.form?.submitted?.status === "draft", "a Polaris select's selected value reaches the submitted form");
    check(result.counter?.roundTripped === true, "a real click reaches the C# handler and re-renders");
    check(result.commandForWired === true, "a modal or popover is wired declaratively with commandFor");
    check(result.tableStructure?.rowsInsideBody === true, "table rows live inside s-table-body");

    check(reconnect.present === true, "#components-reconnect-modal exists on the page");
    check(reconnect.isDialog === true, "the reconnection element is a <dialog>, which is Blazor's contract");
    check(reconnect.stylesheet !== null, "the reconnection stylesheet is linked");
    check(reconnect.script !== null, "the reconnection script is loaded");

    for (const [path, status] of Object.entries(assets)) {
        check(status === 200, `${path} is served (HTTP ${status})`);
    }

    // One uncaught error is expected when the page is opened outside the Admin: App Bridge has no
    // shop to attach to, refuses to initialise and throws. The app handles that case deliberately (it
    // renders "Opened outside the Shopify Admin"), and the alternative — failing on it — would mean
    // never testing the standalone path, which is how a developer first runs the app.
    const expectedOutsideAdmin = [/App Bridge Next: missing required configuration fields/];
    const unexpectedErrors = (result.pageErrors ?? []).filter(
        (error) => !expectedOutsideAdmin.some((pattern) => pattern.test(error.description || error.text || "")),
    );

    check(
        unexpectedErrors.length === 0,
        `the page raised no unexpected errors (${unexpectedErrors.length} of ${(result.pageErrors ?? []).length} ` +
        "are unexpected; the rest are App Bridge's standalone-mode error)",
    );

    for (const error of unexpectedErrors) {
        console.log(`        ${error.url || "(no url)"}:${error.line ?? "?"} ${error.description || error.text}`);
    }

    // Reported rather than asserted: an element may legitimately be light-DOM, so the absence of a
    // shadow root is information, not a failure.
    // The denominator is the elements that ARE defined, not everything listed: an element the CDN did
    // not define has no shadow root either, and counting it would report a shortfall that is really a
    // missing definition — already reported as a failure of its own above.
    const defined = (counts.listed ?? 0) - (counts.notDefined ?? []).length;
    console.log(
        `  note  shadow roots: ${defined - (counts.withoutShadowRoot ?? []).length}/${defined} ` +
        `of the defined elements have one` +
        ((counts.withoutShadowRoot ?? []).length > 0 ? ` (light DOM: ${counts.withoutShadowRoot.join(", ")})` : ""),
    );

    return failures;
}

let devtools;

try {
    const wsUrl = await findPageTarget();
    devtools = await DevTools.connect(wsUrl);
    await devtools.startDiagnostics();
    await devtools.navigate(url);

    // Give the circuit time to connect and the CDN scripts time to arrive.
    await sleep(2500);

    const result = await devtools.evaluate(probe);
    result.consoleMessages = devtools.consoleMessages;
    result.pageErrors = devtools.pageErrors;
    result.probeKeys = Object.keys(result);

    console.log(JSON.stringify(result, null, 2));

    if (assertMode) {
        console.log("");
        const failures = evaluateAssertions(result);

        if (failures.length > 0) {
            console.error(`\n${failures.length} assertion(s) failed:`);
            for (const failure of failures) console.error(`  - ${failure}`);
            process.exitCode = 1;
        } else {
            console.log("\nall assertions passed");
        }
    }
} finally {
    devtools?.close();
    chrome.kill("SIGKILL");

    // Chrome can still be writing its profile as it dies; a failure to delete a temporary
    // directory must not turn a successful verification into an error.
    try {
        await rm(profileDir, { recursive: true, force: true, maxRetries: 5, retryDelay: 200 });
    } catch {
        // Left behind in the OS temp directory, which is harmless.
    }
}
