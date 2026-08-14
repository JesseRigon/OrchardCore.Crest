// Converted from the old admin-queries-page.js
// (modules/OrchardCore.Crest/tests/playwright/admin-queries-page.js). Covers, in order:
// page render, query catalog API shape, a full create-then-delete lifecycle through the
// Crest query editor UI, and confirming the legacy raw-SQL console route still resolves
// through the Crest legacy iframe rather than being hijacked by the native "All Queries"
// Blazor page.
//
// This check mutates state (creates and deletes a temporary "crest-playwright-*" query) the
// same way the original script did — that's inherent to what it's testing, not something
// introduced by the conversion. Leftover queries from a previous aborted run are swept up
// before asserting anything new, same as the original.
module.exports = async function run(page, ctx) {
  const results = [];

  await page.goto(`${ctx.baseUrl}/Admin/Queries/Index`, { waitUntil: 'networkidle' });
  const queriesPage = page.locator('[data-testid="queries-page"]');

  const rendersOk = await queriesPage.waitFor({ timeout: 20000 }).then(() => true).catch(() => false);
  results.push({ name: 'renders-queries-page', pass: rendersOk, message: rendersOk ? 'ok' : 'queries-page test id not found' });
  if (!rendersOk) {
    return results;
  }

  await queriesPage.getByText('Queries', { exact: true }).first().waitFor({ timeout: 10000 }).catch(() => {});
  await queriesPage.locator('.rz-progressbar').waitFor({ state: 'hidden', timeout: 20000 }).catch(() => {});

  const editorAlreadyOpenCount = await queriesPage.getByText('The source and metadata are provider-neutral.', { exact: false }).count();
  results.push({
    name: 'editor-not-open-by-default',
    pass: editorAlreadyOpenCount === 0,
    message: editorAlreadyOpenCount ? 'query editor was unexpectedly open' : 'ok',
  });

  // Query catalog API contract.
  let catalog = { sources: [], queries: [] };
  let apiOk = false;
  try {
    const response = await page.request.get(`${ctx.baseUrl}/api/crest/queries`);
    if (response.ok()) {
      catalog = await response.json();
      apiOk = Array.isArray(catalog.sources);
    }
  } catch {
    apiOk = false;
  }
  results.push({
    name: 'query-catalog-api',
    pass: apiOk,
    message: apiOk ? `sources=${catalog.sources.length} queries=${catalog.queries?.length ?? 0}` : 'catalog fetch failed or malformed',
  });

  const removeQuery = async name => {
    const row = page.locator('tr').filter({ hasText: name });
    await row.locator('button').last().click();
    await page.getByText(`Delete query ${name}?`, { exact: false }).waitFor();
    // Scope the confirmation to the inline confirm alert: every query row also renders
    // its own exact-name "Delete" button, so a page-wide getByRole match is a
    // strict-mode violation (it resolves to one button per row plus this one) and the
    // click silently never happens - which looked like a broken delete flow rather than
    // a broken selector. The confirmation is a Radzen alert rendered in place, not a
    // modal dialog, so there is no [role="dialog"] to scope to.
    const confirm = page.locator('.rz-alert').filter({ hasText: `Delete query ${name}` }).last();
    await confirm.getByRole('button', { name: 'Delete', exact: true }).click();
    await row.waitFor({ state: 'detached', timeout: 20000 });
  };

  // Sweep leftover test queries from a previous failed run before asserting anything new
  // (same cleanup the original script did, unconditionally).
  for (const query of (catalog.queries || []).filter(q => q.name.startsWith('crest-playwright-'))) {
    await removeQuery(query.name).catch(() => {});
  }

  if (apiOk && catalog.sources.length > 0) {
    let created = false;
    let deleted = false;
    const temporaryName = `crest-playwright-${Date.now()}`;
    try {
      await queriesPage.locator('button').filter({ hasText: 'New query' }).click();
      const editor = page.locator('[data-testid="query-editor"]');
      await editor.waitFor();
      await editor.locator('input').first().fill(temporaryName);
      await editor.locator('textarea').fill('{"SqlQueryMetadata":{"Template":"select 1"}}');
      await editor.locator('button').filter({ hasText: 'Save' }).click();
      created = await page.getByText(temporaryName, { exact: true }).waitFor({ timeout: 20000 }).then(() => true).catch(() => false);
      if (created) {
        await removeQuery(temporaryName);
        deleted = true;
      }
    } catch {
      // created/deleted stay at whatever they last reached; reported below.
    }
    results.push({ name: 'create-and-delete-query', pass: created && deleted, message: `created=${created} deleted=${deleted}` });
  } else {
    // Original script only exercised the create/delete flow when sources existed; with none,
    // there's nothing to create a query against, so this is a no-op pass rather than a failure.
    results.push({ name: 'create-and-delete-query', pass: true, message: 'skipped: no query sources available' });
  }

  // The legacy raw-SQL console route must still resolve through the Crest legacy iframe, not
  // get intercepted by the native "All Queries" Blazor page.
  await page.goto(`${ctx.baseUrl}/Admin/Queries/Sql/Query`, { waitUntil: 'networkidle' });
  const legacyFrame = page.locator('iframe.legacy-admin-frame');
  const legacyFrameOk = await legacyFrame.waitFor({ timeout: 20000 }).then(() => true).catch(() => false);
  const legacySrc = legacyFrameOk ? await legacyFrame.getAttribute('src') : null;
  const nativeHijacked = await page.locator('[data-testid="queries-page"]').count();
  results.push({
    name: 'legacy-sql-route-uses-frame',
    pass: legacyFrameOk && Boolean(legacySrc?.includes('legacy-frame=1')) && nativeHijacked === 0,
    message: `frame=${legacyFrameOk} src=${legacySrc ?? 'none'} nativeHijacked=${nativeHijacked}`,
  });

  return results;
};
