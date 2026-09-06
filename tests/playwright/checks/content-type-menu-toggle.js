const { severeConsoleErrors, drainConsoleErrors } = require('../harness/instance');

// The per-type Content-menu designation: flipping "Show in Content menu" on the
// Content Types screen must surface a sidebar entry under Content linking to the
// content-items page filtered to that type (?type=<Name>), and flipping it back
// must remove it. The sync that materializes the entry is DEFERRED to the end of
// the toggle request, so menu assertions reload-and-retry briefly. Type-agnostic:
// uses whatever type the list shows first, and always restores the flag.
module.exports = async function run(page, ctx) {
  const results = [];

  // The sidebar keeps collapsed children out of the DOM entirely, and a tenant's
  // imported layout can nest the Content branch anywhere - so presence is asserted
  // against the navigation API (the same source the sidebar renders from), and the
  // filtered page is then driven through the entry's own URL.
  async function findMenuEntry() {
    return page.evaluate(async () => {
      const response = await fetch('/api/crest/navigation/admin', { credentials: 'include' });
      if (!response.ok) return null;
      const data = await response.json();
      let found = null;
      const walk = items => {
        for (const item of items || []) {
          const url = String(item.url || item.href || '');
          if (url.includes('/Contents/ContentItems?type=')) found = { url, text: item.text || item.textKey || '' };
          walk(item.items);
        }
      };
      walk(Array.isArray(data) ? data : data.items);
      return found;
    });
  }

  async function waitForMenuState(present) {
    for (let attempt = 0; attempt < 10; attempt++) {
      const entry = await findMenuEntry();
      if (!!entry === present) return entry || true;
      await page.waitForTimeout(1500);
    }
    return false;
  }

  await page.goto(`${ctx.baseUrl}/Admin/ContentTypes/List`, { waitUntil: 'networkidle' });
  await page.locator('[data-testid="content-types-page"]').waitFor({ timeout: 20000 });

  const rows = page.locator('.crest-model-list__item');
  await rows.first().waitFor({ timeout: 20000 });
  const typeName = await rows.first().locator('.crest-model-list__item-title').innerText();
  await rows.first().click();
  await page.locator('[data-testid="content-type-detail"]').waitFor({ timeout: 10000 });

  const toggle = page.locator('[data-testid="show-in-content-menu"]');
  const toggleShown = await toggle.waitFor({ timeout: 10000 }).then(() => true).catch(() => false);
  results.push({ name: 'menu-toggle-renders', pass: toggleShown, message: `type=${typeName}` });
  if (!toggleShown) return results;

  // ON: an entry appears in the admin navigation linking to the filtered list.
  await toggle.click();
  const entry = await waitForMenuState(true);
  results.push({ name: 'toggle-on-adds-menu-entry', pass: !!entry, message: `type=${typeName}` });

  if (entry && entry.url) {
    await page.goto(`${ctx.baseUrl}${entry.url}`, { waitUntil: 'networkidle' });
    await page.locator('[data-testid="content-items-page"]').waitFor({ timeout: 20000 });
    const headingShown = await page
      .getByRole('heading', { name: entry.text, exact: true, level: 4 })
      .waitFor({ timeout: 15000 })
      .then(() => true)
      .catch(() => false);
    results.push({
      name: 'menu-entry-opens-filtered-list',
      pass: headingShown,
      message: `url=${entry.url} heading=${entry.text} shown=${headingShown}`,
    });
  }

  // OFF: restore the flag; the entry must leave the menu again.
  await page.goto(`${ctx.baseUrl}/Admin/ContentTypes/List`, { waitUntil: 'networkidle' });
  await page.locator('[data-testid="content-types-page"]').waitFor({ timeout: 20000 });
  await rows.first().click();
  await page.locator('[data-testid="content-type-detail"]').waitFor({ timeout: 10000 });
  await toggle.waitFor({ timeout: 10000 });
  await toggle.click();
  const removed = await waitForMenuState(false);
  results.push({ name: 'toggle-off-removes-menu-entry', pass: removed === true, message: `type=${typeName}` });

  const errors = severeConsoleErrors(drainConsoleErrors(ctx.consoleErrors));
  results.push({ name: 'no-console-errors', pass: errors.length === 0, message: errors.join(' | ') || 'clean' });

  return results;
};
