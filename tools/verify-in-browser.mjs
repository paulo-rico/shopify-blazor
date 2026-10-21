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
     * The viewport point at the centre of the `<button>` inside the nth accordion header.
     *
     * Not the centre of the `s-clickable` host: that element computes to `display: contents`, so it
     * has a 0×0 box and its centre is the top-left corner of the page. The button in its shadow root
     * is the box a merchant actually clicks, which is also why this measures instead of assuming.
     * Re-measured on every call, because opening a section moves every header below it.
     */
    async headerButtonCentre(index) {
        return this.evaluate(`(() => {
            const item = document.querySelectorAll("[data-polaris-accordion-item]")[${index}];
            const button = item?.querySelector("s-clickable")?.shadowRoot?.querySelector("button");
            if (!button) return null;

            button.scrollIntoView({ block: "center" });
            const box = button.getBoundingClientRect();
            return {
                x: Math.round(box.x + box.width / 2),
                y: Math.round(box.y + box.height / 2),
                width: box.width,
                height: box.height,
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
     * Presses a key: a keydown and a keyup, as a hardware keyboard sends them.
     *
     * `Input.insertText` is not enough for Enter, Tab and Escape: those are keys, not text, and the
     * grid's handler reads `event.key`. The virtual key codes are the Windows ones — a keydown without
     * one carries `key` but not the `code` a browser derives from it.
     *
     * @param {string} key one of "Enter", "Tab" or "Escape"
     */
    async pressKey(key) {
        const codes = { Enter: 13, Tab: 9, Escape: 27 };
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
        const panel = item.querySelector("[data-polaris-accordion-panel]");
        const header = item.querySelector("s-clickable");
        const chevron = item.querySelector("[data-polaris-accordion-chevron]");
        const heading = item.querySelector("s-section");
        const chevronBox = chevron?.getBoundingClientRect();
        const headerBox = header?.getBoundingClientRect();
        return {
            open: item.getAttribute("data-open"),
            height: panel ? panel.getBoundingClientRect().height : -1,
            inert: item.querySelector("[data-polaris-accordion-panel-inner]")?.hasAttribute("inert") ?? null,
            // The corners belong to the accordion and not to the sections. Read from the element each
            // one paints with: both hosts compute to display:contents and paint inside their shadow
            // roots, so a host's own border-radius is 0px whatever it was asked for.
            itemRadius: header?.shadowRoot?.querySelector("button")
                ? getComputedStyle(header.shadowRoot.querySelector("button")).borderTopLeftRadius
                : null,
            // Read from the shadow root on purpose: an attribute on the <s-clickable> host is not what
            // assistive technology sees.
            expanded: header?.shadowRoot?.querySelector("button")?.getAttribute("aria-expanded") ?? null,
            // The heading on the left and the chevron on the right is a layout claim only a rendered
            // page can confirm.
            chevronOnTheRight: Boolean(chevronBox && headerBox && heading) &&
                chevronBox.left >= heading.getBoundingClientRect().right - 1 &&
                chevronBox.left > headerBox.left + headerBox.width / 2,
            // The heading is an attribute in Polaris's shadow root, so the surface's own text is empty;
            // the name has to be stated or the control has none.
            name: header?.getAttribute("accessibilityLabel") ?? null,
            // The chevron rotating is the only sighted affordance for the state, and the transition
            // duration is the stylesheet actually applying rather than merely loading.
            chevron: chevron ? getComputedStyle(chevron).transform : null,
            transition: panel ? getComputedStyle(panel).transitionDuration : null,
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

    // The accordion is the toolkit's only composed component, so its open state is the library's
    // rather than Shopify's. Four things are invisible to a unit test: that the panel really grows,
    // that aria-expanded reaches the <button> inside s-clickable's shadow root (it is written there
    // through the interop layer — the element has no property for the state, and no light-DOM
    // attribute can reach the shadow button), that the chevron really sits at the right-hand end of
    // the header, and that opening one section collapses the open one in the same frame.
    let accordion = null;
    {
        const items = Array.from(document.querySelectorAll("[data-polaris-accordion-item]"));

        if (items.length >= 2) {
            const closed = items.find((item) => item.getAttribute("data-open") === "false");
            const closedIndex = closed ? items.indexOf(closed) : -1;

            // The click itself happens on the Node side, with a real mouse event: element.click() is
            // the untrusted path Polaris special-cases, and the merchant's click lands on the button
            // inside a shadow root instead. Only the coordinates are computed here.
            let headerPoint = null;
            if (closed) {
                const box = closed.querySelector("s-clickable").getBoundingClientRect();
                headerPoint = {
                    x: Math.round(box.x + box.width / 2),
                    y: Math.round(box.y + box.height / 2),
                };
            }

            accordion = {
                count: items.length,
                closedIndex,
                before: ${accordionStateExpression},
                after: null,
                headerPoint,
                // The accordion's own box, which is where the corners are. s-box paints inside its
                // shadow root, so the radius is read from the element that actually draws it.
                containerRadius: (() => {
                    const painted = items[0]?.closest("s-box")?.shadowRoot?.querySelector(".box");
                    return painted ? getComputedStyle(painted).borderTopLeftRadius : null;
                })(),
                stylesheet: document.head
                    .querySelector('link[data-polaris-stylesheet*="polaris-accordion.css"]')?.href ?? null,
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

    // The accordion. Its open state is the library's own, and every part of it is invisible to a
    // component test: the clipped panel, the aria-expanded that has to reach a shadow button, and the
    // sibling that has to collapse with it.
    const accordion = result.accordion;
    check(accordion !== null, "the composed accordion is on the page with at least two sections");

    if (accordion) {
        const opened = accordion.after?.[accordion.closedIndex];
        const closed = accordion.before?.[accordion.closedIndex];
        const wasOpenIndex = accordion.before.findIndex((item) => item.open === "true");
        const nowCollapsed = wasOpenIndex >= 0 ? accordion.after?.[wasOpenIndex] : null;

        check(accordion.stylesheet !== null, "the accordion's own stylesheet is linked (it is what animates the panel)");
        check(
            accordion.realClickFlipped === true,
            "a real mouse click on the header — not element.click() — opens the section",
        );
        check(
            closed?.open === "false" && closed?.height < 1,
            "a closed section takes no height at all, so its content is really clipped",
        );
        check(
            closed?.inert === true,
            "a closed section is inert, so clipped content leaves the tab order and the accessibility tree",
        );
        check(
            closed?.expanded === "false",
            "a closed section announces aria-expanded=false on the button inside s-clickable's shadow root",
        );
        check(
            opened?.open === "true" && opened?.height > (closed?.height ?? 0),
            `opening a section gives the panel height (${closed?.height ?? "?"}px -> ${opened?.height ?? "?"}px)`,
        );
        check(
            opened?.expanded === "true",
            "opening a section sets aria-expanded=true on that shadow button, which is the only place it can go",
        );
        check(
            opened?.inert === false,
            "an open section's content is no longer inert",
        );
        check(
            nowCollapsed?.open === "false",
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
            !closed?.name ? false : opened?.name === closed?.name,
            `the header has a stated accessible name (${opened?.name ?? "none"})`,
        );
        check(
            // A browser with reduced motion on reports 0s on purpose, so that is not a failure.
            accordion.reducedMotion || (opened?.transition ?? "0s") !== "0s",
            `the panel's transition is applied, not merely linked (duration ${opened?.transition ?? "none"})`,
        );
        check(
            accordion.containerRadius !== "0px" && accordion.containerRadius !== null,
            `the accordion draws the rounded corners (${accordion.containerRadius ?? "none"})`,
        );
        check(
            accordion.before.every((item) => item.itemRadius === "0px"),
            `no section rounds its own corners (${[...new Set(accordion.before.map((i) => i.itemRadius))].join(", ")})`,
        );

        if (accordion.reducedMotion) {
            console.log("  note  reduced motion is on in this browser, so the transition was skipped");
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

    // The accordion's click is driven from here with a REAL mouse event, and the state read back
    // afterwards. Two things about that are deliberate:
    //
    //   - element.click() produces an untrusted event, which Polaris re-dispatches for its own tests.
    //     A merchant's click lands on the <button> inside s-clickable's shadow root instead, and the
    //     toolkit has to survive that path — which is why "the click is hit and miss" was reported
    //     against a check that had only ever exercised the easy one.
    //   - Input.dispatchMouseEvent works in viewport coordinates, so the header is scrolled into view
    //     and the point re-measured first. A click below the fold silently hits nothing.
    if (result.accordion?.headerPoint) {
        const point = (await devtools.headerButtonCentre(result.accordion.closedIndex)) ?? result.accordion.headerPoint;

        await devtools.clickAt(point.x, point.y);

        const flippedBy = Date.now() + 10000;
        while (Date.now() < flippedBy) {
            const open = await devtools.evaluate(
                `document.querySelectorAll("[data-polaris-accordion-item]")[${result.accordion.closedIndex}]?.getAttribute("data-open")`);

            if (open === "true") break;
            await sleep(100);
        }

        result.accordion.realClickFlipped =
            (await devtools.evaluate(
                `document.querySelectorAll("[data-polaris-accordion-item]")[${result.accordion.closedIndex}]?.getAttribute("data-open")`)) === "true";

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
