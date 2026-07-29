// Converted from the old admin-native-authorization-pages.js. For a set of admin pages
// gated behind distinct permissions (Content Types, Roles, General Settings, Features),
// verifies the logged-in admin's session renders each one natively (heading present) with
// no fallback to the legacy iframe embed.
module.exports = async function run(page, ctx) {
  const pages = [
    ['/Admin/ContentTypes/List', 'Content Types'],
    ['/Admin/Roles/Index', 'Roles'],
    ['/Admin/Settings/general', 'General Settings'],
    ['/Admin/Features', 'Features'],
  ];

  const results = [];
  for (const [route, heading] of pages) {
    await page.goto(`${ctx.baseUrl}${route}`, { waitUntil: 'networkidle' });
    const hasHeading = await page.locator('h4').filter({ hasText: heading }).first().waitFor({ timeout: 20000 })
      .then(() => true).catch(() => false);
    const iframeCount = await page.locator('iframe').count();

    results.push({
      name: `renders-natively:${route}`,
      pass: hasHeading && iframeCount === 0,
      message: `heading=${hasHeading} iframeCount=${iframeCount}`,
    });
  }

  return results;
};
