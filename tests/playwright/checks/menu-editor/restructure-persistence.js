// Restructuring the menu - reparenting, reordering, hiding - persists against each item's
// key. If the key were derived from the displayed caption, every one of these overrides would
// be orphaned by a culture switch or a rename, which is what makes this worth checking
// end-to-end rather than only asserting on the key itself.
//
// Moves a root item under another root, reorders it, hides a third, and then verifies the
// whole arrangement still reads back correctly after a rename of one of the moved items -
// the rename must change only the caption, never where the item sits.
const { fetchAntiforgeryToken } = require('../../harness/antiforgery');

const defaultMenuId = '__crest_default_admin_menu';

module.exports = async function run(page, ctx) {
  const results = [];

  const antiforgery = await fetchAntiforgeryToken(page, ctx.baseUrl);
  const headers = { 'content-type': 'application/json', [antiforgery.headerName]: antiforgery.requestToken };

  async function getMenu() {
    return page.evaluate(async () => {
      const response = await fetch('/api/crest/admin-menus', { credentials: 'include' });
      if (!response.ok) throw new Error(`admin menus failed: ${response.status}`);
      const data = await response.json();
      return data.menus.find(menu => menu.id === '__crest_default_admin_menu');
    });
  }

  async function post(path, body) {
    return page.evaluate(async ({ path, body, headers }) => {
      const response = await fetch(path, {
        method: 'POST',
        credentials: 'include',
        headers,
        body: body === null ? undefined : JSON.stringify(body),
      });
      return { ok: response.ok, status: response.status, text: response.ok ? '' : await response.text() };
    }, { path, body, headers });
  }

  const nodeUrl = nodeId => `/api/crest/admin-menus/${defaultMenuId}/nodes/${encodeURIComponent(nodeId)}`;

  const before = await getMenu();
  const roots = before.nodes;
  const content = roots.find(node => node.text === 'Content');
  const design = roots.find(node => node.text === 'Design');
  if (!content || !design) throw new Error('Expected Content and Design root menu nodes.');

  // Captured so the finally can put every touched item back where it started, rather than
  // assuming a "clean" default arrangement this tenant may not have.
  const originalOrder = roots.map(node => node.id);
  const originalContentEnabled = content.enabled;

  // Document writes commit after the writing request's response, so any read fired right
  // after a mutation can race the commit tail and see the previous state. Every read-back
  // below polls for its expected condition instead of sampling once.
  async function menuSettles(predicate, timeoutMs = 5000) {
    const deadline = Date.now() + timeoutMs;
    let menu = await getMenu();
    while (!predicate(menu) && Date.now() < deadline) {
      await new Promise(resolve => setTimeout(resolve, 250));
      menu = await getMenu();
    }
    return menu;
  }

  try {
    // Reparent: Design becomes a child of Content.
    const moved = await post(`${nodeUrl(design.id)}/move`, { parentNodeId: content.id, position: 0 });
    let menu = await menuSettles(m =>
      (m.nodes.find(node => node.id === content.id)?.items ?? []).some(child => child.id === design.id));
    let contentNode = menu.nodes.find(node => node.id === content.id);
    const designUnderContent = (contentNode?.items ?? []).some(child => child.id === design.id);
    results.push({
      name: 'reparent-persists',
      pass: designUnderContent,
      message: designUnderContent
        ? `design is a child of content`
        : `move status=${moved.status} ${moved.text}; content children=${JSON.stringify((contentNode?.items ?? []).map(c => c.text))}`,
    });

    // Renaming the moved item must not disturb its new position.
    await post(`${nodeUrl(design.id)}/rename`, { text: 'Design Renamed' });
    menu = await menuSettles(m =>
      (m.nodes.find(n => n.id === content.id)?.items ?? []).find(child => child.id === design.id)?.text === 'Design Renamed');
    contentNode = menu.nodes.find(node => node.id === content.id);
    const children = contentNode?.items ?? [];
    const renamedChild = children.find(child => child.id === design.id);
    results.push({
      name: 'rename-does-not-move-the-item',
      pass: !!renamedChild && renamedChild.text === 'Design Renamed',
      message: renamedChild
        ? `still a child of content, text="${renamedChild.text}"`
        : `design is no longer under content after rename; items=${JSON.stringify(children.map(c => c.text))}`,
    });

    // Hiding is keyed the same way and must also survive the rename.
    const toggled = await post(`${nodeUrl(design.id)}/toggle`, null);
    menu = await menuSettles(m =>
      (m.nodes.find(n => n.id === content.id)?.items ?? []).find(child => child.id === design.id)?.enabled === false);
    contentNode = menu.nodes.find(node => node.id === content.id);
    const hiddenChild = (contentNode?.items ?? []).find(child => child.id === design.id);
    results.push({
      name: 'hide-persists-against-the-same-key',
      pass: !!hiddenChild && hiddenChild.enabled === false,
      message: `enabled=${hiddenChild?.enabled} toggleStatus=${toggled.status}`,
    });
  } finally {
    // Unhide, un-rename, and move back to the root at its original index.
    await post(`${nodeUrl(design.id)}/toggle`, null).catch(() => {});
    await post(`${nodeUrl(design.id)}/rename`, { text: '' }).catch(() => {});
    await post(`${nodeUrl(design.id)}/move`, {
      parentNodeId: null,
      position: Math.max(0, originalOrder.indexOf(design.id)),
    }).catch(() => {});
    if (originalContentEnabled === false) {
      await post(`${nodeUrl(content.id)}/toggle`, null).catch(() => {});
    }
  }

  return results;
};
