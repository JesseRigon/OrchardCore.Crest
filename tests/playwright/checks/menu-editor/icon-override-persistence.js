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

  let nodes = await getRootNodes();
  const content = nodes.find(node => node.text === 'Content');
  const design = nodes.find(node => node.text === 'Design');
  if (!content || !design) throw new Error('Expected Content and Design root menu nodes.');

  const original = { content: content.iconClass, design: design.iconClass };
  let afterTwoUpdates;
  let afterChangingContentAgain;

  try {
    await updateNodeIcon(content, '@iconify:mdi:home');
    nodes = await getRootNodes();
    await updateNodeIcon(nodes.find(node => node.text === 'Design'), '@iconify:mdi:wrench');

    nodes = await getRootNodes();
    afterTwoUpdates = {
      content: nodes.find(node => node.text === 'Content')?.iconClass,
      design: nodes.find(node => node.text === 'Design')?.iconClass,
    };

    await updateNodeIcon(nodes.find(node => node.text === 'Content'), '@iconify:mdi:account');

    nodes = await getRootNodes();
    afterChangingContentAgain = {
      content: nodes.find(node => node.text === 'Content')?.iconClass,
      design: nodes.find(node => node.text === 'Design')?.iconClass,
    };
  } finally {
    const restoreNodes = await getRootNodes();
    const restoreContent = restoreNodes.find(node => node.text === 'Content');
    if (restoreContent && original.content) {
      await updateNodeIcon(restoreContent, original.content).catch(() => {});
    }
    if (original.design) {
      const latestNodes = await getRootNodes();
      const restoreDesign = latestNodes.find(node => node.text === 'Design');
      if (restoreDesign) await updateNodeIcon(restoreDesign, original.design).catch(() => {});
    }
  }

  return [
    {
      name: 'both-overrides-persist-independently',
      pass: afterTwoUpdates.content === '@iconify:mdi:home' && afterTwoUpdates.design === '@iconify:mdi:wrench',
      message: JSON.stringify(afterTwoUpdates),
    },
    {
      name: 'later-save-updates-only-its-own-node',
      pass: afterChangingContentAgain.content === '@iconify:mdi:account' && afterChangingContentAgain.design === '@iconify:mdi:wrench',
      message: JSON.stringify(afterChangingContentAgain),
    },
  ];
};
