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
import { mkdtemp, rm, writeFile } from "node:fs/promises";
import { tmpdir } from "node:os";
import { join } from "node:path";

const assertMode = process.argv.includes("--assert");
const url = process.argv.slice(2).find((argument) => argument.startsWith("http")) ?? "http://127.0.0.1:3459/";
const screenshotIndex = process.argv.indexOf("--screenshot");
const screenshotPath = screenshotIndex >= 0 ? process.argv[screenshotIndex + 1] : null;
const chromeFlagIndex = process.argv.indexOf("--chrome");
const chromePath = chromeFlagIndex >= 0 ? process.argv[chromeFlagIndex + 1] : "google-chrome";

const profileDir = await mkdtemp(join(tmpdir(), "polaris-probe-"));
const port = 9222 + Math.floor(Math.random() * 500);

const chrome = spawn(chromePath, [
    "--headless=new",
    "--disable-gpu",
    "--no-first-run",
    "--no-default-browser-check",
    // A taller window than the 800x600 headless default: a control anchored near the bottom of the
    // page opens its panel downwards, and a 485px viewport puts that panel below the fold where a
    // synthetic drag lands on nothing at all.
    "--window-size=1280,900",
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

    /**
     * Clicks a point with a real mouse event.
     *
     * `element.click()` is not the same path a merchant takes: it produces an untrusted event, which
     * Polaris deliberately re-dispatches for its own tests. A real click lands on the `<button>` inside
     * a shadow root, which is what the toolkit has to survive — so the accordion is driven this way.
     */
    async clickAt(x, y) {
        const shared = { x, y, button: "left", clickCount: 1 };
        await this.send("Input.dispatchMouseEvent", { type: "mouseMoved", x, y, button: "none", buttons: 0 });
        await this.send("Input.dispatchMouseEvent", { type: "mousePressed", buttons: 1, ...shared });
        await this.send("Input.dispatchMouseEvent", { type: "mouseReleased", buttons: 0, ...shared });
    }

    /** Scrolls the element into view and returns the viewport point at the centre of its box. */
    async centreOf(selector, index = 0) {
        return this.evaluate(`(() => {
            const element = document.querySelectorAll(${JSON.stringify(selector)})[${index}];
            if (!element) return null;

            // Input.dispatchMouseEvent works in viewport coordinates, so the target has to be on
            // screen first — otherwise the click lands on whatever is at that point, or on nothing.
            element.scrollIntoView({ block: "center", inline: "center" });
            const box = element.getBoundingClientRect();
            return { x: Math.round(box.x + box.width / 2), y: Math.round(box.y + box.height / 2), width: box.width, height: box.height };
        })()`);
    }

    /**
     * The viewport point at the centre of the nth accordion header.
     *
     * The header is a native `<summary>` now, so it is its own painted box. The `s-clickable` it
     * replaced computed to `display: contents` and left nothing to measure but the button inside its
     * shadow root, which is why this helper existed at all. Re-measured on every call, because opening
     * a section moves every header below it.
     */
    async headerButtonCentre(index) {
        return this.evaluate(`(async () => {
            const summary = document.querySelectorAll("[data-polaris-accordion-item]")[${index}]?.querySelector("summary");
            if (!summary) return null;

            const behaviour = document.documentElement.style.scrollBehavior;
            document.documentElement.style.scrollBehavior = "auto";
            summary.scrollIntoView({ block: "center", inline: "center" });
            document.documentElement.style.scrollBehavior = behaviour;

            await new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)));

            const box = summary.getBoundingClientRect();
            return {
                x: Math.round(box.x + box.width / 2),
                y: Math.round(box.y + box.height / 2),
                width: Math.round(box.width),
                height: Math.round(box.height),
            };
        })()`);
    }

    /**
     * The viewport point at the centre of the `<button>` inside the first element matching an
     * expression, or null when there is none.
     *
     * Three things make this different from measuring the element itself, and each one cost a run of
     * checks that reported "the click does nothing":
     *
     *   - An `s-button` host computes to `display: contents`, so its own box is 0×0 at the page origin.
     *     The button inside its shadow root is the box a merchant clicks.
     *   - `Input.dispatchMouseEvent` works in viewport coordinates, so the element has to be on screen.
     *   - Smooth scrolling makes a point measured on the same tick as the scroll a point the element
     *     has left by the time the click arrives, so the scroll is forced to be instant and then
     *     given two frames to settle.
     *
     * @param {string} matchExpression a JavaScript expression returning the host element or null
     */
    async shadowButtonCentre(matchExpression, block = "center") {
        return this.evaluate(`(async () => {
            const element = ${matchExpression};
            const button = element?.shadowRoot?.querySelector("button") ?? element;
            if (!button) return null;

            const behaviour = document.documentElement.style.scrollBehavior;
            document.documentElement.style.scrollBehavior = "auto";
            button.scrollIntoView({ block: ${JSON.stringify(block)}, inline: "center" });
            document.documentElement.style.scrollBehavior = behaviour;

            await new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)));

            const box = button.getBoundingClientRect();
            return box.width > 0 && box.height > 0
                ? { x: Math.round(box.x + (box.width / 2)), y: Math.round(box.y + (box.height / 2)), width: Math.round(box.width) }
                : null;
        })()`);
    }

    /**
     * The text of one cell, addressed by column id and row key.
     *
     * Addressed rather than positional because the grid re-queries: renaming a product moves it in a
     * sort that is by title, so "the first row" after a save is a different record — measured, and the
     * reason an assertion written against the first row's text reported a working save as a revert.
     *
     * @param {string} columnId the column's id, as the cell's data attribute carries it
     * @param {string} rowKey the row key
     */
    async cellText(columnId, rowKey) {
        return this.evaluate(`(() => {
            const cell = Array.from(document.querySelectorAll("[data-polaris-grid-cell]")).find((candidate) =>
                candidate.getAttribute("data-polaris-grid-cell") === ${JSON.stringify(columnId)} &&
                candidate.getAttribute("data-polaris-grid-row") === ${JSON.stringify(String(rowKey))});

            return (cell?.textContent ?? "").replace(/\\s+/g, " ").trim();
        })()`);
    }

    /**
     * Waits until a condition the page reports becomes true, or gives up.
     *
     * The grid's edits cross a Blazor Server circuit, so nothing about them is synchronous: the editor
     * appears a round trip after the click, and the row moves a round trip after the key. Polling is what
     * a test of an asynchronous UI has to do; a fixed sleep is either too short or too slow.
     *
     * @param {() => Promise<boolean>} condition re-evaluated until it holds
     * @param {number} timeoutMs how long to wait
     * @returns {Promise<boolean>} whether it became true
     */
    async waitFor(condition, timeoutMs = 5000) {
        const deadline = Date.now() + timeoutMs;

        while (Date.now() < deadline) {
            if (await condition()) return true;
            await sleep(100);
        }

        return false;
    }

    /**
     * Selects the focused field's whole value and types over it.
     *
     * Two steps because a merchant does two things: `select()` is what Ctrl+A does to the input inside
     * the field's shadow root, and `Input.insertText` is what a keyboard produces. Setting the value
     * from JavaScript instead would skip the `input` event the grid listens for, and a test that skips it
     * proves nothing about typing.
     *
     * @param {string} text what to type
     * @returns {Promise<boolean>} false when no field was focused, which is itself a finding
     */
    async replaceFocusedFieldText(text) {
        const selected = await this.evaluate(`(() => {
            const host = document.activeElement;
            const input = host?.shadowRoot?.querySelector("input, textarea");
            if (!input) return false;
            input.focus();
            input.select();
            return true;
        })()`);

        if (!selected) {
            return false;
        }

        await this.send("Input.insertText", { text });
        return true;
    }

    /**
     * Focuses the inner input of the gallery's autocomplete.
     *
     * Typing has to land in the shadow input of a real search field: the component filters on the
     * `input` event, so setting `value` from JavaScript would prove nothing about typing — and
     * `Input.insertText` goes to whatever has focus, which is this element once focused.
     *
     * @returns {Promise<boolean>} false when the gallery has no autocomplete field
     */
    async focusGalleryAutocomplete() {
        return await this.evaluate(`(() => {
            const host = document.querySelector("[data-gallery-autocomplete] s-text-field");
            const input = host?.shadowRoot?.querySelector("input");
            if (!input) return false;
            input.focus();
            return true;
        })()`);
    }

    /**
     * Presses a key: a keydown and a keyup, as a hardware keyboard sends them.
     *
     * `Input.insertText` is not enough for Enter, Tab and Escape: those are keys, not text, and the
     * grid's handler reads `event.key`. The virtual key codes are the Windows ones — a keydown without
     * one carries `key` but not the `code` a browser derives from it.
     *
     * @param {string} key one of "Enter", "Tab" or "Escape"
     */
    async pressKey(key) {
        // Arrow codes are the Windows virtual-key codes, like the rest: a keydown without one carries
        // `key` but not the `code` a browser derives from it.
        const codes = { Enter: 13, Tab: 9, Escape: 27, ArrowUp: 38, ArrowDown: 40 };
        const code = codes[key] ?? 0;
        const shared = {
            key,
            code: key,
            windowsVirtualKeyCode: code,
            nativeVirtualKeyCode: code,
        };

        await this.send("Input.dispatchKeyEvent", { type: "keyDown", ...shared });
        await this.send("Input.dispatchKeyEvent", { type: "keyUp", ...shared });
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
/**
 * Reads the accordion back out of the page.
 *
 * Shared by the probe and by the re-read that follows the real mouse click, so both describe the same
 * thing and the second one cannot drift from the first.
 */
const accordionStateExpression = `Array.from(document.querySelectorAll("[data-polaris-accordion-item]")).map((item) => {
        const details = item.querySelector("details");
        const summary = item.querySelector("summary");
        const chevron = item.querySelector("[data-polaris-accordion-chevron]");
        const heading = item.querySelector("s-heading");
        const summaryBox = summary?.getBoundingClientRect();
        const detailsBox = details?.getBoundingClientRect();
        const chevronBox = chevron?.getBoundingClientRect();
        return {
            open: details ? details.open : null,
            // A closed <details> does not render its content at all, so the panel's height is what the
            // element adds beyond its header.
            height: summaryBox && detailsBox ? Math.round(detailsBox.height - summaryBox.height) : -1,
            // The disclosure's name, assembled from the ids it points at. The browser's own name
            // computation is the accessibility tree's, which only CDP can read — that half is asserted
            // on the Node side.
            labelledBy: summary?.getAttribute("aria-labelledby") ?? null,
            nameText: (summary?.getAttribute("aria-labelledby") ?? "")
                .split(" ")
                .filter(Boolean)
                .map((id) => document.getElementById(id)?.textContent?.trim() ?? "")
                .join(" | "),
            headingText: heading?.textContent?.trim() ?? null,
            // The corners belong to the accordion and not to the sections: read from the summary, which
            // is what would paint its own if the stylesheet were lost.
            itemRadius: summary ? getComputedStyle(summary).borderTopLeftRadius : null,
            // The heading on the left and the chevron at the header's right-hand edge is a layout claim
            // only a rendered page can confirm. It is measured against the summary rather than the
            // heading's box: an s-heading host computes to display: contents and has no box at all.
            chevronOnTheRight: Boolean(chevronBox && summaryBox) &&
                chevronBox.left > summaryBox.left + summaryBox.width / 2 &&
                chevronBox.right >= summaryBox.right - 24,
            // The chevron rotating is the only sighted affordance for the state, and the transition
            // duration is the stylesheet actually applying rather than merely loading.
            chevron: chevron ? getComputedStyle(chevron).transform : null,
            // The panel's motion lives on ::details-content, which is the only way to animate a native
            // disclosure. A browser without the pseudo-element reports nothing, and the check allows it.
            transition: (() => {
                try {
                    return details ? (getComputedStyle(details, "::details-content").transitionDuration || "") : "";
                } catch {
                    return "";
                }
            })(),
            actions: item.querySelectorAll("[data-polaris-accordion-actions]").length,
        };
})`;
/**
 * The filter panel's inset, measured in the page.
 *
 * An `s-popover` paints no padding of its own — measured: the frame inside its shadow root computes to
 * 0px on every side and has the same rect as the slotted panel — so the distance between a control and
 * the panel's edge is entirely this library's to state. It is also the one layout claim no markup
 * assertion can carry: a control flush against the frame renders perfectly and looks broken.
 */
const panelInsetExpression = `(() => {
    const panel = document.querySelector("[data-polaris-filter-panel]");
    const box = panel?.getBoundingClientRect();
    if (!box || box.width === 0) return { present: false };

    // The content's real edge is the nearest of *every* painted box inside the panel, not the first one
    // found: the controls are Polaris hosts that compute to display: contents, so their own rects are
    // 0×0 and the boxes that paint live inside their shadow roots — and the first such box is not
    // necessarily the top one. Only boxes inside the frame count, because the scroll content is longer
    // than the frame and a box below it describes a place the panel does not occupy.
    let left = null;
    let top = null;
    let right = null;

    const visit = (root) => {
        for (const node of root.children) {
            const rect = node.getBoundingClientRect();

            if (rect.width > 0 && rect.height > 0 && rect.top >= box.top - 1 && rect.bottom <= box.bottom + 1) {
                left = Math.min(left ?? Number.POSITIVE_INFINITY, rect.x);
                top = Math.min(top ?? Number.POSITIVE_INFINITY, rect.y);
                right = Math.max(right ?? Number.NEGATIVE_INFINITY, rect.right);
            }

            if (node.shadowRoot) visit(node.shadowRoot);
            visit(node);
        }
    };

    visit(panel);

    return {
        present: true,
        width: Math.round(box.width),
        height: Math.round(box.height),
        painted: left !== null,
        inset: left === null ? null : {
            left: Math.round(left - box.x),
            top: Math.round(top - box.y),
            right: Math.round(box.right - right),
        },
    };
})()`;

/**
 * Reads the data grid back out of the page.
 *
 * The grid's sorting, filtering and paging are the library's own rather than Polaris's — s-table has
 * none of them — so what is measured here is what a browser alone can answer: that a click on a column
 * heading really sorts, that the filter panel really opens from `commandFor`, and that the slider's
 * track and thumb have real geometry to drag.
 */
const gridStateExpression = `(() => {
    const headers = Array.from(document.querySelectorAll("s-table-header"));
    const rows = Array.from(document.querySelectorAll("s-table-row"));
    const rowText = (row) => (row?.textContent ?? "").replace(/\\s+/g, " ").trim();

    // Not headers[0]: a selectable grid puts its select-all checkbox in the first heading cell, so the
    // first column's heading is the second one. The sortable heading is the one that contains a
    // control, which keeps this independent of how many leading utility columns there are.
    const sortButton = Array.from(document.querySelectorAll("s-table-header"))
        .map((header) => header.querySelector("s-button"))
        .find((button) => button !== null) ?? null;

    const popover = document.querySelector("s-popover");
    const popoverBox = popover?.getBoundingClientRect();

    const slider = document.querySelector("[data-polaris-range-slider]");
    const track = slider?.querySelector("[data-polaris-range-track]");
    const lower = track?.querySelector("input[data-polaris-range-handle=lower]");
    const upper = track?.querySelector("input[data-polaris-range-handle=upper]");
    const trackBox = track?.getBoundingClientRect();

    // The record range is its own text run. Reading the whole nav would concatenate the page-size
    // options and the page numbers into it, which reported "1–5 of 1451025123".
    const pager = document.querySelector('nav[aria-label="Pagination"]');

    return {
        // The grid marks its own section, so a page with a plain table is not mistaken for one with a
        // grid: the gallery has the former and the dashboard the latter, and the same probe runs on
        // both.
        present: document.querySelector("[data-polaris-data-grid]") !== null,
        headers: headers.length,
        rows: rows.length,
        cellsPerRow: rows[0] ? rows[0].querySelectorAll("s-table-cell").length : 0,
        names: rows.slice(0, 3).map(rowText),
        sortButton: sortButton !== null,
        sortIcon: sortButton?.getAttribute("icon") ?? null,
        sortLabel: sortButton?.getAttribute("accessibilityLabel") ?? null,
        // A closed Polaris popover still computes to visibility: visible; what it does not have is a
        // box. Geometry is the honest signal, and it is also what makes a control inside it measurable.
        popoverHeight: popoverBox ? Math.round(popoverBox.height) : 0,
        rangeText: (pager?.querySelector("s-text")?.textContent ?? "").replace(/\\s+/g, " ").trim(),
        chips: document.querySelectorAll("s-clickable-chip").length,
        slider: slider && track ? {
            present: true,
            // The slider's own width, which is 0 while the panel that holds it is closed: this is the
            // measurement that says "the panel really opened", since the host element's box cannot.
            width: Math.round(slider.getBoundingClientRect().width),
            start: getComputedStyle(slider).getPropertyValue("--polaris-range-start").trim(),
            end: getComputedStyle(slider).getPropertyValue("--polaris-range-end").trim(),
            trackWidth: trackBox ? Math.round(trackBox.width) : 0,
            // The declared domain and both ends, so "the handle did not move" can be told apart from
            // "the handle was already where the drag was aiming".
            min: lower?.getAttribute("min") ?? null,
            max: lower?.getAttribute("max") ?? null,
            step: lower?.getAttribute("step") ?? null,
            lowerValue: lower?.value ?? null,
            upperValue: upper?.value ?? null,
            // The thumb's position is not the input's: the input spans the whole track and only the
            // thumb inside it is a pointer target. Nor is the thumb at a bare fraction of the track: a
            // range input insets it by half its width at each end, so a value at the maximum puts its
            // centre half a thumb inside the right edge — which is why a press aimed at the edge missed
            // and the drag did nothing.
            thumbPoint: (() => {
                if (!upper || !trackBox || trackBox.width === 0) return null;
                const min = Number(upper.min);
                const max = Number(upper.max);
                const value = Number(upper.value);
                const ratio = max > min ? (value - min) / (max - min) : 0;
                const inset = 8;
                return {
                    x: Math.round(trackBox.x + inset + (ratio * Math.max(0, trackBox.width - (inset * 2)))),
                    y: Math.round(trackBox.y + (trackBox.height / 2)),
                };
            })(),
            // The two numeric fields are Polaris hosts, so the boxes that paint are inside their shadow
            // roots; the cell each one was given is the box that says whether they ended up side by side.
            fields: Array.from(document.querySelectorAll("[data-polaris-range-inputs] > [data-polaris-range-input]"))
                .map((cell) => {
                    const rect = cell.getBoundingClientRect();
                    return { x: Math.round(rect.x), y: Math.round(rect.y), w: Math.round(rect.width) };
                }),
            // The title, which has to be text on the page and not only an attribute: a label nobody can
            // see is the failure this measures.
            label: (() => {
                const label = document.querySelector("[data-polaris-range-label]");
                if (!label) return null;
                const rect = label.getBoundingClientRect();
                const text = (label.textContent ?? "").replace(/\\s+/g, " ").trim();
                return {
                    text,
                    visible: rect.width > 0 && rect.height > 0 && text.length > 0,
                    // Above the track rather than floating next to it.
                    aboveTrack: trackBox ? rect.bottom <= trackBox.top + 1 : null,
                };
            })(),
        } : { present: false },
    };
})()`;

/**
 * Reads the data grid's editing state out of the page.
 *
 * Separate from the grid reader above because it is read at different moments — before a click, in the
 * middle of an edit, after a save — and the interesting question changes each time. Everything here is
 * something only a browser can answer: whether a click opened an editor, whether the caret is really
 * inside it, and whether a key pressed in a Polaris field's shadow root ever reached the grid's handler.
 */
const editingStateExpression = `(() => {
    const idle = document.querySelectorAll("[data-polaris-grid-cell][tabindex]");
    const editor = document.querySelector("[data-polaris-grid-editing]");
    const active = document.activeElement;
    const bar = document.querySelector("[data-polaris-grid-save-bar]");
    const banner = document.querySelector("[data-polaris-grid-root] s-banner");

    // The caret inside a Polaris field is in its shadow root, so document.activeElement is the *host*:
    // "is focus inside the editor" is a containment question, not an equality one.
    const focusedInsideEditor = Boolean(editor && active && (editor.contains(active) || active === editor));

    return {
        present: document.querySelector("[data-polaris-grid-root]") !== null,
        idleCells: idle.length,
        // A table cell without an editable marker inside it: the selection column and the columns that
        // declared themselves read-only. Counting markers alone cannot see those at all.
        readOnlyCells: Array.from(document.querySelectorAll("s-table-cell"))
            .filter((cell) => cell.querySelector("[data-polaris-grid-cell]") === null).length,
        popoverOpen: (() => {
            const track = document.querySelector("[data-polaris-range-track]");
            return track ? Math.round(track.getBoundingClientRect().width) > 0 : null;
        })(),
        editing: editor !== null,
        editorTag: editor?.firstElementChild?.tagName?.toLowerCase() ?? null,
        editorRow: editor?.getAttribute("data-polaris-grid-row") ?? null,
        editorColumn: editor?.getAttribute("data-polaris-grid-cell") ?? null,
        focusedInsideEditor,
        activeTag: active?.tagName?.toLowerCase() ?? null,
        dirtyCells: document.querySelectorAll("[data-polaris-grid-dirty]").length,
        saveBar: bar !== null,
        saveBarText: (bar?.textContent ?? "").replace(/\\s+/g, " ").trim(),
        bannerText: (banner?.textContent ?? "").replace(/\\s+/g, " ").trim(),
        savedSummary: document.querySelector("[data-sample-saved-summary]")?.textContent?.trim() ?? null,
        // The whole first row's text, so a committed value can be read back as the merchant sees it: the
        // column's formatted value, not the editor's raw one.
        firstRowText: (document.querySelector("s-table-row")?.textContent ?? "").replace(/\\s+/g, " ").trim(),
    };
})()`;

const probe = markedElementHelper
 + `(async () => {
    // The list comes from the page, not from this file. Components.razor renders it, so a component    // added to the gallery is verified here without anyone remembering to update a second list — and
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
        "_content/Polaris.Blazor/polaris-accordion.css",
        "_content/Polaris.Blazor/polaris-range-slider.css",
        "_content/Polaris.Blazor/polaris-grid.css",
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

    // The accordion is the toolkit's only composed component, and its disclosure is a native
    // details/summary — so what a browser adds here is that a real click flips it, that the content
    // really appears and disappears, that the header's action runs without opening the section, and
    // that the action is a node in the accessibility tree at all. That last one is the reason the
    // header stopped being an s-clickable: a button inside its shadow <button> was a button within a
    // button, and the action left the tree.
    let accordion = null;
    {
        const items = Array.from(document.querySelectorAll("[data-polaris-accordion-item]"));
        const closedItem = items.find((item) => item.querySelector("details")?.open === false);

        if (items.length >= 2 && closedItem) {
            const closedIndex = items.indexOf(closedItem);
            const headerBox = closedItem.querySelector("summary").getBoundingClientRect();

            // The click itself happens on the Node side, with a real mouse event: element.click() is
            // not the path a merchant takes. Only the coordinates are computed here.
            accordion = {
                count: items.length,
                closedIndex,
                before: ${accordionStateExpression},
                after: null,
                headerPoint: {
                    x: Math.round(headerBox.x + headerBox.width / 2),
                    y: Math.round(headerBox.y + headerBox.height / 2),
                },
                // The accordion's own box, which is where the corners are. s-box paints inside its
                // shadow root, so the radius is read from the element that actually draws it.
                containerRadius: (() => {
                    const painted = items[0]?.closest("s-box")?.shadowRoot?.querySelector(".box");
                    return painted ? getComputedStyle(painted).borderTopLeftRadius : null;
                })(),
                stylesheet: document.head
                    .querySelector('link[data-polaris-stylesheet*="polaris-accordion.css"]')?.href ?? null,
                // Whether the page has a header action to drive, and whether this browser can animate
                // ::details-content at all — the two things the Node side needs to know before it
                // asserts anything about them.
                hasAction: items.some((item) => item.querySelector("[data-polaris-accordion-actions]")),
                supportsDetailsContent: CSS.supports("selector(::details-content)"),
                reducedMotion: matchMedia("(prefers-reduced-motion: reduce)").matches,
            };
        }
    }

    return {
        title: document.title,
        counter,
        accordion,
        // Awaited, because the reader scrolls and waits for the scroll to settle before measuring —
        // an un-awaited promise here serialises as an empty object, and every grid assertion then reads
        // a grid of zero headings.
        grid: await ${gridStateExpression},
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
        autocomplete: { present: document.querySelector("[data-gallery-autocomplete] s-text-field") !== null },
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
    // Asserted only where the page has the counter button: this probe also runs against pages that do
    // not render the component gallery, and a check that cannot exist on a page must not be reported as
    // a failure there. The gallery page is where this one earns its keep.
    if (result.counter) {
        check(result.counter.roundTripped === true, "a real click reaches the C# handler and re-renders");
    } else {
        console.log("  note  no click counter on this page, so the click round trip is not checked here");
    }
    check(result.commandForWired === true, "a modal or popover is wired declaratively with commandFor");
    check(result.tableStructure?.rowsInsideBody === true, "table rows live inside s-table-body");

    check(reconnect.present === true, "#components-reconnect-modal exists on the page");
    check(reconnect.isDialog === true, "the reconnection element is a <dialog>, which is Blazor's contract");
    check(reconnect.stylesheet !== null, "the reconnection stylesheet is linked");
    check(reconnect.script !== null, "the reconnection script is loaded");

    // The accordion. Its disclosure is native, so what is checked here is the part no component test
    // can see: the content really disappears and reappears, a real click drives it, a header action
    // runs without opening the section, and both are separate nodes in the accessibility tree.
    const accordion = result.accordion;
    check(accordion !== null, "the composed accordion is on the page with at least two sections");

    if (accordion) {
        const opened = accordion.after?.[accordion.closedIndex];
        const closed = accordion.before?.[accordion.closedIndex];
        const wasOpenIndex = accordion.before.findIndex((item) => item.open === true);
        const nowCollapsed = wasOpenIndex >= 0 ? accordion.after?.[wasOpenIndex] : null;

        check(accordion.stylesheet !== null, "the accordion's stylesheet is linked (it draws the header band and the motion)");
        check(
            accordion.realClickFlipped === true,
            "a real mouse click on the header — not element.click() — opens the section",
        );
        check(
            closed?.open === false && closed?.height < 1,
            "a closed section renders no content, so it takes no height beyond its header",
        );
        check(
            opened?.open === true && opened?.height > (closed?.height ?? 0),
            `opening a section renders its content (${closed?.height ?? "?"}px -> ${opened?.height ?? "?"}px)`,
        );
        check(
            nowCollapsed?.open === false,
            "the section that was open collapses when another one opens",
        );
        check(
            opened?.chevron !== null && opened?.chevron !== "none",
            `the chevron rotates when the section opens (transform: ${opened?.chevron ?? "none"})`,
        );
        check(
            opened?.chevronOnTheRight === true,
            "the heading is on the left of the header and the chevron at its right-hand end",
        );
        check(
            typeof opened?.nameText === "string" && opened.nameText.includes(opened.headingText ?? "\u0000"),
            `the header's name is the text that is drawn (${opened?.nameText ?? "none"})`,
        );
        check(
            !(opened?.nameText ?? "").includes("Accept changes"),
            "the header action's label is not folded into the disclosure's name",
        );
        // The three below only make sense on a page whose header carries an action; a page without one
        // is not failing them, it just has nothing to check. Same rule as the grid's own checks.
        if (accordion.hasAction) {
            check(
                accordion.action?.clicked === true && accordion.action?.reachedCSharp === true,
                `pressing the header action reaches C# (${accordion.action?.ranBefore ?? "?"} -> ${accordion.action?.ranAfter ?? "?"})`,
            );
            check(
                accordion.action?.wasOpen === false && accordion.action?.openedTheSection === false,
                "pressing the header action leaves a closed section closed",
            );
            check(
                (accordion.a11y?.disclosureRole ?? "").includes("DisclosureTriangle"),
                `the header is a disclosure control, not a button (role ${accordion.a11y?.disclosureRole ?? "unknown"})`,
            );
            check(
                accordion.a11y?.actionName === "Accept changes" && accordion.a11y?.actionIgnored === false,
                "a button in the header is its own node in the accessibility tree",
            );
        } else {
            console.log("  note  this page's accordion has no header action, so the action checks do not apply");
        }

        check(
            // A browser without ::details-content opens instantly, which is the design; reduced motion
            // is a merchant's own choice and is not a failure either.
            accordion.reducedMotion || !accordion.supportsDetailsContent || (opened?.transition ?? "") !== "",
            `the panel's motion comes from the stylesheet (duration ${opened?.transition || "none"})`,
        );
        check(
            accordion.containerRadius !== "0px" && accordion.containerRadius !== null,
            `the accordion draws the rounded corners (${accordion.containerRadius ?? "none"})`,
        );
        check(
            accordion.before.every((item) => item.itemRadius === "0px"),
            `no section rounds its own corners (${[...new Set(accordion.before.map((i) => i.itemRadius))].join(", ")})`,
        );

        if (!accordion.supportsDetailsContent) {
            console.log("  note  this browser cannot animate ::details-content, so the panel snaps");
        }
    }

    // The composed data grid. The table is Polaris's; the sorting, the filter panel and the slider are
    // the toolkit's, and not one of them can be observed by a component test — bUnit asserts that a
    // handler was attached, a browser is the only thing that can say the click arrived.
    //
    // Only where the page has one: the same probe runs against the component gallery, which has a plain
    // Polaris table and no grid, and a check that cannot apply on a page must not report a failure there.
    const grid = result.grid ?? {};

    if (!grid.present) {
        console.log("  note  this page has no composed data grid, so the grid checks do not apply");
    } else {
    check(grid.headers >= 5, `the data grid draws a heading per column (${grid.headers ?? 0})`);
    check(
        grid.rows > 0 && grid.cellsPerRow === grid.headers,
        `every row has one cell per heading (${grid.cellsPerRow ?? 0} of ${grid.headers ?? 0})`,
    );
    check(grid.sortButton === true, "a sortable heading is a button, because s-table offers no sorting");
    check(
        grid.sortClicked === true,
        `a real click on a heading sorts the grid (${grid.afterSort?.sortLabel ?? "no direction reported"})`,
    );
    check(
        grid.afterSort?.names?.[0] !== grid.names?.[0],
        `the row order really changed (${grid.names?.[0] ?? "?"} -> ${grid.afterSort?.names?.[0] ?? "?"})`,
    );
    check(
        grid.popoverBeforeWidth === 0 && (grid.popoverOpenedBy?.sliderWidth ?? 0) > 0,
        `the filter panel opens from the button's commandFor, with no JavaScript of this library's own ` +
        `(content width ${grid.popoverBeforeWidth ?? "?"}px -> ${grid.popoverOpenedBy?.sliderWidth ?? "?"}px)`,
    );
    check(grid.slider?.present === true, "the range filter renders a slider with a track of its own");

    // The two numeric fields on one line, at equal widths. Measured: an `s-stack direction="inline"`
    // around them laid them out one above the other at full width, because a Polaris host computes to
    // `display: contents`; nothing in the markup said so.
    //
    // Read from the snapshot taken with the panel open: every box inside a closed popover is 0×0, so the
    // first measurement of the page would say the fields have no geometry at all — a check that fails
    // for the wrong reason is worse than no check.
    const openSlider = grid.afterDrag?.slider ?? grid.duringDrag?.slider ?? {};
    const fields = openSlider.fields ?? [];

    // Grouped by row, because a panel can hold more than one range filter (this one has two): what the
    // check claims is that each pair shares a line at equal widths, not that the page has one pair.
    const rows = Object.values(fields.reduce((grouped, field) => {
        (grouped[field.y] ??= []).push(field);
        return grouped;
    }, {}));
    const pairs = rows.filter((row) => row.length === 2);
    const alignedAtEqualWidths = (row) =>
        Math.abs(row[0].w - row[1].w) <= 1 &&
        Math.abs(row[0].w - (row[1].x - row[0].x - 8)) <= 1;

    check(
        pairs.length > 0 && pairs.length === rows.length && pairs.every(alignedAtEqualWidths),
        `the min and max fields share one row of two equal columns ` +
        `(${rows.map((row) => row.map((f) => `${f.w}px@x${f.x},y${f.y}`).join(" + ")).join(" | ") || "no fields"})`,
    );

    // The title is text on the page, above the track — not only an `aria-label` that no merchant can see.
    check(
        openSlider.label?.visible === true && openSlider.label.aboveTrack === true &&
        (openSlider.label.text ?? "").length > 0,
        `the range filter shows its title above the track ("${openSlider.label?.text ?? "none"}")`,
    );

    check(
        (grid.popoverOpenedBy?.trackWidth ?? 0) > 50,
        `the track has a measurable width once the panel is open, so the stylesheet resolved ` +
        `(${grid.popoverOpenedBy?.trackWidth ?? 0}px)`,
    );

    // The panel's own inset. Without one the controls sit flush against the popover's frame — a text
    // field's border touching the panel's border — which is a layout fault that renders perfectly.
    check(
        (grid.panel?.inset?.left ?? 0) >= 8 && (grid.panel?.inset?.top ?? 0) >= 8 && (grid.panel?.inset?.right ?? 0) > 0,
        `the filter panel insets its controls from the popover's edge ` +
        `(left ${grid.panel?.inset?.left ?? "?"}px, top ${grid.panel?.inset?.top ?? "?"}px, ` +
        `right ${grid.panel?.inset?.right ?? "?"}px across ${grid.panel?.width ?? "?"}px, ` +
        `content ${grid.panel?.height ?? "?"}px tall)`,
    );

    // The drag reported nothing until it ended. Two halves, and both are needed: the control really was
    // following the pointer (or a drag that did nothing would also "not re-query"), and the result text
    // had not moved yet (or a re-query per pixel would look the same as one per release).
    check(
        (grid.duringDrag?.slider?.end ?? "") !== (grid.beforeDrag?.slider?.end ?? "") &&
        (grid.duringDrag?.slider?.upperValue ?? "") !== (grid.beforeDrag?.slider?.upperValue ?? ""),
        `the interval follows the drag while the mouse is still down ` +
        `(${grid.beforeDrag?.slider?.end ?? "?"} -> ${grid.duringDrag?.slider?.end ?? "?"})`,
    );
    check(
        (grid.duringDrag?.rangeText ?? "") === (grid.beforeDrag?.rangeText ?? "") &&
        (grid.afterDrag?.rangeText ?? "") !== (grid.beforeDrag?.rangeText ?? ""),
        `the grid re-queries once, on release, and not while dragging ` +
        `(during the drag "${grid.duringDrag?.rangeText ?? "?"}", after release "${grid.afterDrag?.rangeText ?? "?"}")`,
    );
    check(
        grid.afterDrag?.slider?.upperValue !== grid.beforeDrag?.slider?.upperValue,
        `dragging the upper handle moves it (${grid.beforeDrag?.slider?.upperValue ?? "?"} -> ${grid.afterDrag?.slider?.upperValue ?? "?"})`,
    );
    check(
        grid.afterDrag?.slider?.end !== grid.beforeDrag?.slider?.end,
        `the highlighted interval follows the handle (${grid.beforeDrag?.slider?.end ?? "?"} -> ${grid.afterDrag?.slider?.end ?? "?"})`,
    );
    check(
        (grid.afterDrag?.rangeText ?? "") !== (grid.beforeDrag?.rangeText ?? ""),
        `the result is re-queried after the drag (${grid.beforeDrag?.rangeText ?? "?"} -> ${grid.afterDrag?.rangeText ?? "?"})`,
    );

    // Spreadsheet editing. Every step below is driven with a real mouse and a real key, and read back
    // from the page: a component test can prove that a handler was attached, and none of these can.
    const opened = grid.editingOpened ?? {};
    const committed = grid.editingCommitted ?? {};
    const escaped = grid.editingEscaped ?? {};
    const saved = grid.editingSaved ?? {};
    const refused = grid.editingRefused ?? {};

    if ((grid.editingBefore?.idleCells ?? 0) > 0) {
        check(
            (grid.editingBefore?.readOnlyCells ?? 0) > 0,
            `only the editable columns get a clickable cell, so a read-only column stays text ` +
            `(${grid.editingBefore.idleCells} editable, ${grid.editingBefore.readOnlyCells} read-only)`,
        );
        check(
            opened.editing === true && opened.editorTag === "s-text-field",
            `a click on a cell draws its editor (${opened.editorTag ?? "nothing appeared"})`,
        );
        check(
            opened.focusedInsideEditor === true,
            `the caret really lands in the editor, inside the field's shadow root ` +
            `(document.activeElement is ${opened.activeTag ?? "nothing"})`,
        );

        // The typed value reached the cell — and as the stored value, not as the raw text the field
        // reported. "AA Renamed" is what was typed over the old title, and the cell is addressed by its
        // row key because a grid sorted by title moves the row as soon as it is renamed.
        check(
            (grid.editingCommittedCell ?? "").includes("AA Renamed"),
            `Enter commits what was typed and the cell shows it ("${grid.editingCommittedCell ?? "?"}")`,
        );
        check(
            committed.editorRow !== opened.editorRow && committed.editorColumn === opened.editorColumn,
            `Enter moves down to the next row's cell, in the same column ` +
            `(row ${opened.editorRow ?? "?"} -> ${committed.editorRow ?? "?"}, ${committed.editorColumn ?? "?"})`,
        );
        check(
            committed.dirtyCells === 1 && committed.saveBar === true,
            `a committed edit is a draft, marked on its cell and counted by the save bar ` +
            `(${committed.dirtyCells ?? "?"} marked, bar: "${committed.saveBarText ?? "none"}")`,
        );
        check(
            escaped.editing === false && escaped.dirtyCells === 1,
            "Escape leaves the editor without committing the cell it was in, and without dropping another cell's draft",
        );

        check(
            saved.saveBar === false && saved.dirtyCells === 0,
            `the save button writes the row and clears the drafts ` +
            `(${saved.dirtyCells ?? "?"} still marked, bar: ${saved.saveBar ?? "?"})`,
        );
        check(
            (grid.editingSavedCell ?? "").includes("AA Renamed"),
            `the saved value is what the store holds after the save, not reverted by a round trip ` +
            `("${grid.editingSavedCell ?? "?"}")`,
        );
        check(
            (grid.editingSavedSummary ?? "nothing yet") !== "nothing yet",
            `the page's save callback really ran and reported what it wrote ("${grid.editingSavedSummary ?? "nothing yet"}")`,
        );

        // A refusal: the price floor is the sample's own rule, and the point is that a rejected save keeps
        // the merchant's typing on screen with the reason beside it rather than dropping either.
        check(
            (refused.bannerText ?? "").toLowerCase().includes("lowest price"),
            `a save the server refuses is shown with its reason ("${refused.bannerText ?? "no message"}")`,
        );
        check(
            refused.dirtyCells === 1 && (grid.editingRefusedCell ?? "").includes("0.50"),
            `a refused edit keeps its draft on screen (${refused.dirtyCells ?? "?"} marked, ` +
            `"${grid.editingRefusedCell ?? "?"}")`,
        );
    } else {
        console.log("  note  this grid is not editable, so the editing checks do not apply");
    }
    }

    // The autocomplete, driven with a real keyboard. Every part of this is invisible to a component
    // test: the `input` event has to reach C# from inside a search field's shadow root, the arrow key
    // has to move focus onto a row button inside another shadow root, Enter has to be the browser's own
    // click on that button, and Escape has to put focus back in the field.
    const autocomplete = result.autocomplete;
    check(autocomplete?.present === true, "the composed autocomplete is on the page");

    if (autocomplete?.present) {
        check(
            (autocomplete.typed?.rows ?? 0) >= 2,
            `typing opens the list and filters it (${autocomplete.typed?.rows ?? 0} rows)`,
        );
        check(
            autocomplete.typed?.panel?.position === "fixed",
            `the suggestion panel is a fixed layer (position: ${autocomplete.typed?.panel?.position ?? "none"})`,
        );
        check(
            autocomplete.typed?.lastRowHit?.inPanel === true,
            `every suggestion is hit-testable, so no ancestor clips the panel ` +
            `(the last row's centre found ${autocomplete.typed?.lastRowHit?.tag ?? "nothing"})`,
        );
        check(
            Number(autocomplete.typed?.panel?.zIndex ?? 0) > 0,
            `the panel stacks over the page content (z-index: ${autocomplete.typed?.panel?.zIndex ?? "auto"})`,
        );
        check(
            Math.abs((autocomplete.typed?.echoTop ?? 0) - (autocomplete.before?.echoTop ?? 0)) < 1,
            `opening the list does not move the page (the line below stayed at ${autocomplete.before?.echoTop ?? "?"}px)`,
        );
        check(
            autocomplete.typed?.panel !== null &&
                autocomplete.typed.panel.bottom > (autocomplete.typed.echoViewportTop ?? 0) &&
                autocomplete.typed.panel.top < (autocomplete.typed.echoViewportTop ?? 0),
            "the panel is drawn over the content below the field, not in place of it",
        );
        check(
            Math.abs((autocomplete.typed?.panel?.width ?? 0) - (autocomplete.typed?.fieldWidth ?? 0)) < 2,
            `the panel matches the field's width (${Math.round(autocomplete.typed?.panel?.width ?? 0)}px vs ${Math.round(autocomplete.typed?.fieldWidth ?? 0)}px)`,
        );
        check(
            autocomplete.arrowed?.active === "S-BUTTON",
            `ArrowDown moves focus onto a row (document.activeElement is ${autocomplete.arrowed?.active ?? "nothing"})`,
        );
        check(
            autocomplete.chosen?.chosen === "Gold",
            `Enter chooses the row focus is on, and C# is told ("${autocomplete.chosen?.chosen ?? "nothing"}")`,
        );
        check(
            autocomplete.chosen?.rows === 0 && autocomplete.chosen?.fieldText === "Gold",
            `choosing closes the list and puts the chosen text in the field ` +
            `(${autocomplete.chosen?.rows ?? "?"} rows, "${autocomplete.chosen?.fieldText ?? "?"}")`,
        );
        check(
            autocomplete.chosen?.active === "S-TEXT-FIELD",
            `choosing returns focus to the field (document.activeElement is ${autocomplete.chosen?.active ?? "nothing"})`,
        );
        check(
            autocomplete.escaped?.active === "S-TEXT-FIELD" && autocomplete.escaped?.rows === 0,
            `Escape closes the list and returns focus to the field ` +
            `(${autocomplete.escaped?.rows ?? "?"} rows, active: ${autocomplete.escaped?.active ?? "nothing"})`,
        );
        check(
            autocomplete.escaped?.fieldText === "Gold" && autocomplete.escaped?.chosen === "Gold",
            `Escape restores the value that is actually chosen ` +
            `("${autocomplete.escaped?.fieldText ?? "?"}", C# holds "${autocomplete.escaped?.chosen ?? "?"}")`,
        );
    }

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

    // The header action comes FIRST, while the section it sits in is still closed: a real click on the
    // button beside the title must reach C# and must NOT open that section. Both halves matter — a
    // click that does nothing looks like a broken action, and a click that also opens the panel is the
    // failure the component's stop/prevent wrapper exists to prevent, and one the component could
    // never hear about. Driven before the header click so "did it open?" is a real question.
    if (result.accordion?.hasAction) {
        const actionExpression =
            `document.querySelector("[data-polaris-accordion-actions] s-button, [data-polaris-accordion-actions] button")`;

        const stateOfActionSection = `(() => {
            const item = document.querySelector("[data-polaris-accordion-actions]")?.closest("[data-polaris-accordion-item]");
            return { open: item?.querySelector("details")?.open === true, runs: document.querySelector("[data-accordion-action-runs]")?.textContent?.trim() ?? null };
        })()`;

        const before = await devtools.evaluate(stateOfActionSection);
        const actionPoint = await devtools.shadowButtonCentre(actionExpression);

        if (actionPoint) {
            await devtools.clickAt(actionPoint.x, actionPoint.y);
            await devtools.waitFor(async () => (await devtools.evaluate(stateOfActionSection)).runs !== before.runs, 5000);
        }

        const after = await devtools.evaluate(stateOfActionSection);

        result.accordion.action = {
            clicked: actionPoint !== null,
            reachedCSharp: after.runs !== before.runs,
            openedTheSection: after.open,
            wasOpen: before.open,
            ranBefore: before.runs,
            ranAfter: after.runs,
        };

        // The accessibility tree, which is the whole reason the header stopped being an s-clickable.
        // Read through CDP rather than the page: only the browser knows what was announced.
        await devtools.send("Accessibility.enable");
        const { nodes } = await devtools.send("Accessibility.getFullAXTree");

        const disclosure = nodes.find((node) =>
            /DisclosureTriangle/.test(node.role?.value ?? "") &&
            (node.name?.value ?? "").includes("A closed section"));
        const action = nodes.find((node) =>
            node.role?.value === "button" && (node.name?.value ?? "") === "Accept changes");

        result.accordion.a11y = {
            disclosureRole: disclosure?.role?.value ?? null,
            disclosureName: disclosure?.name?.value ?? null,
            actionName: action?.name?.value ?? null,
            actionIgnored: action ? action.ignored : null,
        };
    }

    // The accordion's click is driven from here with a REAL mouse event, and the state read back
    // afterwards. Two things about that are deliberate:
    //
    //   - element.click() dispatches an untrusted event, and a merchant's click has to survive the
    //     path the browser actually takes — including deciding whether the summary's own activation
    //     runs. That is the path a header action has to suppress.
    //   - Input.dispatchMouseEvent works in viewport coordinates, so the header is scrolled into view
    //     and the point re-measured first. A click below the fold silently hits nothing.
    if (result.accordion?.headerPoint) {
        const point = (await devtools.headerButtonCentre(result.accordion.closedIndex)) ?? result.accordion.headerPoint;

        await devtools.clickAt(point.x, point.y);

        const openExpression =
            `document.querySelectorAll("[data-polaris-accordion-item]")[${result.accordion.closedIndex}]?.querySelector("details")?.open === true`;

        const flippedBy = Date.now() + 10000;
        while (Date.now() < flippedBy) {
            if (await devtools.evaluate(openExpression)) break;
            await sleep(100);
        }

        result.accordion.realClickFlipped = (await devtools.evaluate(openExpression)) === true;

        // Let the 200ms transition settle, so the heights read are the settled ones.
        await sleep(500);
        result.accordion.after = await devtools.evaluate(accordionStateExpression);
    }

    // The grid is driven next, and everything about it is the library's own behaviour: Polaris has no
    // sorting, no filter panel and no slider, so a unit test can only prove the handler is attached.
    //
    // Order matters. Sorting first proves the heading is a real control; the filter panel is opened
    // afterwards because the slider lives inside it.
    if (result.grid?.sortButton) {
        // Measured immediately before the click, by a helper that scrolls the heading into view: a
        // point computed earlier is a point measured against a different scroll position, and the click
        // then lands on whatever has since moved under it.
        const sortPoint = await devtools.shadowButtonCentre(
            `Array.from(document.querySelectorAll("s-table-header"))
                .map((header) => header.querySelector("s-button"))
                .find((button) => button !== null) ?? null`);

        if (sortPoint) {
            await devtools.clickAt(sortPoint.x, sortPoint.y);

            const sortedBy = Date.now() + 10000;
            while (Date.now() < sortedBy) {
                const icon = await devtools.evaluate(gridStateExpression).then((state) => state.sortIcon);

                if (icon) break;
                await sleep(100);
            }

            result.grid.afterSort = await devtools.evaluate(gridStateExpression);
            result.grid.sortClicked = result.grid.afterSort.sortIcon !== null;
        }
    }

    if (result.grid?.slider?.present) {
        const beforeOpen = await devtools.evaluate(gridStateExpression);
        result.grid.popoverBeforeWidth = beforeOpen.slider?.width ?? 0;

        const filterPoint = await devtools.shadowButtonCentre(
            `Array.from(document.querySelectorAll("s-button"))
                .find((button) => (button.textContent ?? "").trim().startsWith("Filter")) ?? null`);

        if (filterPoint) {
            result.grid.filterClick = filterPoint;

            // What is under the point, measured before the click: a point that lands on a sibling — the
            // search field beside it, say — is a click that does nothing and says nothing.
            result.grid.filterHit = await devtools.evaluate(`(() => {
                const target = document.elementFromPoint(${filterPoint.x}, ${filterPoint.y});
                if (!target) return null;
                const host = target.closest("s-button") ?? target.closest("s-search-field") ?? target;
                return {
                    tag: target.tagName.toLowerCase(),
                    host: host.tagName.toLowerCase(),
                    hostId: host.getAttribute("id") ?? null,
                    text: (host.textContent ?? "").trim().slice(0, 24),
                };
            })()`);

            await devtools.clickAt(filterPoint.x, filterPoint.y);
        }

        // A closed Polaris popover still reports visibility: visible, so the panel is waited for by a
        // control inside it becoming measurable — which is also the precondition for the drag below.
        // Waiting on the wrong signal is how an earlier version dragged against a 0px track.
        const openedBy = Date.now() + 5000;

        while (Date.now() < openedBy) {
            const opened = await devtools.evaluate(
                `(() => {
                    const track = document.querySelector("[data-polaris-range-track]");
                    const slider = document.querySelector("[data-polaris-range-slider]");
                    return {
                        sliderWidth: slider ? Math.round(slider.getBoundingClientRect().width) : 0,
                        trackWidth: track ? Math.round(track.getBoundingClientRect().width) : 0,
                        // Read while the panel is open and still unscrolled: the drag below scrolls the
                        // slider into view, and the insets would then describe a scrolled panel.
                        panel: ${panelInsetExpression},
                    };
                })()`);

            if (opened.trackWidth > 0) {
                result.grid.popoverOpenedBy = opened;
                result.grid.panel = opened.panel;
                break;
            }

            await sleep(150);
        }

        // The panel scrolls: it holds more controls than fit in the height it can claim, so the control
        // to drag may be below the visible edge. A press point measured there lands on space the panel
        // does not occupy — which reported as "the drag does nothing" three times before it was
        // measured. Scrolling it into view is what a merchant does, and what makes the point real.
        await devtools.evaluate(`(async () => {
            const slider = document.querySelector("[data-polaris-range-slider]");
            slider?.scrollIntoView({ block: "center" });
            await new Promise((resolve) => requestAnimationFrame(() => requestAnimationFrame(resolve)));
            return true;
        })()`);

        // A drag of the upper handle: pressed on the thumb, moved in steps (a range input follows
        // pointer moves, not a single jump) and released, which is the interaction a merchant makes.
        const slider = await devtools.evaluate(gridStateExpression);

        if (slider.slider?.thumbPoint && slider.slider.trackWidth > 0) {
            const from = slider.slider.thumbPoint;
            const toX = Math.max(8, Math.round(from.x - (slider.slider.trackWidth * 0.4)));
            const shared = { button: "left", y: from.y };

            await devtools.send("Input.dispatchMouseEvent", { type: "mouseMoved", x: from.x, y: from.y, button: "none", buttons: 0 });
            await devtools.send("Input.dispatchMouseEvent", { type: "mousePressed", x: from.x, y: from.y, button: "left", buttons: 1, clickCount: 1 });

            for (let step = 1; step <= 6; step++) {
                const x = Math.round(from.x + ((toX - from.x) * (step / 6)));
                await devtools.send("Input.dispatchMouseEvent", { type: "mouseMoved", x, buttons: 1, ...shared });
                // Slow enough that the whole drag outlasts the filter's 300ms debounce. A drag shorter
                // than that would let a per-pixel commit go unnoticed even by a check that samples
                // mid-drag, which is a check that cannot fail.
                await sleep(120);
            }

            // Sampled with the mouse still down, which is the whole claim of "do not re-query while
            // dragging": the fill has followed the thumb, and the result has not been recomputed.
            //
            // The pause before sampling is what makes this check able to fail. A merchant holds a handle
            // still all the time, and the filter applies a change 300ms after it stops arriving, so a
            // control that reported on every `input` event would re-query during exactly this hold —
            // while a control that reports on `change` has told the page nothing yet.
            await sleep(600);

            result.grid.duringDrag = await devtools.evaluate(gridStateExpression);

            await devtools.send("Input.dispatchMouseEvent", { type: "mouseReleased", x: toX, buttons: 0, ...shared });

            await sleep(800);
            result.grid.beforeDrag = slider;
            result.grid.afterDrag = await devtools.evaluate(gridStateExpression);
        }
    }

    // Spreadsheet editing, driven with real input, because every part of it is a browser question: a
    // click that opens an editor, a caret that lands inside a shadow root, and a keydown pressed in a
    // Polaris field that has to reach the grid's own handler. A component test can prove none of them.
    if (result.grid?.present) {
        result.grid.editingBefore = await devtools.evaluate(editingStateExpression);

        if ((result.grid.editingBefore?.idleCells ?? 0) > 0) {
            // The filter panel is still open from the checks above, anchored over the top of the table,
            // and a click aimed at a cell lands on its backdrop instead — measured: the editor never
            // opened and focus went to the `s-popover` host. Escape closes it; the paragraph click is the
            // fallback for a popover that does not listen for Escape.
            if (result.grid.editingBefore.popoverOpen) {
                await devtools.pressKey("Escape");

                const closed = await devtools.waitFor(
                    async () => !(await devtools.evaluate(editingStateExpression)).popoverOpen,
                    3000);

                if (!closed) {
                    const away = await devtools.centreOf("s-paragraph");

                    if (away) {
                        await devtools.clickAt(away.x, away.y);
                        await devtools.waitFor(
                            async () => !(await devtools.evaluate(editingStateExpression)).popoverOpen,
                            3000);
                    }
                }
            }

            // The first editable cell of the first row. Measured immediately before the click, like every
            // other point in this script.
            const cellPoint = await devtools.centreOf("[data-polaris-grid-cell][tabindex]");

            if (cellPoint) {
                await devtools.clickAt(cellPoint.x, cellPoint.y);
                await devtools.waitFor(async () => (await devtools.evaluate(editingStateExpression)).editing);
                result.grid.editingOpened = await devtools.evaluate(editingStateExpression);

                // What actually has focus, read separately from the editor's presence: an editor that
                // appears without the caret in it is indistinguishable on screen from one that works, and
                // it is exactly what a Polaris host plus Blazor's own FocusAsync produces.
                result.grid.editingFocus = await devtools.evaluate(`(() => {
                    const active = document.activeElement;
                    return {
                        tag: active?.tagName?.toLowerCase() ?? null,
                        hasShadowRoot: Boolean(active?.shadowRoot),
                        inner: active?.shadowRoot?.querySelector("input, textarea")?.tagName?.toLowerCase() ?? null,
                        editorPresent: document.querySelector("[data-polaris-grid-editing]") !== null,
                    };
                })()`);

                // Select what the editor holds and type over it, then commit with a key. `insertText`
                // produces the same `input` event a keystroke does — it is what a hardware keyboard is
                // translated into — and the selection is what Ctrl+A would do, so the value that lands is
                // exactly the text below rather than the old value with the new one appended.
                result.grid.editingTyped = await devtools.replaceFocusedFieldText("AA Renamed");
                await devtools.pressKey("Enter");
                const openedRow = result.grid.editingOpened?.editorRow;
                await devtools.waitFor(async () => (await devtools.evaluate(editingStateExpression)).editorRow !== openedRow);
                result.grid.editingCommitted = await devtools.evaluate(editingStateExpression);
                result.grid.editingCommittedCell = await devtools.cellText("Title", openedRow);

                // Escape out of the next cell. The draft committed above stays: it is a different cell.
                await devtools.pressKey("Escape");
                await devtools.waitFor(async () => !(await devtools.evaluate(editingStateExpression)).editing);
                result.grid.editingEscaped = await devtools.evaluate(editingStateExpression);

                // Save, through the grid's own button — measured from the shadow root, because an
                // s-button host has no box of its own.
                const savePoint = await devtools.shadowButtonCentre(
                    `Array.from(document.querySelectorAll("[data-polaris-grid-save-bar] s-button"))
                        .find((button) => (button.textContent ?? "").trim() === "Save changes") ?? null`);

                if (savePoint) {
                    await devtools.clickAt(savePoint.x, savePoint.y);
                    await devtools.waitFor(async () => !(await devtools.evaluate(editingStateExpression)).saveBar);

                    // Waited for, not merely read: the callback and the render that shows its answer are
                    // two round trips, and reading between them reported a working save as "nothing yet".
                    await devtools.waitFor(
                        async () => (await devtools.evaluate(editingStateExpression)).savedSummary !== "nothing yet");

                    result.grid.editingSaved = await devtools.evaluate(editingStateExpression);
                    result.grid.editingSavedCell = await devtools.cellText("Title", openedRow);
                    result.grid.editingSavedSummary = result.grid.editingSaved.savedSummary;
                }

                // A save the server refuses: the price floor is the sample's own rule, and the point of
                // measuring it is that a refusal has to keep the merchant's typing on screen with the
                // reason beside it.
                const pricePoint = await devtools.centreOf('[data-polaris-grid-cell="Price"][tabindex]');

                if (pricePoint) {
                    await devtools.clickAt(pricePoint.x, pricePoint.y);
                    await devtools.waitFor(async () => (await devtools.evaluate(editingStateExpression)).editing);
                    const priceRow = (await devtools.evaluate(editingStateExpression)).editorRow;
                    result.grid.editingPriceTyped = await devtools.replaceFocusedFieldText("0.50");
                    await devtools.pressKey("Enter");
                    await devtools.waitFor(async () => (await devtools.evaluate(editingStateExpression)).saveBar);

                    const refusedPoint = await devtools.shadowButtonCentre(
                        `Array.from(document.querySelectorAll("[data-polaris-grid-save-bar] s-button"))
                            .find((button) => (button.textContent ?? "").trim() === "Save changes") ?? null`);

                    if (refusedPoint) {
                        await devtools.clickAt(refusedPoint.x, refusedPoint.y);
                        await devtools.waitFor(async () => (await devtools.evaluate(editingStateExpression)).bannerText.length > 0);
                        result.grid.editingRefused = await devtools.evaluate(editingStateExpression);
                        result.grid.editingRefusedCell = await devtools.cellText("Price", priceRow);
                    }
                }
            }
        }
    }

    // The autocomplete's keyboard path. Focus is the thing being checked, so it is read from
    // document.activeElement after each key — a component test can only prove the interop call was made.
    if (result.autocomplete?.present && await devtools.focusGalleryAutocomplete()) {
        const autocompleteState = `(() => {
            const host = document.querySelector("[data-gallery-autocomplete] s-text-field");
            const panel = document.querySelector("[data-polaris-popup]");
            const box = panel?.getBoundingClientRect();
            const echo = document.querySelector("[data-gallery-autocomplete-value]")?.getBoundingClientRect();
            return {
                rows: document.querySelectorAll("[data-polaris-autocomplete-row]").length,
                labels: Array.from(document.querySelectorAll("[data-polaris-autocomplete-row]"))
                    .map((row) => (row.textContent ?? "").trim().replace(/\\s+/g, " ")),
                chosen: document.querySelector("[data-gallery-autocomplete-value]")?.textContent?.trim() ?? null,
                fieldText: host?.shadowRoot?.querySelector("input")?.value ?? null,
                active: document.activeElement?.tagName ?? null,
                // Document-relative, so scrolling between two reads cannot masquerade as the panel
                // pushing the page down.
                echoTop: echo ? echo.top + window.scrollY : null,
                echoViewportTop: echo?.top ?? null,
                fieldWidth: document.querySelector("[data-polaris-autocomplete-field]")?.getBoundingClientRect().width ?? null,
                panel: box && panel
                    ? {
                        position: getComputedStyle(panel).position,
                        zIndex: getComputedStyle(panel).zIndex,
                        top: box.top,
                        bottom: box.bottom,
                        width: box.width,
                    }
                    : null,
                // The top layer is the anti-clipping mechanism, so the proof is that the LAST row can be
                // hit-tested: with an absolutely positioned panel the second row was outside the
                // section's clip and elementFromPoint found the section instead.
                lastRowHit: (() => {
                    const rows = Array.from(document.querySelectorAll("[data-polaris-autocomplete-row]"));
                    const last = rows[rows.length - 1];
                    if (!last) return null;
                    const rowBox = last.getBoundingClientRect();
                    const hit = document.elementFromPoint(rowBox.left + rowBox.width / 2, rowBox.top + rowBox.height / 2);
                    return { inPanel: Boolean(hit?.closest("[data-polaris-popup]")), tag: hit?.tagName ?? null };
                })(),
            };
        })()`;

        result.autocomplete.before = await devtools.evaluate(autocompleteState);

        await devtools.replaceFocusedFieldText("go");
        await devtools.waitFor(async () => (await devtools.evaluate(autocompleteState)).rows >= 2);
        result.autocomplete.typed = await devtools.evaluate(autocompleteState);

        await devtools.pressKey("ArrowDown");
        await devtools.waitFor(async () => (await devtools.evaluate(autocompleteState)).active === "S-BUTTON");
        result.autocomplete.arrowed = await devtools.evaluate(autocompleteState);

        await devtools.pressKey("Enter");
        await devtools.waitFor(async () => {
            const state = await devtools.evaluate(autocompleteState);
            return state.rows === 0 && state.chosen !== "nothing";
        });
        result.autocomplete.chosen = await devtools.evaluate(autocompleteState);

        // A second pass for Escape: the first one's list is already closed by the selection.
        await devtools.focusGalleryAutocomplete();
        await devtools.replaceFocusedFieldText("si");
        await devtools.waitFor(async () => (await devtools.evaluate(autocompleteState)).rows >= 1);
        await devtools.pressKey("ArrowDown");
        await devtools.waitFor(async () => (await devtools.evaluate(autocompleteState)).active === "S-BUTTON");
        await devtools.pressKey("Escape");
        await devtools.waitFor(async () => (await devtools.evaluate(autocompleteState)).active === "S-TEXT-FIELD");
        result.autocomplete.escaped = await devtools.evaluate(autocompleteState);
    }

    // A picture of the page as it stands after every interaction, which is the only honest answer to
    // "what does it look like?" — and the fastest way to tell a layout fault from a behaviour one.
    if (screenshotPath) {
        const metrics = await devtools.send("Page.getLayoutMetrics");
        const width = Math.ceil(metrics.cssContentSize?.width ?? 1280);
        const height = Math.ceil(metrics.cssContentSize?.height ?? 900);

        const shot = await devtools.send("Page.captureScreenshot", {
            format: "png",
            captureBeyondViewport: true,
            clip: { x: 0, y: 0, width, height, scale: 1 },
        });

        await writeFile(screenshotPath, Buffer.from(shot.data, "base64"));
        console.log(`wrote ${screenshotPath} (${width}x${height})`);
    }

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
