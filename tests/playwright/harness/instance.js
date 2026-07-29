const { chromium } = require('playwright');

// Launches exactly one browser/context/page for an entire suite run. Every feature
// check in the suite reuses this same page instead of each script launching its own
// browser — that's the whole point: one login, one warm process, many checks.
async function createInstance(opts = {}) {
  const headed = process.env.HEADED === '1';
  const debugConsole = process.env.DEBUG_CONSOLE === '1';

  const browser = await chromium.launch({ headless: !headed });
  const context = await browser.newContext({
    viewport: { width: 1440, height: 1000 },
    deviceScaleFactor: 1,
    ...opts.contextOptions,
  });
  const page = await context.newPage();

  const consoleErrors = [];
  page.on('console', message => {
    const line = `[${message.type()}] ${message.text()}`;
    if (message.type() === 'error') {
      consoleErrors.push(line);
    }
    if (debugConsole) {
      console.log(`[browser:${message.type()}] ${message.text()}`);
    }
  });
  page.on('pageerror', error => {
    consoleErrors.push(`[pageerror] ${error.message}`);
    console.log(`[browser:pageerror] ${error.message}`);
  });

  return { browser, context, page, consoleErrors };
}

// The same "ignore known-noisy browser errors" filter every existing script hand-rolled
// separately (favicon 404s, the dev refresh websocket, etc). Pulled out once so feature
// checks share one definition instead of forty copies of the same regex.
function severeConsoleErrors(consoleErrors) {
  const noise = /favicon|Failed to load resource.*404|browser refresh server|WebSocket connection to .*localhost/i;
  return consoleErrors.filter(line => !noise.test(line));
}

// Since checks share one page/console-error array across the whole suite run, each
// check should drain (read + clear) the buffer at its own boundary rather than see
// errors accumulated by earlier, unrelated checks.
function drainConsoleErrors(consoleErrors) {
  const drained = consoleErrors.splice(0, consoleErrors.length);
  return drained;
}

module.exports = { createInstance, severeConsoleErrors, drainConsoleErrors };
