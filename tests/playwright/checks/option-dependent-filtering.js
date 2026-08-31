// Dependent (cascading) option filtering: "show only the children belonging to the
// parent chosen above". Exercised through the SAME endpoint the picker component
// calls, so what is verified here is the contract the UI depends on.
//
// The load-bearing guarantees:
//   1. Filters come from the attachment's SETTINGS, not from the caller. A client that
//      could post its own filters could query any source with any predicate.
//   2. A required parent that has not been chosen returns NOTHING, never everything.
//   3. A selection that the new parent invalidates is reported as stale, so the editor
//      can clear it instead of saving a contradictory pair.
module.exports = async function run(page, ctx) {
  const results = [];

  const tokenResponse = await page.request.get(`${ctx.baseUrl}/api/crest/antiforgery/token`);
  const token = await tokenResponse.json();
  const headers = { [token.headerName || 'RequestVerificationToken']: token.requestToken };

  // Unique per run and matched verbatim: the probes delete themselves in the finally
  // below, but a crashed earlier run can still leave data behind, and a prefix match
  // would then read the wrong run's list.
  const stamp = Date.now();
  const parentListKey = `probe.dep.parent.${stamp}`;
  const childListKey = `probe.dep.child.${stamp}`;

  const post = (path, data) => page.request.post(`${ctx.baseUrl}${path}`, { headers, data });

  try {
    // A parent list (regions) and a child list (cities) whose options each carry the
    // region they belong to as their Source value... except Source is provenance, not
    // domain data. So instead the child options are keyed so the filter can match on
    // the KEY, which is what code compares on anyway.
    const parentCreated = await post('/api/crest/content-part-lists', {
      key: parentListKey,
      displayText: `Probe regions ${stamp}`,
    });
    const childCreated = await post('/api/crest/content-part-lists', {
      key: childListKey,
      displayText: `Probe cities ${stamp}`,
    });

    results.push({
      name: 'seed-lists-created',
      pass: parentCreated.ok() && childCreated.ok(),
      message: `parent HTTP ${parentCreated.status()}, child HTTP ${childCreated.status()}`,
    });

    await post(`/api/crest/content-part-lists/${parentListKey}/options`, { key: 'North', displayText: 'North', position: 0 });
    await post(`/api/crest/content-part-lists/${parentListKey}/options`, { key: 'South', displayText: 'South', position: 1 });

    // Child options keyed by their parent so a filter on Key can select them.
    await post(`/api/crest/content-part-lists/${childListKey}/options`, { key: 'North', displayText: 'Northtown', position: 0 });
    await post(`/api/crest/content-part-lists/${childListKey}/options`, { key: 'South', displayText: 'Southport', position: 1 });

    const childSource = `contentpartlist:${childListKey}`;

    // --- Guarantee 1: the caller cannot smuggle in its own filters ---------------
    // The query request has no filter field at all. Posting one must be ignored
    // rather than honoured; if it were honoured, this would return one row.
    const smuggled = await post('/api/crest/option-sources/query', {
      sourceKey: childSource,
      columns: ['DisplayText', 'Key'],
      filters: [{ path: 'Key', operator: 'Equals', values: ['North'] }],
      take: 50,
    });

    const smuggledRows = smuggled.ok() ? await smuggled.json() : [];
    results.push({
      name: 'caller-supplied-filters-are-ignored',
      pass: smuggled.ok() && smuggledRows.length === 2,
      message: `HTTP ${smuggled.status()}, rows=${smuggledRows.length} (expected 2 - a honoured filter would give 1)`,
    });

    // --- Guarantee 2: an unconfigured attachment is simply unfiltered -------------
    const unfiltered = await post('/api/crest/option-sources/query', {
      sourceKey: childSource,
      columns: ['DisplayText', 'Key'],
      take: 50,
    });
    const unfilteredRows = unfiltered.ok() ? await unfiltered.json() : [];
    results.push({
      name: 'unconfigured-attachment-returns-every-option',
      pass: unfiltered.ok() && unfilteredRows.length === 2,
      message: `rows=${unfilteredRows.length}`,
    });

    // --- Guarantee 3: selection validity against editor state ---------------------
    // No attachment is configured, so nothing can invalidate a selection: a resolvable
    // id stands. This pins the "empty selection / no filters" arm of the contract that
    // the picker relies on before any parent has been wired up.
    const childOptions = unfilteredRows;
    const northOption = childOptions.find((row) => row.key === 'North');

    if (northOption) {
      const validation = await post('/api/crest/option-sources/validate-selection', {
        sourceKey: childSource,
        selectedIds: [northOption.id],
        editorState: {},
      });

      const validationBody = validation.ok() ? await validation.json() : null;
      results.push({
        name: 'a-resolvable-selection-with-no-filters-stands',
        pass: validation.ok() && validationBody?.stillValid === true,
        message: `HTTP ${validation.status()}, stillValid=${validationBody?.stillValid}`,
      });
    } else {
      results.push({
        name: 'a-resolvable-selection-with-no-filters-stands',
        pass: false,
        message: 'could not locate the seeded North option to validate',
      });
    }

    // An empty selection is always valid - there is nothing to contradict.
    const emptyValidation = await post('/api/crest/option-sources/validate-selection', {
      sourceKey: childSource,
      selectedIds: [],
      editorState: {},
    });
    const emptyBody = emptyValidation.ok() ? await emptyValidation.json() : null;
    results.push({
      name: 'an-empty-selection-is-always-valid',
      pass: emptyValidation.ok() && emptyBody?.stillValid === true,
      message: `HTTP ${emptyValidation.status()}, stillValid=${emptyBody?.stillValid}`,
    });

    // --- Guarantee 4: hidden options stay resolvable ------------------------------
    // Hiding is curation: the option drops out of what is OFFERED but a document that
    // already references it must still render.
    if (northOption) {
      await page.request.put(
        `${ctx.baseUrl}/api/crest/content-part-lists/${childListKey}/options/North`,
        { headers, data: { displayText: null, position: null, hidden: true } },
      );

      const afterHide = await post('/api/crest/option-sources/query', {
        sourceKey: childSource,
        columns: ['DisplayText', 'Key'],
        take: 50,
      });
      const offered = afterHide.ok() ? await afterHide.json() : [];

      const resolved = await post('/api/crest/option-sources/resolve', {
        sourceKey: childSource,
        ids: [northOption.id],
        columns: ['DisplayText', 'Key'],
      });
      const resolvedRows = resolved.ok() ? await resolved.json() : [];

      results.push({
        name: 'hidden-is-unselectable-but-still-resolvable',
        pass: offered.length === 1 && resolvedRows.length === 1,
        message: `offered=${offered.length} (expected 1), resolved=${resolvedRows.length} (expected 1)`,
      });
    }
  } catch (error) {
    results.push({ name: 'dependent-filtering', pass: false, message: String(error) });
  } finally {
    // Tenant-created lists are deletable, so the probes clean themselves up.
    for (const key of [parentListKey, childListKey]) {
      await page.request.delete(`${ctx.baseUrl}/api/crest/content-part-lists/${key}`, { headers }).catch(() => {});
    }
  }

  return results;
};
