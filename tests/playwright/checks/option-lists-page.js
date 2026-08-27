// The Option Lists management screen: the tenant-facing half of the enum system.
// Verifies the page renders its lists, that a module-seeded option can be relabelled
// and hidden from the UI, and - the load-bearing guarantee - that relabelling never
// changes the key code matches on.
module.exports = async function run(page, ctx) {
  const results = [];

  const tokenResponse = await page.request.get(`${ctx.baseUrl}/api/crest/antiforgery/token`);
  const token = await tokenResponse.json();
  const headers = { [token.headerName || 'RequestVerificationToken']: token.requestToken };

  // Seed a list through the API so the page has something deterministic to show and
  // this check never depends on another module's seeds.
  // The title must be unique per run AND used verbatim when locating the row: repeat
  // runs leave earlier probe lists behind (there is no delete endpoint yet), so a
  // prefix match would open a PREVIOUS run's list and every later assertion would
  // read the wrong option.
  const stamp = Date.now();
  const listKey = `probe.page.${stamp}`;
  const listTitle = `Probe page list ${stamp}`;
  const created = await page.request.post(`${ctx.baseUrl}/api/crest/option-lists`, {
    headers,
    data: { key: listKey, displayText: listTitle },
  });
  results.push({ name: 'seed-list-created', pass: created.ok(), message: `HTTP ${created.status()}` });

  await page.request.post(`${ctx.baseUrl}/api/crest/option-lists/${listKey}/options`, {
    headers,
    data: { key: 'Alpha', displayText: 'Alpha', position: 0 },
  });

  try {
    await page.goto(`${ctx.baseUrl}/Admin/OptionLists`, { waitUntil: 'networkidle' });
    await page.locator('[data-testid="option-lists-page"]').waitFor({ timeout: 20000 });

    const indexVisible = await page.locator('[data-testid="option-list-index"]').isVisible();
    results.push({ name: 'page-renders', pass: indexVisible, message: `index visible: ${indexVisible}` });

    // Open the seeded list.
    await page.locator('[data-testid="option-list-index"] button', { hasText: listTitle }).first().click();
    await page.locator('[data-testid="option-list-detail"]').waitFor({ timeout: 15000 });
    await page.waitForTimeout(500);

    const gridText = await page.locator('[data-testid="option-grid"]').innerText();
    results.push({
      name: 'shows-option-key-alongside-label',
      pass: gridText.includes('Alpha'),
      message: gridText.replace(/\s+/g, ' ').slice(0, 160),
    });

    // Relabel through the UI, then assert via the API that the KEY is untouched.
    // Scope to the BODY row: the grid renders its own filter input in the header, so
    // an unscoped .first() types into the filter instead of the option's label.
    const labelBox = page.locator('[data-testid="option-grid"] tbody tr input').first();
    await labelBox.waitFor({ timeout: 15000 });
    await labelBox.click();
    await labelBox.fill('Renamed alpha');
    // Tab, not blur(): the text box commits on a real focus change, and blur() alone
    // does not always produce one.
    await page.keyboard.press('Tab');
    await page.waitForTimeout(2500);

    const afterRelabel = await (await page.request.get(`${ctx.baseUrl}/api/crest/option-lists/${listKey}`)).json();
    const option = (afterRelabel.options || []).find(entry => entry.key === 'Alpha');
    results.push({
      name: 'relabel-preserves-the-key',
      pass: !!option && option.displayText === 'Renamed alpha',
      message: option ? `key=${option.key} label=${option.displayText}` : 'option missing after relabel',
    });

    // Hide it, and confirm the selectable projection drops it while the full list
    // keeps it - history must still resolve a hidden option.
    await page.locator('[data-testid="option-grid"] tbody tr .rz-switch').first().click();
    await page.waitForTimeout(2000);

    const full = await (await page.request.get(`${ctx.baseUrl}/api/crest/option-lists/${listKey}`)).json();
    const selectable = await (await page.request.get(`${ctx.baseUrl}/api/crest/option-lists/${listKey}/selectable`)).json();
    results.push({
      name: 'hiding-drops-it-from-selectable-but-keeps-it-resolvable',
      pass: (full.options || []).some(entry => entry.key === 'Alpha')
        && !(selectable || []).some(entry => entry.key === 'Alpha'),
      message: `full=${(full.options || []).length} selectable=${(selectable || []).length}`,
    });

    const alerts = await page.locator('.rz-alert-danger').count();
    results.push({ name: 'no-danger-alerts', pass: alerts === 0, message: `alertCount=${alerts}` });
  } finally {
    // Option lists have no delete endpoint yet; hide the probe list's options so
    // repeat runs stay clean without leaving selectable noise behind.
    await page.request.put(`${ctx.baseUrl}/api/crest/option-lists/${listKey}/options/Alpha`, {
      headers,
      data: { displayText: null, position: null, hidden: true },
    }).catch(() => {});
  }

  return results;
};
