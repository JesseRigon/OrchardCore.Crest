// Converted from OrchardCore.Crest.Admin/tests/playwright/model-list-actions.js.
// End-to-end CRUD flow against the Crest model-list UI for Customers: edit inline
// (without navigating away), save a draft, duplicate from the editor, duplicate from the
// list row, then delete from the list row. Creates and always cleans up its own seed data.
//
// Note: the original defaulted to /Admin/CRM/Customers, which predates this repo's
// CRM -> Accounting rename; updated to /Admin/Accounting/Customers.
module.exports = async function run(page, ctx) {
  const route = '/Admin/Accounting/Customers';

  async function getAntiforgeryToken() {
    return page.evaluate(async () => {
      const response = await fetch('/api/crest/antiforgery/token', { credentials: 'include' });
      if (!response.ok) throw new Error(`antiforgery failed: ${response.status}`);
      return response.json();
    });
  }

  async function api(method, url, body) {
    const token = await getAntiforgeryToken();
    return page.evaluate(
      async ({ method, url, body, token }) => {
        const response = await fetch(url, {
          method,
          credentials: 'include',
          headers: {
            'Content-Type': 'application/json',
            [token.headerName || 'RequestVerificationToken']: token.requestToken,
          },
          body: body === null || body === undefined ? undefined : JSON.stringify(body),
        });
        const text = await response.text();
        let json = null;
        try {
          json = text ? JSON.parse(text) : null;
        } catch {}
        return { ok: response.ok, status: response.status, text, json };
      },
      { method, url, body, token },
    );
  }

  async function listCustomers() {
    return page.evaluate(async () => {
      const response = await fetch('/api/crest/model/content-items?contentType=Customer&name=Customers&latest=true&limit=250', {
        credentials: 'include',
      });
      if (!response.ok) throw new Error(`list customers failed: ${response.status}`);
      return response.json();
    });
  }

  async function deleteContentItem(contentItemId) {
    if (!contentItemId) return;
    await api('DELETE', `/api/crest/model/content-items/${encodeURIComponent(contentItemId)}`, null);
  }

  async function cleanupPlaywrightCustomers() {
    const state = await listCustomers().catch(() => null);
    for (const item of state?.items || []) {
      const contentItem = item.contentItem;
      if (contentItem?.contentItemId && contentItem.displayText?.includes('Playwright customer')) {
        await deleteContentItem(contentItem.contentItemId).catch(() => {});
      }
    }
  }

  async function createCustomer(displayText) {
    const result = await api('POST', '/api/crest/model/content-items', { contentType: 'Customer', displayText, publish: false });
    if (!result.ok) throw new Error(`create failed: ${result.status} ${result.text}`);
    return result.json;
  }

  async function rowFor(contentItemId) {
    const row = page.locator('.crest-model-list__item', { hasText: contentItemId }).first();
    await row.waitFor({ timeout: 15000 });
    await row.scrollIntoViewIfNeeded();
    await row.hover();
    return row;
  }

  await cleanupPlaywrightCustomers();

  const seedName = `Playwright customer ${Date.now()}`;
  let seedId;
  const results = [];

  try {
    const seed = await createCustomer(seedName);
    seedId = seed?.contentItem?.contentItemId;
    if (!seedId) throw new Error('create response did not include contentItem.contentItemId');

    await page.goto(`${ctx.baseUrl}${route}`, { waitUntil: 'networkidle' });
    await page.locator('.crest-model-list').waitFor({ timeout: 20000 });
    await page.waitForTimeout(1000);

    const initialUrl = page.url();
    const row = await rowFor(seedId);
    await row.locator('button[title="Edit"]').click();
    await page.locator('.crest-content-item-editor').waitFor({ timeout: 15000 });
    await page.locator('.crest-form-manager').waitFor({ timeout: 15000 });
    const editorVisible = await page.locator('.crest-content-item-editor').isVisible();
    const formManagerVisible = await page.locator('.crest-form-manager').isVisible();
    const stayedInPageFlow = page.url() === initialUrl;

    results.push({
      name: 'edit-opens-inline-without-navigating',
      pass: editorVisible && formManagerVisible && stayedInPageFlow,
      message: `editorVisible=${editorVisible} formManagerVisible=${formManagerVisible} stayedInPageFlow=${stayedInPageFlow}`,
    });

    await page.locator('.crest-content-item-editor input').first().fill(`${seedName} updated`);
    await page.locator('.crest-content-item-editor button:has-text("Save draft")').click();
    await page.waitForTimeout(1500);
    const updatedVisible = await page.locator('.crest-model-list__item', { hasText: `${seedName} updated` }).count();
    results.push({ name: 'save-draft-updates-list-row', pass: updatedVisible > 0, message: `updatedRowCount=${updatedVisible}` });

    const rowCountBeforeEditorDuplicate = await page.locator('.crest-model-list__item').count();
    await page.locator('.crest-content-item-editor button:has-text("Duplicate")').first().click();
    await page.waitForTimeout(1500);
    const rowCountAfterEditorDuplicate = await page.locator('.crest-model-list__item').count();
    results.push({
      name: 'editor-duplicate-adds-a-row',
      pass: rowCountAfterEditorDuplicate > rowCountBeforeEditorDuplicate,
      message: `before=${rowCountBeforeEditorDuplicate} after=${rowCountAfterEditorDuplicate}`,
    });

    const listRow = await rowFor(seedId);
    const rowCountBeforeListDuplicate = await page.locator('.crest-model-list__item').count();
    await listRow.locator('button[title="Duplicate"]').click();
    await page.waitForTimeout(1500);
    const rowCountAfterListDuplicate = await page.locator('.crest-model-list__item').count();
    results.push({
      name: 'list-row-duplicate-adds-a-row',
      pass: rowCountAfterListDuplicate > rowCountBeforeListDuplicate,
      message: `before=${rowCountBeforeListDuplicate} after=${rowCountAfterListDuplicate}`,
    });

    const deleteRow = await rowFor(seedId);
    await deleteRow.locator('button[title="Delete"]').click();
    await page.waitForTimeout(1500);
    const deletedStillVisible = await page.locator('.crest-model-list__item', { hasText: seedId }).count();
    results.push({ name: 'list-row-delete-removes-row', pass: deletedStillVisible === 0, message: `remainingCount=${deletedStillVisible}` });

    const alerts = await page.locator('.rz-alert-danger').count();
    results.push({ name: 'no-danger-alerts', pass: alerts === 0, message: `alertCount=${alerts}` });
  } finally {
    await cleanupPlaywrightCustomers().catch(() => {});
  }

  return results;
};
