import '@testing-library/jest-dom/vitest'

/**
 * jsdom implements neither pointer capture nor the scroll-into-view that Radix
 * relies on, so every shadcn `Select` throws `target.hasPointerCapture is not a
 * function` the moment it is opened.
 *
 * This affects any test that touches a Select, not just this page's, so the
 * stubs live in the shared setup rather than in an individual test file. A test
 * that needs the real behaviour can restore it.
 */

if (!Element.prototype.hasPointerCapture) {
  Element.prototype.hasPointerCapture = function hasPointerCapture() {
    return false
  }
}

if (!Element.prototype.setPointerCapture) {
  Element.prototype.setPointerCapture = function setPointerCapture() {
    // Nothing to capture against in jsdom, and nothing reads the value back.
  }
}

if (!Element.prototype.releasePointerCapture) {
  Element.prototype.releasePointerCapture = function releasePointerCapture() {
    // As above.
  }
}

if (!Element.prototype.scrollIntoView) {
  Element.prototype.scrollIntoView = function scrollIntoView() {
    // Radix scrolls the highlighted option into view; the list is fully rendered
    // here, so there is nothing to scroll.
  }
}

/**
 * jsdom has no ResizeObserver, and Radix's dialog and popover primitives
 * construct one while mounting. Without this, any component that opens a dialog
 * throws during render rather than on interaction, which makes the failure look
 * unrelated to whatever the test was actually about.
 */
if (typeof globalThis.ResizeObserver === 'undefined') {
  globalThis.ResizeObserver = class ResizeObserver {
    observe() {}
    unobserve() {}
    disconnect() {}
  }
}

/**
 * jsdom does not implement CacheStorage, which undici (used by fetch) tries to
 * access during module initialization. This causes tests that use fetch to fail
 * with "webidl.util.markAsUncloneable is not a function".
 *
 * We provide a minimal CacheStorage implementation that satisfies the interface
 * without actually caching anything.
 */
if (typeof globalThis.caches === 'undefined') {
  const noopCache = {
    async match() { return undefined; },
    async matchAll() { return []; },
    async add() {},
    async addAll() {},
    async delete() { return false; },
    async keys() { return []; },
    async put() {}
  };

  globalThis.caches = {
    async open() { return noopCache; },
    async has() { return false; },
    async delete() { return false; },
    async keys() { return []; },
    async match() { return undefined; }
  };
}
