// The Crest translations editor replaces the stock DataLocalization page: same per-culture,
// grouped editing of the tenant translation store, but reads include orphaned entries and the
// save merges (posted rows are the displayed set; blank deletes; everything else is preserved)
// instead of replacing the culture wholesale.
//
// UI assertions stay light (the page renders as a Crest Blazor page, not the legacy frame);
// the data contract is exercised through the API the page itself uses.
const { fetchAntiforgeryToken } = require('../harness/antiforgery');

module.exports = async function run(page, ctx) {
  const results = [];
  const probeKey = 'Ghost Probe Type';
  const probeValue = 'Tipo fantasma de prueba';

  // 1. The stock URL now renders the Crest page, not the legacy frame.
  await page.goto(`${ctx.baseUrl}/Admin/DataLocalization`, { waitUntil: 'networkidle' });
  const crestPage = await page.locator('[data-testid="translations-page"]').count();
  const legacyFrame = await page.locator('iframe.legacy-admin-frame').count();
  results.push({
    name: 'crest-page-shadows-the-stock-url',
    pass: crestPage > 0 && legacyFrame === 0,
    message: `crestPage=${crestPage} legacyFrame=${legacyFrame}`,
  });

  async function api(method, body) {
    const antiforgery = await fetchAntiforgeryToken(page, ctx.baseUrl);
    return page.evaluate(async ({ method, body, antiforgery }) => {
      const response = await fetch('/api/crest/translations' + (method === 'GET' ? '?culture=es-ES' : ''), {
        method,
        credentials: 'include',
        headers: { 'content-type': 'application/json', [antiforgery.headerName]: antiforgery.requestToken },
        body: body ? JSON.stringify(body) : undefined,
      });
      if (!response.ok) return { ok: false, status: response.status };
      return { ok: true, status: response.status, data: await response.json() };
    }, { method, body, antiforgery });
  }

  const valuedCount = data => data.groups.reduce((n, g) => n + g.strings.filter(s => s.value).length, 0);
  const findRow = (data, key) => data.groups.flatMap(g => g.strings).find(s => s.key === key);

  const before = await api('GET');
  if (!before.ok) return [...results, { name: 'translations-api-get', pass: false, message: `status=${before.status}` }];
  const baseline = valuedCount(before.data);

  try {
    // 2. Merge-save: a save whose displayed set is ONE row must not touch anything else.
    const put1 = await api('PUT', {
      culture: 'es-ES',
      translations: [{ context: 'Content Types', key: probeKey, value: probeValue }],
    });
    const afterAdd = put1.ok ? put1.data : null;
    results.push({
      name: 'single-row-save-preserves-everything-else',
      pass: !!afterAdd && valuedCount(afterAdd) === baseline + 1 && findRow(afterAdd, probeKey)?.value === probeValue,
      message: afterAdd
        ? `valued ${baseline} -> ${valuedCount(afterAdd)}; probe="${findRow(afterAdd, probeKey)?.value}"`
        : `status=${put1.status}`,
    });

    // 3. The probe has no live descriptor (no such content type), so it must surface as an
    // orphan - visible and editable rather than silently invisible.
    results.push({
      name: 'orphan-is-visible-and-flagged',
      pass: !!afterAdd && findRow(afterAdd, probeKey)?.orphan === true,
      message: `orphan=${afterAdd ? findRow(afterAdd, probeKey)?.orphan : 'n/a'}`,
    });

    // 4. Blanking a displayed row deletes exactly that entry.
    const put2 = await api('PUT', {
      culture: 'es-ES',
      translations: [{ context: 'Content Types', key: probeKey, value: '' }],
    });
    const afterDelete = put2.ok ? put2.data : null;
    results.push({
      name: 'blanked-row-deletes-only-itself',
      pass: !!afterDelete && valuedCount(afterDelete) === baseline && !findRow(afterDelete, probeKey)?.value,
      message: afterDelete ? `valued back to ${valuedCount(afterDelete)} (baseline ${baseline})` : `status=${put2.status}`,
    });
  } finally {
    // Defensive: never strand the probe if an assertion threw mid-way.
    await api('PUT', {
      culture: 'es-ES',
      translations: [{ context: 'Content Types', key: probeKey, value: '' }],
    }).catch(() => {});
  }

  return results;
};
