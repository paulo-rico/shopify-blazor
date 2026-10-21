/*
 * Polaris.Blazor — browser interop for Shopify's Polaris web components.
 *
 * Loaded as an ES module from /_content/Polaris.Blazor/polaris-interop.js by
 * PolarisInterop (C#). It owns four things and nothing else:
 *
 *   1. Attaching DOM listeners and forwarding events to .NET.
 *   2. Setting DOM *properties* that cannot be expressed (or cannot be kept) as attributes.
 *   3. Detaching both when a component is disposed or a circuit goes away.
 *   4. Loading this package's stylesheets, which exist only for motion that a media query has to
 *      govern — see polaris-accordion.css.
 *
 * Application logic belongs in C#. If this file grows a decision about what a click means,
 * something has gone wrong.
 */

const registry = new Map();          // element -> { objRef, listeners: Map<event, fn> }
let seq = 0;                         // generates DOM ids for components that do not have one

/**
 * Properties Polaris reads while rendering but does not react to.
 *
 * Most Polaris properties are accessors that schedule a render when they change; these are plain
 * values the render function reads, so writing one on its own changes nothing on screen. Measured
 * in a real browser: setting `s-button.accessibilityExpanded` left the shadow button's
 * `aria-expanded` untouched, and it appeared only once the element rendered again.
 */
const RENDER_ONLY_PROPERTIES = new Set(["accessibilityExpanded"]);

/**
 * Asks an element to render itself, using Polaris's own queue.
 *
 * This is deliberately defensive: `queueRender` is not documented API. If a future Polaris renames
 * it, the property has still been written and simply takes effect on the next render the element
 * does anyway — the state is late rather than wrong.
 *
 * @param {HTMLElement} element the upgraded custom element
 */
function requestRender(element) {
    if (typeof element.queueRender === "function") {
        element.queueRender();
    }
}

// The stylesheets this package ships, loaded here because the alternative — asking every consuming
// app to add a <link> — fails silently: an accordion with no animation still works, and a range
// slider with no track still slides, so nobody ever notices the missing file.
//
//   polaris-accordion.css     — the accordion's motion, which a media query has to govern.
//   polaris-range-slider.css  — the slider's track and thumbs, which Polaris has no element for.
//   polaris-grid.css          — the data grid's editing affordances: the pointer cursor and the
//                               focus ring on a cell, which no Polaris element provides.
const stylesheets = ["./polaris-accordion.css", "./polaris-range-slider.css", "./polaris-grid.css"];

/**
 * Loads this package's stylesheets into the document, once.
 *
 * Guarded by the href, not by a module-level flag: the module can be loaded more than once (a second
 * circuit, a hot reload) and a duplicate <link> would be a second request for the same file.
 */
export function ensureStylesheets() {
    if (!document.head) {
        // Only reachable when the module is imported from <head>, before the document body exists.
        document.addEventListener("DOMContentLoaded", ensureStylesheets, { once: true });
        return;
    }

    for (const path of stylesheets) {
        const href = new URL(path, import.meta.url).href;
        if (document.head.querySelector(`link[data-polaris-stylesheet="${href}"]`)) continue;

        const link = document.createElement("link");
        link.rel = "stylesheet";
        link.href = href;
        link.dataset.polarisStylesheet = href;
        document.head.append(link);
    }
}

ensureStylesheets();

/**
 * Waits until a custom element has been defined and upgraded.
 *
 * This matters more than it looks. Polaris arrives from a CDN and upgrades elements
 * asynchronously; until it does, the element exists as a plain HTMLElement and has none of its
 * properties. Assigning `value` or `checked` to it before upgrade sets an ordinary expando that
 * the upgrade discards, and the field silently renders empty.
 *
 * @param {string} tagName custom element name, e.g. "s-text-field"
 * @param {number} timeoutMs how long to wait before giving up
 * @returns {Promise<boolean>} true when defined, false on timeout
 */
export function whenDefined(tagName, timeoutMs = 10000) {
    if (!tagName) return Promise.resolve(true);
    if (window.customElements?.get(tagName)) return Promise.resolve(true);

    const defined = window.customElements?.whenDefined(tagName) ?? Promise.resolve();

    // A CDN failure never resolves whenDefined, and a page that hangs forever is worse than a
    // page that renders an unupgraded element, so race the promise against a timer.
    const timeout = new Promise((resolve) => setTimeout(() => resolve("timeout"), timeoutMs));

    return Promise.race([defined, timeout]).then((result) => result !== "timeout");
}

/** Resolves an element reference, throwing a diagnosable error rather than a null dereference. */
function requireElement(element, operation) {
    if (!element) {
        throw new Error(`Polaris interop: ${operation} was called with no element.`);
    }
    return element;
}

/** Ensures the element has an id, so C# can address it later (commandFor, interestFor). */
export function ensureId(element) {
    requireElement(element, "ensureId");
    if (!element.id) {
        element.id = `polaris-blazor-${++seq}`;
    }
    return element.id;
}

/**
 * Sets properties on an upgraded custom element.
 *
 * `null` and `undefined` values are skipped rather than written, so a component can pass an
 * optional value through unconditionally. Booleans are written in both forms — as the property
 * and as the attribute — because Polaris reflects booleans from attribute presence, and clearing
 * one form alone leaves the element in its previous state (the documented `loading` trap).
 *
 * @param {HTMLElement} element target element
 * @param {string} tagName custom element name to await
 * @param {Record<string, unknown>} values property names to values
 */
export async function setProperties(element, tagName, values) {
    requireElement(element, "setProperties");
    await whenDefined(tagName);

    let needsRender = false;

    for (const [name, value] of Object.entries(values ?? {})) {
        if (value === null || value === undefined) continue;

        element[name] = value;

        if (typeof value === "boolean") {
            if (value) {
                element.setAttribute(name, "");
            } else {
                element.removeAttribute(name);
            }
        }

        if (RENDER_ONLY_PROPERTIES.has(name)) needsRender = true;
    }

    if (needsRender) requestRender(element);
}

/**
 * Focuses the first thing inside an element that can actually take keyboard focus.
 *
 * `HTMLElement.focus()` on a Polaris host does nothing useful: the host computes to
 * `display: contents` and the input that receives typing lives inside its shadow root. So the search
 * descends — through the element's children and through one shadow boundary — for a real control, and
 * falls back to the element itself for the case where the container is the focusable thing (a grid
 * cell with a tabindex).
 *
 * Measured while building the editable data grid: passing the `s-text-field` host to Blazor's own
 * `ElementReference.FocusAsync` leaves `document.activeElement` on `body`, so the editor appeared with
 * the caret nowhere, and the next keystroke went to the page.
 *
 * @param {HTMLElement} element the container to search
 * @param {string|null} selector narrows the search to the first matching descendant, for a container
 *   that holds more than one focusable thing
 * @returns {Promise<boolean>} true when focus landed inside the element
 */
export async function focusWithin(element, selector = null) {
    requireElement(element, "focusWithin");

    const focusableSelector = "input:not([type=hidden]), textarea, select, [tabindex]:not([tabindex='-1'])";

    const focusable = (node, depth = 0) => {
        if (!node || depth > 8) return null;

        // A shadow root is opaque to querySelector from outside, and for a Polaris field it is the only
        // place the input is. It is searched *whole* rather than from its first element child: measured,
        // a Polaris shadow root begins with a `<style>`, so descending into the first child found nothing
        // and the editor appeared with the caret on `body` — the next keystroke went to the page.
        if (node.shadowRoot) {
            const inside = node.shadowRoot.querySelector(focusableSelector);

            if (inside) return inside;

            for (const child of node.shadowRoot.children) {
                const found = focusable(child, depth + 1);

                if (found) return found;
            }
        }

        if (node.matches?.(focusableSelector)) return node;

        for (const child of node.children ?? []) {
            const found = focusable(child, depth + 1);

            if (found) return found;
        }

        return null;
    };

    // The element may still be upgrading: an element that has no shadow root yet has nothing to focus,
    // and giving up on the first frame would leave the editor unreachable for exactly one keystroke.
    for (let attempt = 0; attempt < 10; attempt++) {
        const container = selector ? element.querySelector(selector) : element;
        const target = container ? focusable(container) : null;

        if (target) {
            target.focus();

            // `document.activeElement` is the outermost host of a focused shadow tree, so the proof
            // that focus arrived is the *root* of the target the caret is in.
            const root = target.getRootNode();
            return (root.activeElement ?? document.activeElement) === target;
        }

        await new Promise((resolve) => requestAnimationFrame(resolve));
    }

    return false;
}

/**
 * Writes `aria-expanded` onto the `<button>` an element renders inside its own shadow root.
 *
 * Two Polaris components are interactive surfaces with a real button inside a shadow root, and only
 * one of them can express this state as a property: `s-button` has `accessibilityExpanded`, and
 * `s-clickable` has no equivalent (measured — its render function reads the accessibility label and
 * the interest/command props, and nothing else). The button Polaris renders is the element assistive
 * technology sees, so that is where the state has to go.
 *
 * Returns false rather than throwing when there is no button to write to, so a Polaris change that
 * moves the markup costs the announcement and nothing else.
 *
 * @param {HTMLElement} element the upgraded custom element
 * @param {boolean} expanded whether what the element controls is open
 * @returns {Promise<boolean>} true when the attribute was written
 */
export async function setShadowButtonExpanded(element, expanded) {
    requireElement(element, "setShadowButtonExpanded");
    await whenDefined(element.localName);

    const button = element.shadowRoot?.querySelector("button");
    if (!button) return false;

    button.setAttribute("aria-expanded", expanded ? "true" : "false");
    return true;
}

/**
 * Clears properties that were set from a previous render.
 *
 * An array-valued property is reset to an empty array rather than to `undefined`, and that is not
 * cosmetic: Polaris reads `s-choice-list.values` with array methods, so `undefined` produces an
 * uncaught "Cannot read properties of undefined (reading 'join')" inside the component. The other
 * failure is the stale array itself, which keeps options selected that the server has since
 * deselected, with nothing in the markup to show it.
 */
export async function clearProperties(element, tagName, names) {
    requireElement(element, "clearProperties");
    await whenDefined(tagName);

    for (const name of names ?? []) {
        element[name] = Array.isArray(element[name]) ? [] : undefined;
    }
}

/**
 * Attaches listeners for the named events and forwards each one to a .NET callback.
 *
 * @param {HTMLElement} element target element
 * @param {string[]} eventNames DOM event names, e.g. ["click", "change"]
 * @param {object} dotNetRef DotNetObjectReference<PolarisEventDispatcher>
 * @param {string} handle handle identifying this registration
 * @param {string} tagName custom element name to await before attaching
 */
export async function attachEvents(element, eventNames, dotNetRef, handle, tagName) {
    requireElement(element, "attachEvents");

    if (!dotNetRef || !eventNames?.length) return;

    detachEvents(element);

    // Attaching before upgrade also works for standard events, because they bubble to the host,
    // but waiting keeps ordering predictable between a listener and the element's own wiring.
    await whenDefined(tagName);

    const listeners = new Map();

    for (const eventName of eventNames) {
        const listener = (event) => {
            try {
                dotNetRef.invokeMethodAsync("Dispatch", handle, eventName, readEvent(event));
            } catch (error) {
                // A disposed circuit rejects the promise. That is normal during teardown, so log
                // and continue rather than surfacing an unhandled rejection in the merchant's
                // console.
                console.debug(`Polaris interop: forwarding '${eventName}' failed.`, error);
            }
        };

        element.addEventListener(eventName, listener);
        listeners.set(eventName, listener);
    }

    registry.set(element, { objRef: dotNetRef, listeners });
}

/**
 * Calls a method on the custom element itself.
 *
 * Polaris elements are upgraded asynchronously, so the method does not exist yet on a freshly
 * rendered element; the call waits for the definition first. `showOverlay()` on a modal is the
 * motivating case.
 *
 * @param {HTMLElement} element target element
 * @param {string} methodName method to call
 * @param {unknown[]} args arguments to pass
 */
export async function invokeElementMethod(element, methodName, args) {
    requireElement(element, "invokeElementMethod");
    await whenDefined(element.localName);

    const method = element[methodName];
    if (typeof method !== "function") {
        throw new Error(`Polaris interop: '${element.localName}' has no method '${methodName}'.`);
    }

    return await method.apply(element, args ?? []);
}

/** Removes every listener this module attached to the element. Safe to call twice. */
export function detachEvents(element) {
    const entry = registry.get(element);
    if (!entry) return;

    for (const [eventName, listener] of entry.listeners) {
        element.removeEventListener(eventName, listener);
    }

    registry.delete(element);
}

/**
 * Detaches listeners and releases the .NET reference. Called from component disposal; the
 * reference must not be disposed here because the owning component reuses it across re-renders.
 */
export function release(element) {
    detachEvents(element);
}

/**
 * Serializes an event into something System.Text.Json can deserialize.
 *
 * Element references and functions cannot cross the interop boundary, so they are dropped and
 * replaced with what a caller actually needs: the field's value, the current target's identity,
 * and the modifier keys.
 */
function readEvent(event) {
    const target = event.currentTarget ?? event.target;

    const detail = {};
    if (event.detail && typeof event.detail === "object") {
        for (const [key, value] of Object.entries(event.detail)) {
            if (typeof value === "function" || value instanceof Node) continue;
            try {
                JSON.stringify(value);
                detail[key] = value;
            } catch {
                // Circular or otherwise unserializable payloads are not worth failing an event for.
            }
        }
    }

    return {
        type: event.type,
        value: typeof event.value === "string" ? event.value : (target?.value ?? null),
        values: Array.isArray(event.values)
            ? event.values
            : Array.isArray(target?.values) ? target.values : null,
        checked: typeof event.checked === "boolean" ? event.checked : (target?.checked ?? null),
        detail: Object.keys(detail).length ? detail : null,
        key: event.key ?? null,
        altKey: event.altKey ?? false,
        ctrlKey: event.ctrlKey ?? false,
        metaKey: event.metaKey ?? false,
        shiftKey: event.shiftKey ?? false,
    };
}

/**
 * Tells .NET which custom elements are actually registered.
 *
 * Used by the diagnostics page and by tests: an element that never upgrades renders as an empty
 * box, and the markup looks perfectly correct while it happens.
 */
export function componentStatus(tagNames) {
    const result = {};

    for (const name of tagNames ?? []) {
        const ctor = window.customElements?.get(name);
        result[name] = {
            defined: Boolean(ctor),
            upgraded: ctor
                ? document.querySelectorAll(name).length ===
                  Array.from(document.querySelectorAll(name)).filter((el) => el instanceof ctor).length
                : false,
            count: document.querySelectorAll(name).length,
        };
    }

    return result;
}

/**
 * True when the page is running inside the Shopify Admin, where App Bridge injects `window.shopify`.
 */
export function isEmbedded() {
    return Boolean(window.shopify);
}
