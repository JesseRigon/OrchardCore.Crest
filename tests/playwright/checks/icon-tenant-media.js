// Converted from OrchardCore.Crest.Icons/tests/playwright/tenant-media-icons-api.js.
// Verifies the tenant media icon lifecycle: enabling the TenantMedia feature, uploading an
// SVG icon, seeing it listed and searchable, then deleting it. Uses a timestamped icon name
// (as the original did) so repeated runs don't collide.
const featureId = 'OrchardCore.Crest.Icons.TenantMedia';
const svg =
  '<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 24 24" fill="currentColor" aria-hidden="true"><path d="M12 3 3 8l9 5 9-5-9-5Zm-7 8v5l7 5 7-5v-5l-7 4-7-4Z"/></svg>';

async function api(page, path, options = {}) {
  return page.evaluate(async ({ path, options }) => {
    const response = await fetch(path, { credentials: 'include', ...options });
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

async function uploadIcon(page, iconFileName) {
  return page.evaluate(async ({ iconFileName, svg }) => {
    const form = new FormData();
    form.append('file', new Blob([svg], { type: 'image/svg+xml' }), iconFileName);
    form.append('overwrite', 'true');
    const response = await fetch('/api/crest/icons/tenant', { method: 'POST', credentials: 'include', body: form });
    const text = await response.text();
    return { status: response.status, ok: response.ok, body: text ? JSON.parse(text) : null };
  }, { iconFileName, svg });
}

module.exports = async function run(page, ctx) {
  const iconFileName = `crest-tenant-icon-${Date.now()}.svg`;
  const iconName = iconFileName.replace(/\.svg$/i, '');
  const results = [];

  const enable = await api(page, `/api/crest/features/${encodeURIComponent(featureId)}/enable`, { method: 'POST' });
  results.push({ name: 'enable-tenant-media-feature', pass: enable.ok || enable.status === 204, message: `status=${enable.status}` });
  if (!results[0].pass) return results;

  await page.waitForTimeout(3000);

  const upload = await uploadIcon(page, iconFileName);
  results.push({
    name: 'upload-tenant-icon',
    pass: upload.ok && upload.body?.name === iconName,
    message: `status=${upload.status} name=${upload.body?.name}`,
  });
  if (!upload.ok) return results;

  const list = await api(page, '/api/crest/icons/tenant');
  const listed = Array.isArray(list.body) && list.body.some(icon => icon.name === iconName && icon.key === `tenant/current/default/${iconName}`);
  results.push({ name: 'tenant-icon-listed', pass: listed, message: `listed=${listed}` });

  const search = await api(page, `/api/crest/icons?library=tenant&query=${encodeURIComponent(iconName)}&skip=0&take=20`);
  const found =
    search.ok && Array.isArray(search.body?.items) && search.body.items.some(icon => icon.key === `tenant/current/default/${iconName}` && Boolean(icon.svgMarkup));
  results.push({ name: 'tenant-icon-searchable', pass: found, message: `found=${found}` });

  const remove = await api(page, `/api/crest/icons/tenant/${encodeURIComponent(iconName)}`, { method: 'DELETE' });
  results.push({ name: 'delete-tenant-icon', pass: remove.ok, message: `status=${remove.status}` });

  return results;
};
