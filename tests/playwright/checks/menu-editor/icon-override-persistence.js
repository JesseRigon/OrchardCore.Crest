// Converted from OrchardCore.Crest.Admin/tests/playwright/admin-menu-icon-override-persistence.js.
// Two separate icon-override saves on different root nodes must each persist
// independently — a later save on one node must not clobber an earlier save on another.
const { fetchAntiforgeryToken } = require('../../harness/antiforgery');

module.exports = async function run(page, ctx) {
  const defaultMenuId = '__crest_default_admin_menu';

  // Mutating Crest APIs are antiforgery-protected - see harness/antiforgery.js.
  const antiforgery = await fetchAntiforgeryToken(page, ctx.baseUrl);
  const antiforgeryHeaders = { [antiforgery.headerName]: antiforgery.requestToken };

  async function getRootNodes() {
    const data = await page.evaluate(async () => {
      const response = await fetch('/api/crest/admin-menus', { credentials: 'include' });
      if (!response.ok) throw new Error(`admin menus failed: ${response.status}`);
      return response.json();
    });
    return data.menus.find(menu => menu.id === defaultMenuId).nodes;
  }

  async function updateNodeIcon(node, iconClass) {
    const payload = {
      type: node.type,
      text: node.text,
      url: node.url,
      iconClass,
      enabled: node.enabled,
      priority: node.priority,
      displayPosition: node.displayPosition,
      permissionNames: node.permissionNames,
      parentNodeId: null,
      position: null,
    };

    const result = await page.evaluate(
      async ({ id, payload, antiforgeryHeaders }) => {
        const response = await fetch(`/api/crest/admin-menus/__crest_default_admin_menu/nodes/${encodeURIComponent(id)}`, {
          method: 'PUT',
          credentials: 'include',
          headers: { 'content-type': 'application/json', ...antiforgeryHeaders },
          body: JSON.stringify(payload),
        });
        return { ok: response.ok, status: response.status, text: await response.text() };
      },
      { id: node.id, payload, antiforgeryHeaders },
    );

    if (!result.ok) throw new Error(`Updating ${node.text} failed: ${result.status} ${result.text}`);
  }

  // Any two non-locked, non-custom root nodes serve: the contract under test is
  // that two overrides persist independently, not anything about which nodes carry
  // them. Literal Content/Design roots only exist on the stock arrangement - a
  // tenant's imported layout overlay can move or rename anything.
  let nodes = await getRootNodes();
  const candidates = nodes.filter(node => node.id !== 'new' && !String(node.id).startsWith('custom-'));
  if (candidates.length < 2) throw new Error(`Need two non-locked root menu nodes, found ${candidates.length}.`);
  const firstId = candidates[0].id;
  const secondId = candidates[candidates.length - 1].id;
  const byId = (list, id) => list.find(node => node.id === id);

  const original = { first: byId(nodes, firstId).iconClass, second: byId(nodes, secondId).iconClass };
  let afterTwoUpdates;
  let afterChangingFirstAgain;

  try {
    await updateNodeIcon(byId(nodes, firstId), '@iconify:mdi:home');
    nodes = await getRootNodes();
    await updateNodeIcon(byId(nodes, secondId), '@iconify:mdi:wrench');

    nodes = await getRootNodes();
    afterTwoUpdates = {
      first: byId(nodes, firstId)?.iconClass,
      second: byId(nodes, secondId)?.iconClass,
    };

    await updateNodeIcon(byId(nodes, firstId), '@iconify:mdi:account');

    nodes = await getRootNodes();
    afterChangingFirstAgain = {
      first: byId(nodes, firstId)?.iconClass,
      second: byId(nodes, secondId)?.iconClass,
    };
  } finally {
    // Always restore - an empty iconClass clears the override back to the default.
    const restoreNodes = await getRootNodes();
    const restoreFirst = byId(restoreNodes, firstId);
    if (restoreFirst) await updateNodeIcon(restoreFirst, original.first ?? '').catch(() => {});
    const latestNodes = await getRootNodes();
    const restoreSecond = byId(latestNodes, secondId);
    if (restoreSecond) await updateNodeIcon(restoreSecond, original.second ?? '').catch(() => {});
  }

  return [
    {
      name: 'both-overrides-persist-independently',
      pass: afterTwoUpdates.first === '@iconify:mdi:home' && afterTwoUpdates.second === '@iconify:mdi:wrench',
      message: JSON.stringify(afterTwoUpdates),
    },
    {
      name: 'later-save-updates-only-its-own-node',
      pass: afterChangingFirstAgain.first === '@iconify:mdi:account' && afterChangingFirstAgain.second === '@iconify:mdi:wrench',
      message: JSON.stringify(afterChangingFirstAgain),
    },
  ];
};
