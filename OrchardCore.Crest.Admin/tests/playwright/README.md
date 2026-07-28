# OrchardCore.Crest.Admin Playwright probes

Run these reusable admin UI probes from the repository root while Fruitful is listening on
`http://fruitful.localhost:5010`:

```bash
node modules/OrchardCore.Crest/OrchardCore.Crest.Admin/tests/playwright/admin-standard-pages.js
node modules/OrchardCore.Crest/OrchardCore.Crest.Admin/tests/playwright/admin-features-page.js
node modules/OrchardCore.Crest/OrchardCore.Crest.Admin/tests/playwright/admin-route-primary-nav-menu.js
node modules/OrchardCore.Crest/OrchardCore.Crest.Admin/tests/playwright/admin-menu-design-system.js
node modules/OrchardCore.Crest/OrchardCore.Crest.Admin/tests/playwright/admin-localization-page.js
node modules/OrchardCore.Crest/OrchardCore.Crest.Admin/tests/playwright/admin-titlebar.js
```

All scripts accept `BASE_URL`, `ADMIN_USER`, and `ADMIN_PASSWORD`; interactive probes also
accept `HEADED=1`. Individual scripts document additional route or output variables near the top.
