// Converted from OrchardCore.Crest.Icons/tests/playwright/iconify-provider-icons-page.js.
// Verifies the /Admin/Design/Icons page renders the Iconify provider search preview, hides
// the removed manual cache-update button, and that picking a remote Iconify icon updates the
// field end-to-end.
//
// ADAPTATION: 'remote-search-returns-mdi-home' and 'selecting-remote-icon-updates-field'
// require genuine outbound network access from the app server to https://api.iconify.design
// (same as the original script). They are kept as real assertions rather than turned into
// always-pass checks; if outbound network access isn't available where this suite runs,
// expect those two to fail for environmental reasons rather than a real regression.
async function api(page, path, options = {}) {
  return page.evaluate(async ({ path, options }) => {
    const response = await fetch(path, {
      credentials: 'include',
      headers: { 'content-type': 'application/json', ...(options.headers || {}) },
      ...options,
    });
    const text = await response.text();
    let body = null;
    try {
      body = text ? JSON.parse(text) : null;
    } catch {
      body = text;
    }
    return { status: response.status, ok: response.ok, body };
  }, { path, options });
}

module.exports = async function run(page, ctx) {
  const original = await api(page, '/api/crest/icons/providers');
  if (!original.ok || !original.body?.iconify) {
    return { name: 'read-provider-settings', pass: false, message: `status=${original.status}` };
  }

  const results = [];
  try {
    const configured = { iconify: { enabled: true, baseUrl: 'https://api.iconify.design', apiKey: null, apiKeyHeader: null, prefixes: ['mdi'] } };
    const save = await api(page, '/api/crest/icons/providers', { method: 'PUT', body: JSON.stringify(configured) });
    results.push({
      name: 'save-iconify-provider-settings',
      pass: save.ok && save.body?.iconify?.baseUrl === configured.iconify.baseUrl && save.body?.iconify?.prefixes?.[0] === 'mdi',
      message: `status=${save.status} baseUrl=${save.body?.iconify?.baseUrl}`,
    });

    await page.goto(`${ctx.baseUrl}/Admin/Design/Icons`, { waitUntil: 'networkidle' });
    const rendersOk = await Promise.all([
      page.locator('h4', { hasText: 'Icons' }).waitFor({ timeout: 20000 }).then(() => true).catch(() => false),
      page.getByText('Iconify', { exact: true }).waitFor({ timeout: 10000 }).then(() => true).catch(() => false),
      page.getByText('Provider search preview').waitFor({ timeout: 10000 }).then(() => true).catch(() => false),
    ]);
    results.push({
      name: 'renders-icons-admin-page',
      pass: rendersOk.every(Boolean),
      message: `heading=${rendersOk[0]} iconifyLabel=${rendersOk[1]} previewLabel=${rendersOk[2]}`,
    });

    const manualUpdateButtons = await page.getByRole('button', { name: /update public iconify cache|sync public iconify/i }).count();
    results.push({ name: 'no-manual-cache-update-button', pass: manualUpdateButtons === 0, message: `count=${manualUpdateButtons}` });

    const remoteSearch = await api(page, '/api/crest/icons?library=iconify&query=home&skip=0&take=20');
    const iconifyItem = remoteSearch.body?.items?.find(
      item => item.providerId === 'iconify' && item.key === 'iconify.mdi/current/default/home' && item.svgMarkup?.includes('<svg'),
    );
    results.push({
      name: 'remote-search-returns-mdi-home',
      pass: remoteSearch.ok && Boolean(iconifyItem),
      message: `status=${remoteSearch.status} total=${remoteSearch.body?.total} found=${Boolean(iconifyItem)}`,
    });

    let pickedIcon = false;
    const chooseIconTrigger = page.getByTitle('Choose icon');
    if (await chooseIconTrigger.count()) {
      await chooseIconTrigger.click();
      const dialog = page.locator('.icon-selector__dialog');
      await dialog.waitFor({ timeout: 15000 });
      await dialog.locator('.icon-selector__filters').waitFor({ timeout: 10000 });
      await dialog.getByRole('button', { name: 'Iconify' }).click();
      const searchBox = dialog.getByPlaceholder('Search all icons...');
      await searchBox.click();
      await searchBox.pressSequentially('home');
      const remoteHomeItem = page.locator('.icon-selector__item[title="@iconify:mdi:home"]');
      await remoteHomeItem.waitFor({ timeout: 30000 }).catch(() => {});
      if (await remoteHomeItem.count()) {
        await remoteHomeItem.click();
        pickedIcon = await page
          .getByText('Selected icon: @iconify:mdi:home')
          .waitFor({ timeout: 10000 })
          .then(() => true)
          .catch(() => false);
      }
    }
    results.push({ name: 'selecting-remote-icon-updates-field', pass: pickedIcon, message: `picked=${pickedIcon}` });
  } finally {
    await api(page, '/api/crest/icons/providers', { method: 'PUT', body: JSON.stringify(original.body) }).catch(() => {});
  }

  return results;
};
