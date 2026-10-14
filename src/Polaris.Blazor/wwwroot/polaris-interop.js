/*
 * Polaris.Blazor — browser interop for Shopify's Polaris web components.
 *
 * Loaded as an ES module from /_content/Polaris.Blazor/polaris-interop.js by
 * PolarisInterop (C#). It owns three things and nothing else:
 *
 *   1. Attaching DOM listeners and forwarding events to .NET.
 *   2. Setting DOM *properties* that cannot be expressed (or cannot be kept) as attributes.
 *   3. Detaching both when a component is disposed or a circuit goes away.
 *
 * Application logic belongs in C#. If this file grows a decision about what a click means,
 * something has gone wrong.
 */

const registry = new Map();          // element -> { objRef, listeners: Map<event, fn> }
let seq = 0;                         // generates DOM ids for components that do not have one

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
    }
}

/**
 * Removes array-valued properties that were set from a previous render.
 *
 * A stale `values` array on `s-choice-list` keeps options selected that the server has since
 * deselected, and nothing about the markup shows it.
 */
export async function clearProperties(element, tagName, names) {
    requireElement(element, "clearProperties");
    await whenDefined(tagName);

    for (const name of names ?? []) {
        element[name] = undefined;
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
