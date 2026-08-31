// Data scope on option sources: what the CURRENT USER may see, as distinct from what
// a dropdown is configured to offer.
//
// The guarantee that matters most here is that RESOLVE cannot be used as a bypass.
// Query and resolve must agree about authorization: a user who cannot query a record
// must not be able to enumerate it by posting its id to resolve instead. Curation
// (hidden/inactive) is the opposite - resolve deliberately ignores it so historical
// documents still render.
//
// An out-of-scope record is ABSENT from the result, never returned as a hollow
// placeholder: a placeholder still asserts that the reference resolves, and a caller
// can act on that as though the record were merely unnamed.
//
// KNOWN LIMIT OF THIS CHECK: the suite runs as a single logged-in administrator (the
// harness is deliberately one login, one warm process), so everything here observes
// the PERMITTED path - authorized reads succeed, and structural bypasses (unknown
// ids, cross-type resolution, unqualified sources) return nothing. The DENIED path -
// a restricted role receiving redacted rows - is covered by unit tests over
// OptionSourceScope rather than here, because asserting it live needs a second
// authenticated context the harness cannot currently produce. That gap is worth
// closing when a role-switching fixture exists; until then the denial logic is
// verified in isolation, not end to end.
module.exports = async function run(page, ctx) {
  const results = [];

  const tokenResponse = await page.request.get(`${ctx.baseUrl}/api/crest/antiforgery/token`);
  const token = await tokenResponse.json();
  const headers = { [token.headerName || 'RequestVerificationToken']: token.requestToken };

  const post = (path, data) => page.request.post(`${ctx.baseUrl}${path}`, { headers, data });

  try {
    // The suite runs as an administrator, so the positive path is what is directly
    // observable: an admin has ViewContent for every type and therefore sees records.
    // The negative path (a restricted role seeing nothing) is covered by unit tests
    // over OptionSourceScope, since switching roles mid-suite would require a second
    // authenticated context.
    const queryAsAdmin = await post('/api/crest/option-sources/query', {
      sourceKey: 'contentitem:ContentPartList',
      columns: ['DisplayText', 'Owner'],
      take: 5,
    });

    const adminRows = queryAsAdmin.ok() ? await queryAsAdmin.json() : [];
    results.push({
      name: 'an-authorized-user-sees-records',
      pass: queryAsAdmin.ok(),
      message: `HTTP ${queryAsAdmin.status()}, rows=${adminRows.length}`,
    });

    // An authorized user gets the real record, with real values - and no placeholder
    // shape anywhere in the payload.
    if (adminRows.length > 0) {
      const resolved = await post('/api/crest/option-sources/resolve', {
        sourceKey: 'contentitem:ContentPartList',
        ids: [adminRows[0].id],
        columns: ['DisplayText'],
      });

      const resolvedRows = resolved.ok() ? await resolved.json() : [];
      const first = resolvedRows[0];
      const hasRealValue = !!first?.values?.DisplayText;

      results.push({
        name: 'in-scope-ids-resolve-to-the-real-record',
        pass: resolved.ok() && resolvedRows.length === 1 && hasRealValue,
        message: `HTTP ${resolved.status()}, display=${first?.values?.DisplayText}`,
      });

      results.push({
        name: 'no-redacted-placeholder-shape-is-returned',
        pass: first !== undefined && !('isRedacted' in first),
        message: `keys=${first ? Object.keys(first).join(',') : 'none'}`,
      });
    }

    // An unknown id must simply not resolve - no row, no error, and above all no
    // fabricated placeholder that would imply the id exists.
    const unknown = await post('/api/crest/option-sources/resolve', {
      sourceKey: 'contentitem:ContentPartList',
      ids: ['definitely-not-a-real-content-item-id'],
      columns: ['DisplayText'],
    });
    const unknownRows = unknown.ok() ? await unknown.json() : [];
    results.push({
      name: 'an-unknown-id-resolves-to-nothing',
      pass: unknown.ok() && unknownRows.length === 0,
      message: `HTTP ${unknown.status()}, rows=${unknownRows.length}`,
    });

    // Resolving must be constrained to the source's OWN type. A caller that names one
    // type but posts ids of another must get nothing back - otherwise scope would be
    // computed for the named type while records of a different type were returned.
    if (adminRows.length > 0) {
      const crossType = await post('/api/crest/option-sources/resolve', {
        sourceKey: 'contentitem:Option',
        ids: [adminRows[0].id],
        columns: ['DisplayText'],
      });
      const crossTypeRows = crossType.ok() ? await crossType.json() : [];
      results.push({
        name: 'resolve-will-not-cross-content-types',
        pass: crossType.ok() && crossTypeRows.length === 0,
        message: `HTTP ${crossType.status()}, rows=${crossTypeRows.length} (an ContentPartList id asked for as an Option)`,
      });
    }

    // A source with no qualifier must not become an unbounded query over every
    // content item in the tenant.
    const unqualified = await post('/api/crest/option-sources/query', {
      sourceKey: 'contentitem:',
      columns: ['DisplayText'],
      take: 50,
    });
    const unqualifiedRows = unqualified.ok() ? await unqualified.json() : [];
    results.push({
      name: 'an-unqualified-content-item-source-returns-nothing',
      pass: unqualified.ok() && unqualifiedRows.length === 0,
      message: `HTTP ${unqualified.status()}, rows=${unqualifiedRows.length}`,
    });

    // Content part lists are tenant CONFIGURATION rather than per-user records, so they
    // carry no ownership scope and stay readable.
    const contentPartListSource = await post('/api/crest/option-sources/query', {
      sourceKey: 'contentpartlist:transaction.status',
      columns: ['DisplayText', 'Key'],
      take: 20,
    });
    results.push({
      name: 'content-part-lists-are-not-owner-scoped',
      pass: contentPartListSource.ok(),
      message: `HTTP ${contentPartListSource.status()}`,
    });
  } catch (error) {
    results.push({ name: 'option-source-scope', pass: false, message: String(error) });
  }

  return results;
};
