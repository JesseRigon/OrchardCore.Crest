// Converted from OrchardCore.Crest.Admin/tests/playwright/admin-menu-icon-override.js.
// Setting a custom iconClass on an admin menu node via the API should be reflected in the
// rendered primary-nav icon. Restores the original iconClass in `finally` either way.
//
// Note: the original script pointed at /Admin/CRM/Customers, which predates this repo's
// CRM -> Accounting module rename; updated to /Admin/Accounting/Customers.
const { fetchAntiforgeryToken } = require('../../harness/antiforgery');

module.exports = async function run(page, ctx) {
  const menuId = '__crest_default_admin_menu';
  const targetNodeId = process.env.NODE_ID || 'content';
  const testIconClass = process.env.ICON_CLASS || '@iconify:mdi:folder';

  function flatten(nodes) {
    return nodes.flatMap(node => [node, ...flatten(node.items || [])]);
  }

  async function api(path, options = {}) {
    const response = await page.evaluate(
      async ({ path, options }) => {
        const res = await fetch(path, {
          credentials: 'include',
          ...options,
          // Merged AFTER the options spread, or options.headers (the antiforgery token
          // alone) would replace this object entirely and drop the content-type → 415.
          headers: { 'content-type': 'application/json', ...(options.headers || {}) },
        });
        return { ok: res.ok, status: res.status, text: await res.text() };
      },
      { path, options },
    );
    if (!response.ok) throw new Error(`${path} failed ${response.status}: ${response.text}`);
    return response.text ? JSON.parse(response.text) : null;
  }

  // Mutating Crest APIs are antiforgery-protected - see harness/antiforgery.js.
  const antiforgery = await fetchAntiforgeryToken(page, ctx.baseUrl);
  const antiforgeryHeaders = { [antiforgery.headerName]: antiforgery.requestToken };

  const state = await api('/api/crest/admin-menus');
  const menu = state.menus.find(m => m.id === menuId);
  if (!menu) throw new Error(`Menu ${menuId} not found`);
  const node = flatten(menu.nodes).find(n => n.id === targetNodeId);
  if (!node) throw new Error(`Node ${targetNodeId} not found`);
  const originalIconClass = node.iconClass || null;

  const model = {
    type: node.type,
    text: node.text,
    url: node.url,
    iconClass: testIconClass,
    enabled: node.enabled,
    priority: node.priority,
    displayPosition: node.displayPosition,
    permissionNames: node.permissionNames || [],
    parentNodeId: node.parentId || '',
    position: node.order,
  };

  let updatedIconClass;
  let contentIcon;
  try {
    const updated = await api(`/api/crest/admin-menus/${encodeURIComponent(menuId)}/nodes/${encodeURIComponent(targetNodeId)}`, {
      method: 'PUT',
      headers: antiforgeryHeaders,
      body: JSON.stringify(model),
    });
    updatedIconClass = flatten(updated.nodes).find(n => n.id === targetNodeId)?.iconClass;

    await page.goto(`${ctx.baseUrl}/Admin/Accounting/Customers`, { waitUntil: 'networkidle' });
    await page.locator('.primary-nav-menu').waitFor({ timeout: 15000 });
    contentIcon = await page
      .locator('.crest-panel-menu__item-content', { hasText: /^Content$/ })
      .first()
      .locator('.orchard-icon')
      .evaluate(icon => ({
        library: icon.getAttribute('data-icon-library'),
        name: icon.getAttribute('data-icon-name'),
        hasSvg: !!icon.querySelector('svg'),
      }));
  } finally {
    const restore = { ...model, iconClass: originalIconClass };
    await api(`/api/crest/admin-menus/${encodeURIComponent(menuId)}/nodes/${encodeURIComponent(targetNodeId)}`, {
      method: 'PUT',
      headers: antiforgeryHeaders,
      body: JSON.stringify(restore),
    }).catch(() => {});
  }

  return [
    { name: 'api-reflects-icon-override', pass: updatedIconClass === testIconClass, message: `iconClass=${updatedIconClass}` },
    {
      name: 'rendered-icon-uses-override',
      pass: Boolean(contentIcon?.hasSvg),
      message: JSON.stringify(contentIcon),
    },
  ];
};
