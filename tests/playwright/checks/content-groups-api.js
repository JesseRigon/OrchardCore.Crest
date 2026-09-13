// Content groups (api/crest/content-groups): module-declared sets of content
// SOURCES (types, option lists) merged with the tenant's document. Asserts the
// Parties and Members providers surface (Person is a "type" entry, the honorifics
// list is a "list" entry), that ?group= on the content-items list returns only
// what the group covers, that a tenant override (rename, add/remove entry) round
// trips and is undone, and that the auto-menu-pages setting round trips - with the
// menu entry appearing (deferred sync, so reload-and-retry) and disappearing.
module.exports = async function run(page, ctx) {
  async function getAntiforgeryToken() {
    return page.evaluate(async () => {
      const response = await fetch('/api/crest/antiforgery/token', { credentials: 'include' });
      if (!response.ok) throw new Error(`antiforgery failed: ${response.status}`);
      return response.json();
    });
  }

  async function api(method, url, body) {
    const token = await getAntiforgeryToken();
    return page.evaluate(
      async ({ method, url, body, token }) => {
        const response = await fetch(url, {
          method,
          credentials: 'include',
          headers: {
            'Content-Type': 'application/json',
            [token.headerName || 'RequestVerificationToken']: token.requestToken,
          },
          body: body === null || body === undefined ? undefined : JSON.stringify(body),
        });
        const text = await response.text();
        let json = null;
        try { json = text ? JSON.parse(text) : null; } catch {}
        return { ok: response.ok, status: response.status, text, json };
      },
      { method, url, body, token },
    );
  }

  async function findGroupMenuEntry(key) {
    return page.evaluate(async key => {
      const response = await fetch('/api/crest/navigation/admin', { credentials: 'include' });
      if (!response.ok) return null;
      const data = await response.json();
      let found = null;
      const wanted = `/Contents/ContentItems?group=${encodeURIComponent(key)}`;
      const walk = items => {
        for (const item of items || []) {
          const url = String(item.url || item.href || '');
          if (url.endsWith(wanted)) found = { url, text: item.text || item.textKey || '' };
          walk(item.items);
        }
      };
      walk(Array.isArray(data) ? data : data.items);
      return found;
    }, key);
  }

  async function waitForMenuState(key, present) {
    for (let attempt = 0; attempt < 10; attempt++) {
      const entry = await findGroupMenuEntry(key);
      if (!!entry === present) return entry || true;
      await page.waitForTimeout(1500);
    }
    return null;
  }

  const results = [];
  const check = (name, pass, message) => results.push({ name, pass, message });
  await page.goto(`${ctx.baseUrl}/Admin`, { waitUntil: 'networkidle' });

  const originalSettings = (await api('GET', '/api/crest/content-groups/settings', null)).json;
  const stamp = Date.now();

  try {
    const list = await api('GET', '/api/crest/content-groups', null);
    const groups = Array.isArray(list.json) ? list.json : [];
    const parties = groups.find(group => group.key === 'parties');
    const members = groups.find(group => group.key === 'members');
    check('groups-list-ok', list.ok, `HTTP ${list.status}`);
    check('parties-group-declared', !!parties && parties.source === 'Module', parties ? `${parties.entries.length} entries` : 'missing');
    check('members-group-declared', !!members && members.source === 'Module', members ? `${members.entries.length} entries` : 'missing');
    check('parties-has-person-type', !!parties?.entries.find(entry => entry.kind === 'type' && entry.key === 'Person' && entry.resolved), 'type:Person resolved');
    check('parties-has-honorifics-list', !!parties?.entries.find(entry => entry.kind === 'list' && entry.key === 'global.honorifics' && entry.resolved), 'list:global.honorifics resolved');
    check('members-has-perk-list', !!members?.entries.find(entry => entry.kind === 'list' && entry.key === 'members.perk'), 'list:members.perk');

    // A group filter returns only what the group covers: every row is either one
    // of the group's types or one of its list items (a list item is typed ContentPartList).
    const filtered = await api('GET', '/api/crest/content-items?group=members&pageSize=100', null);
    const items = filtered.json?.items || [];
    const allowedTypes = new Set(members?.entries.filter(entry => entry.kind === 'type').map(entry => entry.key) || []);
    const outside = items.filter(item => !allowedTypes.has(item.contentType) && item.contentType !== 'ContentPartList');
    check('group-filter-ok', filtered.ok, `HTTP ${filtered.status} ${items.length} rows`);
    check('group-filter-confined', outside.length === 0, outside.length ? `outside: ${outside.map(item => item.contentType).join(',')}` : 'all rows in group');
    check('group-filter-includes-lists', items.some(item => item.contentType === 'ContentPartList'), 'members lists present');

    const unknown = await api('GET', `/api/crest/content-items?group=no-such-group-${stamp}`, null);
    check('unknown-group-404', unknown.status === 404, `HTTP ${unknown.status}`);

    // Tenant override round trip: rename, remove a module entry, add a list, undo.
    const renamed = await api('PUT', '/api/crest/content-groups/members', { displayName: `Members ${stamp}` });
    check('rename-group', renamed.ok && renamed.json?.displayName === `Members ${stamp}`, `HTTP ${renamed.status}`);
    const removed = await api('DELETE', '/api/crest/content-groups/members/entries/type/Perk', null);
    check('remove-module-entry', removed.ok && !removed.json?.entries.find(entry => entry.kind === 'type' && entry.key === 'Perk'), `HTTP ${removed.status}`);
    const added = await api('POST', '/api/crest/content-groups/members/entries', { kind: 'list', key: 'global.honorifics' });
    check('add-tenant-entry', added.ok && added.json?.entries.find(entry => entry.kind === 'list' && entry.key === 'global.honorifics')?.source === 'Tenant', `HTTP ${added.status} ${added.text}`);
    const badKind = await api('POST', '/api/crest/content-groups/members/entries', { kind: 'nope', key: 'x' });
    check('unknown-kind-400', badKind.status === 400, `HTTP ${badKind.status}`);

    await api('DELETE', '/api/crest/content-groups/members/entries/list/global.honorifics', null);
    const restoredEntry = await api('POST', '/api/crest/content-groups/members/entries', { kind: 'type', key: 'Perk' });
    await api('PUT', '/api/crest/content-groups/members', { displayName: '' });
    const after = (await api('GET', '/api/crest/content-groups', null)).json?.find(group => group.key === 'members');
    check('override-undone', restoredEntry.ok && after?.displayName === 'Members'
      && !!after?.entries.find(entry => entry.kind === 'type' && entry.key === 'Perk' && entry.source === 'Module')
      && !after?.entries.find(entry => entry.key === 'global.honorifics'), `name=${after?.displayName}`);

    // Auto menu pages: on -> entry under Content per group; off -> gone.
    const on = await api('PUT', '/api/crest/content-groups/settings', { autoMenuPages: true });
    check('settings-on', on.ok && on.json?.autoMenuPages === true, `HTTP ${on.status}`);
    const entry = await waitForMenuState('members', true);
    check('group-menu-entry-present', !!entry, entry?.url || 'no ?group=members entry');
    const off = await api('PUT', '/api/crest/content-groups/settings', { autoMenuPages: false });
    check('settings-off', off.ok && off.json?.autoMenuPages === false, `HTTP ${off.status}`);
    check('group-menu-entry-removed', !!(await waitForMenuState('members', false)), 'entry gone');
  } finally {
    if (originalSettings) await api('PUT', '/api/crest/content-groups/settings', { autoMenuPages: !!originalSettings.autoMenuPages });
  }

  return results;
};
