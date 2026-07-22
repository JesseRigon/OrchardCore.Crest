# OrchardCore.Crest.Icons Playwright probes

Run icon UI and API probes from the repository root:

```bash
node modules/OrchardCore.Crest/OrchardCore.Crest.Icons/tests/playwright/admin-icon-selector.js
node modules/OrchardCore.Crest/OrchardCore.Crest.Icons/tests/playwright/iconify-remote-fallback-api.js
node modules/OrchardCore.Crest/OrchardCore.Crest.Icons/tests/playwright/tenant-media-icons-api.js
```

The app defaults to `http://fruitful.localhost:5010`. Override it with `BASE_URL`; credentials
use `ADMIN_USER` and `ADMIN_PASSWORD`.
