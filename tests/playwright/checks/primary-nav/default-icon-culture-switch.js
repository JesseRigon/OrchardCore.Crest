const { createInstance } = require('../../harness/instance');
const { loginAsAdmin } = require('../../harness/auth');

// Regression check for CrestIconSourceStore's legacy icon fallback: default icons for
// stock menu items with no stored override used to be resolved (as a last resort) by
// looking up the item's TRANSLATED caption text in LegacyNavigationIconMap, so switching
// the admin UI culture changed the lookup key and silently dropped the icon for every
// un-overridden item. The fix keys that map exclusively by the item's stable, bare-slug
// Id/CSS class, never by Text. This check reads the live admin menu (the same endpoint
// PrimaryNavMenu.razor consumes) under English, then again under French, with no override
// ever saved, and verifies the same items keep the same non-null icon in both cultures.
async function getAdminMenu(page) {
  return page.evaluate(async () => {
    const response = await fetch('/api/crest/navigation/admin', { credentials: 'include' });
    if (!response.ok) throw new Error(`admin menu failed: ${response.status}`);
    return response.json();
  });
}

function flatten(items, out = []) {
  for (const item of items || []) {
    out.push(item);
    flatten(item.items, out);
  }
  return out;
}

module.exports = async function run(page, ctx) {
  const results = [];

  const englishMenu = await getAdminMenu(page);
  const englishItems = flatten(englishMenu.items).filter(item => item.id);
  const englishIconById = new Map(englishItems.map(item => [item.id, item.icon?.name ?? null]));

  const withIcons = englishItems.filter(item => englishIconById.get(item.id));
  results.push({
    name: 'english-default-items-have-icons',
    pass: withIcons.length > 0,
    message: `${withIcons.length}/${englishItems.length} items with an Id resolved a default icon under English`,
  });

  const french = await createInstance({ contextOptions: { locale: 'fr-FR' } });
  try {
    await loginAsAdmin(french.page, ctx.baseUrl);
    const trigger = french.page.locator('.admin-titlebar__culture-selector');
    await trigger.click();
    await french.page.locator('[role="option"]', { hasText: 'français' }).first().click();
    await french.page.waitForTimeout(300);

    const frenchMenu = await getAdminMenu(french.page);
    const frenchItems = flatten(frenchMenu.items).filter(item => item.id);
    const frenchIconById = new Map(frenchItems.map(item => [item.id, item.icon?.name ?? null]));

    const mismatches = [];
    for (const [id, englishIcon] of englishIconById) {
      if (!englishIcon) continue;
      const frenchIcon = frenchIconById.get(id);
      if (frenchIcon !== englishIcon) {
        mismatches.push({ id, englishIcon, frenchIcon });
      }
    }

    results.push({
      name: 'default-icons-survive-culture-switch',
      pass: mismatches.length === 0,
      message: mismatches.length === 0
        ? `all ${withIcons.length} default icons matched between en-US and fr-FR`
        : `mismatches: ${JSON.stringify(mismatches)}`,
    });
  } finally {
    await french.browser.close();
  }

  return results;
};
