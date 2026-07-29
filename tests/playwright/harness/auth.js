// Log into /Admin once per suite run. Every existing script hand-rolled this same
// function — it now lives in exactly one place.
async function loginAsAdmin(page, baseUrl, creds = {}) {
  const username = creds.username || process.env.ADMIN_USER || 'admin';
  const password = creds.password || process.env.ADMIN_PASSWORD || 'FruitfulRules1!';

  await page.goto(`${baseUrl}/login`, { waitUntil: 'networkidle' });
  if (await page.locator('#UserName').count()) {
    await page.fill('#UserName', username);
    await page.fill('#Password', password);
    await page.locator('button:has-text("Login")').click();
    await page.waitForURL(/\/admin/i, { timeout: 15000 }).catch(() => {});
    await page.waitForLoadState('networkidle').catch(() => {});
  }
  await page.goto(`${baseUrl}/Admin`, { waitUntil: 'networkidle' });
}

// Placeholder for the public/front-end site's auth flow. No client-site feature checks
// exist yet — this exists so run-client-suite.js has a real login step to call once the
// first front-end check is written, instead of inventing the shape then. Same Orchard
// /login mechanism as admin, just no forced redirect into /Admin afterward.
async function loginAsClient(page, baseUrl, creds = {}) {
  const username = creds.username || process.env.CLIENT_USER;
  const password = creds.password || process.env.CLIENT_PASSWORD;

  await page.goto(`${baseUrl}/`, { waitUntil: 'networkidle' });

  if (username && password && (await page.locator('#UserName').count())) {
    await page.fill('#UserName', username);
    await page.fill('#Password', password);
    await page.locator('button:has-text("Login")').click();
    await page.waitForLoadState('networkidle').catch(() => {});
  }
}

module.exports = { loginAsAdmin, loginAsClient };
