// Phase 8: admin pages default to InteractiveAuto — first visit runs on a server
// circuit while the WASM runtime downloads, later interactivity may run in-browser.
// Mirrors blazor-counter.js's "genuinely interactive, not just markup" proof on an
// admin page: the Features search box is prerendered into the SSR document, but
// FILTERING only works once an interactive runtime (circuit or WASM) attaches
// handlers. A static-SSR page would render the box and ignore the input.
module.exports = async function run(page, ctx) {
  await page.goto(`${ctx.baseUrl}/Admin/Features`, { waitUntil: 'networkidle' });
  await page.locator('[data-testid="features-page"]').waitFor({ timeout: 20000 });

  // blazor.web.js exposes window.Blazor once the Web App runtime boots — the
  // interactivity precondition for both circuit and WASM phases.
  const blazorBooted = await page
    .waitForFunction(() => typeof window.Blazor !== 'undefined', { timeout: 20000 })
    .then(() => true)
    .catch(() => false);

  const cards = page.locator('[data-feature-id]');
  await cards.first().waitFor({ timeout: 20000 });
  const initialCount = await cards.count();
  const targetId = (await cards.first().getAttribute('data-feature-id')) || '';

  const search = page.getByPlaceholder('Search name, ID, category, description, or dependency...');
  await search.fill(targetId);
  // The filter applies on change/blur, not per keystroke — same as features-page.js.
  await search.press('Tab');
  const filtered = await page
    .waitForFunction(
      ({ initialCount }) => document.querySelectorAll('[data-feature-id]').length < initialCount,
      { initialCount },
      { timeout: 15000 },
    )
    .then(() => true)
    .catch(() => false);
  const filteredCount = await cards.count();
  await search.fill('');

  return [
    { name: 'blazor-web-runtime-boots', pass: blazorBooted, message: blazorBooted ? 'window.Blazor present' : 'window.Blazor never appeared' },
    {
      name: 'interactive-handlers-attached',
      pass: filtered && filteredCount < initialCount && filteredCount >= 1,
      message: `initial=${initialCount} filtered=${filteredCount} target=${targetId || 'none'}`,
    },
  ];
};
