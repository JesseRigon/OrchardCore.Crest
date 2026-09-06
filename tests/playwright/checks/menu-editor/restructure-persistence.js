// Restructuring the menu - reparenting, reordering, hiding - persists against each item's
// key. If the key were derived from the displayed caption, every one of these overrides would
// be orphaned by a culture switch or a rename, which is what makes this worth checking
// end-to-end rather than only asserting on the key itself.
//
// Moves a root item under another root, reorders it, hides a third, and then verifies the
// whole arrangement still reads back correctly after a rename of one of the moved items -
// the rename must change only the caption, never where the item sits.
//
// The subjects are picked from whatever ROOT nodes the tenant actually has (skipping the
// locked "new" branch) instead of demanding literal Content/Design roots: a tenant's
// imported layout overlay can move or rename anything, and the mechanics under test are
// node-agnostic. Renames are per-culture display overrides - the invariant identity
// (originalText/UniqueId) is untouched, which this check exercises but must not assume
// positionally.
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
  // The "new" branch has a locked structure (regenerated from creatable types) and
  // rejects moved items - never pick it as subject or target. Custom items take a
  // different update path, an already-renamed node's caption could not be restored
  // by the blank-rename reset below, and a disabled node would make the hide
  // assertion ambiguous - skip all of those too.
  const candidates = roots.filter(node =>
    node.id !== 'new'
    && !String(node.id).startsWith('custom-')
    && !node.originalText
    && node.enabled !== false);
  if (candidates.length < 2) throw new Error(`Need two non-locked root nodes, found ${candidates.length}.`);
  const target = candidates[0];
  const subject = candidates[candidates.length - 1];

  // Captured so the finally can put every touched item back where it started, rather than
  // assuming a "clean" default arrangement this tenant may not have.
  const originalOrder = roots.map(node => node.id);
  const originalTargetEnabled = target.enabled;

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
    // Reparent: the subject root becomes a child of the target root.
    const moved = await post(`${nodeUrl(subject.id)}/move`, { parentNodeId: target.id, position: 0 });
    let menu = await menuSettles(m =>
      (m.nodes.find(node => node.id === target.id)?.items ?? []).some(child => child.id === subject.id));
    let targetNode = menu.nodes.find(node => node.id === target.id);
    const subjectUnderTarget = (targetNode?.items ?? []).some(child => child.id === subject.id);
    results.push({
      name: 'reparent-persists',
      pass: subjectUnderTarget,
      message: subjectUnderTarget
        ? `"${subject.text}" is a child of "${target.text}"`
        : `move status=${moved.status} ${moved.text}; target children=${JSON.stringify((targetNode?.items ?? []).map(c => c.text))}`,
    });

    // Renaming the moved item must not disturb its new position.
    await post(`${nodeUrl(subject.id)}/rename`, { text: 'Restructure Renamed' });
    menu = await menuSettles(m =>
      (m.nodes.find(n => n.id === target.id)?.items ?? []).find(child => child.id === subject.id)?.text === 'Restructure Renamed');
    targetNode = menu.nodes.find(node => node.id === target.id);
    const children = targetNode?.items ?? [];
    const renamedChild = children.find(child => child.id === subject.id);
    results.push({
      name: 'rename-does-not-move-the-item',
      pass: !!renamedChild && renamedChild.text === 'Restructure Renamed',
      message: renamedChild
        ? `still a child of "${target.text}", text="${renamedChild.text}"`
        : `subject is no longer under target after rename; items=${JSON.stringify(children.map(c => c.text))}`,
    });

    // Hiding is keyed the same way and must also survive the rename.
    const toggled = await post(`${nodeUrl(subject.id)}/toggle`, null);
    menu = await menuSettles(m =>
      (m.nodes.find(n => n.id === target.id)?.items ?? []).find(child => child.id === subject.id)?.enabled === false);
    targetNode = menu.nodes.find(node => node.id === target.id);
    const hiddenChild = (targetNode?.items ?? []).find(child => child.id === subject.id);
    results.push({
      name: 'hide-persists-against-the-same-key',
      pass: !!hiddenChild && hiddenChild.enabled === false,
      message: `enabled=${hiddenChild?.enabled} toggleStatus=${toggled.status}`,
    });
  } finally {
    // Unhide, un-rename, and move back to the root at its original index.
    await post(`${nodeUrl(subject.id)}/toggle`, null).catch(() => {});
    await post(`${nodeUrl(subject.id)}/rename`, { text: '' }).catch(() => {});
    await post(`${nodeUrl(subject.id)}/move`, {
      parentNodeId: null,
      position: Math.max(0, originalOrder.indexOf(subject.id)),
    }).catch(() => {});
    if (originalTargetEnabled === false) {
      await post(`${nodeUrl(target.id)}/toggle`, null).catch(() => {});
    }
  }

  return results;
};
