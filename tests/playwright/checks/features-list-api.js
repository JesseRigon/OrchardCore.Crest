// Converted from the old admin-features-list.js
// (modules/OrchardCore.Crest/tests/playwright/admin-features-list.js).
//
// The original didn't assert much beyond an implicit throw-on-non-ok-response — it logged
// the sorted feature-id catalog for a human to eyeball. This keeps the same network
// contract (navigate to /Admin/Features, capture the GET /api/crest/features response) but
// turns those implicit assumptions into real pass/fail assertions instead of a console.log.
// Note this is a distinct concern from checks/features-page.js, which asserts the rendered
// UI grid — this one asserts the underlying API response directly.
module.exports = async function run(page, ctx) {
  const featuresResponsePromise = page.waitForResponse(
    response => response.url().includes('/api/crest/features') && response.request().method() === 'GET',
    { timeout: 30000 }
  );

  await page.goto(`${ctx.baseUrl}/Admin/Features`, { waitUntil: 'networkidle' });
  await page.locator('[data-testid="features-page"]').waitFor({ timeout: 30000 });

  const response = await featuresResponsePromise;
  const apiOk = response.ok();

  let ids = [];
  if (apiOk) {
    const features = await response.json();
    ids = features
      .map(feature => feature.id)
      .filter(Boolean)
      .sort((left, right) => left.localeCompare(right));
  }

  return [
    { name: 'features-api-response-ok', pass: apiOk, message: `status ${response.status()}` },
    { name: 'features-api-returns-ids', pass: ids.length > 0, message: `${ids.length} feature ids` },
  ];
};
