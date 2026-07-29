const { severeConsoleErrors, drainConsoleErrors } = require('../harness/instance');

// Converted from the old admin-content-item-editor-page.js. Depends on at least one
// real (seeded) content item existing — like the original, it fetches the first item
// from the API rather than assuming a specific content-item ID, since seeded IDs aren't
// guaranteed stable across environments.
module.exports = async function run(page, ctx) {
  const listResponse = await page.request.get(`${ctx.baseUrl}/api/crest/content-items?pageSize=1`);
  if (!listResponse.ok()) {
    return [{ name: 'has-real-content-item', pass: false, message: `list request failed: ${listResponse.status()}` }];
  }
  const list = await listResponse.json();
  const item = list.items?.[0];
  if (!item) {
    return [{ name: 'has-real-content-item', pass: false, message: 'no content items available to validate editor against' }];
  }

  const results = [{ name: 'has-real-content-item', pass: true, message: `contentItemId=${item.contentItemId}` }];

  await page.goto(`${ctx.baseUrl}/Admin/Contents/ContentItems/${encodeURIComponent(item.contentItemId)}/Edit`, { waitUntil: 'networkidle' });
  const editorShown = await page.locator('[data-testid="content-item-editor"]').waitFor({ timeout: 20000 }).then(() => true).catch(() => false);
  const labelShown = await page.getByText('Content document', { exact: true }).waitFor({ timeout: 20000 }).then(() => true).catch(() => false);
  results.push({ name: 'renders-editor', pass: editorShown && labelShown, message: `editor=${editorShown} content-document-label=${labelShown}` });

  let jsonValid = false;
  let jsonLength = 0;
  try {
    const json = await page.locator('textarea').inputValue();
    JSON.parse(json);
    jsonValid = true;
    jsonLength = json.length;
  } catch {
    jsonValid = false;
  }
  results.push({ name: 'editor-json-valid', pass: jsonValid, message: `length=${jsonLength}` });

  // Round-trips a throwaway content item through the native API to prove the editor
  // route sits on top of real Orchard content APIs, then cleans it up immediately.
  const created = await page.request.post(`${ctx.baseUrl}/api/crest/content-items`, {
    data: { contentType: item.contentType, displayText: 'Crest editor probe', content: {}, publish: false },
  });
  let createdOk = created.ok();
  let probeId;
  if (createdOk) {
    const probe = await created.json();
    probeId = probe.contentItemId;
    const deleted = await page.request.delete(`${ctx.baseUrl}/api/crest/content-items/${encodeURIComponent(probeId)}`);
    createdOk = deleted.ok();
  }
  results.push({
    name: 'native-create-and-delete',
    pass: createdOk,
    message: probeId ? `probe=${probeId}` : `create failed: ${created.status()}`,
  });

  const legacyFrame = await page.locator('iframe').count();
  results.push({ name: 'no-legacy-iframe', pass: legacyFrame === 0, message: `iframe count=${legacyFrame}` });

  const errors = severeConsoleErrors(drainConsoleErrors(ctx.consoleErrors));
  results.push({ name: 'no-console-errors', pass: errors.length === 0, message: errors.join(' | ') || 'clean' });

  return results;
};
