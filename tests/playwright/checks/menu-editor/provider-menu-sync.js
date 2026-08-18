// Provider-contributed menu items are imported into the DB-backed admin menu system, so that
// Crest's sidebar and menu editor only ever deal with admin menu nodes.
//
// This matters because provider items have no dependable identity of their own: 20 of the 57
// upstream admin menu providers never call .Id(...), and those that do use hand-written slugs
// unique only by convention. Nor can their captions be translated at runtime - those come from
// PO files, a deploy-time artifact. Importing them as AdminNodes gives each a UniqueId (which
// the node navigation builders copy onto MenuItem.Id) and a MenuName (the context
// IDataLocalizer needs to hold a tenant-level translation).
//
// The import runs once per shell on first use, and again on demand via sync-providers. It must
// be idempotent: re-running matches existing nodes on the untranslated caption and leaves their
// UniqueIds - and therefore every override stored against them - untouched.
const { fetchAntiforgeryToken } = require('../../harness/antiforgery');

const importedMenuName = 'Primary Navigation';
const GUID_LIKE = /^[0-9a-f]{32}$/i;

module.exports = async function run(page, ctx) {
  const results = [];

  async function post(path) {
    const antiforgery = await fetchAntiforgeryToken(page, ctx.baseUrl);
    return page.evaluate(async ({ path, antiforgery }) => {
      const response = await fetch(path, {
        method: 'POST',
        credentials: 'include',
        headers: { 'content-type': 'application/json', [antiforgery.headerName]: antiforgery.requestToken },
      });
      const text = await response.text();
      let json = null;
      try { json = JSON.parse(text); } catch { /* error bodies are plain text */ }
      return { ok: response.ok, status: response.status, text, json };
    }, { path, antiforgery });
  }

  async function importedNodes() {
    return page.evaluate(async (menuName) => {
      const response = await fetch('/api/crest/admin-menus', { credentials: 'include' });
      if (!response.ok) throw new Error(`admin menus failed: ${response.status}`);
      const data = await response.json();
      const menu = data.menus.find(candidate => candidate.name === menuName);
      const flat = [];
      (function walk(nodes, depth) {
        for (const node of nodes || []) {
          flat.push({ id: node.id, text: node.text, depth });
          walk(node.items, depth + 1);
        }
      })(menu?.nodes, 0);
      return { exists: !!menu, flat };
    }, importedMenuName);
  }

  // The import runs on the first request that reads the admin menu, which the harness has
  // already made by the time this check runs.
  const initial = await importedNodes();
  results.push({
    name: 'provider-items-imported-into-an-admin-menu',
    pass: initial.exists && initial.flat.length > 0,
    message: initial.exists ? `${initial.flat.length} nodes in "${importedMenuName}"` : `no "${importedMenuName}" menu`,
  });

  if (!initial.exists) {
    return results;
  }

  // Every imported node is keyed by its own UniqueId, not by a provider slug or a caption.
  // This is what makes overrides survive both a rename and a culture switch.
  const nonGuid = initial.flat.filter(node => !GUID_LIKE.test(node.id));
  results.push({
    name: 'imported-nodes-are-keyed-by-uniqueid',
    pass: nonGuid.length === 0,
    message: nonGuid.length === 0
      ? `all ${initial.flat.length} ids are uniqueids`
      : `${nonGuid.length} non-guid ids, e.g. ${JSON.stringify(nonGuid.slice(0, 3))}`,
  });

  // The provider menu is a tree, so a flat import would silently lose every submenu.
  const children = initial.flat.filter(node => node.depth > 0);
  results.push({
    name: 'imported-tree-keeps-its-hierarchy',
    pass: children.length > 0,
    message: `${initial.flat.filter(n => n.depth === 0).length} roots, ${children.length} descendants, max depth ${Math.max(...initial.flat.map(n => n.depth))}`,
  });

  // Re-running must be a no-op. If matching were done on the translated caption, or if new
  // UniqueIds were minted each pass, this would add duplicates and orphan every stored override.
  const second = await post('/api/crest/admin-menus/sync-providers');
  const afterSecond = await importedNodes();
  results.push({
    name: 'resync-adds-nothing',
    pass: second.ok && second.json?.added === 0 && afterSecond.flat.length === initial.flat.length,
    message: `status=${second.status} result=${JSON.stringify(second.json)} count ${initial.flat.length} -> ${afterSecond.flat.length}`,
  });

  const beforeIds = initial.flat.map(node => node.id).sort();
  const afterIds = afterSecond.flat.map(node => node.id).sort();
  results.push({
    name: 'resync-preserves-uniqueids',
    pass: JSON.stringify(beforeIds) === JSON.stringify(afterIds),
    message: JSON.stringify(beforeIds) === JSON.stringify(afterIds)
      ? 'identical id set across syncs'
      : `id set changed; ${beforeIds.filter(id => !afterIds.includes(id)).length} lost, ${afterIds.filter(id => !beforeIds.includes(id)).length} new`,
  });

  results.push({
    name: 'resync-creates-no-duplicates',
    pass: new Set(afterIds).size === afterIds.length,
    message: `${afterIds.length} nodes, ${new Set(afterIds).size} distinct`,
  });

  return results;
};
