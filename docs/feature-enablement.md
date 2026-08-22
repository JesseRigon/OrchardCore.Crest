# Feature enablement: dependencies vs. the setup recipe

When a Crest page, controller, or admin-menu entry needs an OrchardCore feature, there
are two places that need can be expressed. Picking the wrong one produces a tenant that
works on your machine and breaks on a fresh provision.

## Rule

**If Crest code cannot function without it, declare it in the manifest.** Never add it to
a recipe to "fix" a missing feature — a recipe entry only affects tenants provisioned
from that recipe, so the gap silently returns for every other host, and for anyone who
toggles features by hand.

**If it is a site-level choice about what this particular deployment runs**, it belongs
in the host's setup recipe (fruitful's is `recipes/fruitful.saas.recipe.json`).

## Why the manifest is the real fix

`OrchardCore.Crest` is `IsAlwaysEnabled = true`, and Orchard's feature system refuses to
disable a feature while an enabled dependent declares it. So a correct manifest
dependency is self-healing: it auto-enables on setup, survives another recipe's
`feature.disable` list, and survives manual toggling in the admin UI. See commit
`5c022f2` in the `OrchardCore.Crest` submodule, which audited every constructor-injected
`OrchardCore.*` type in the Server assembly against its real registration site.

Note that the lazy `ICrestRequestAccess.GetRequiredService<T>()` pattern does **not**
remove the need for a dependency. It defers the failure from startup to request time
instead of preventing it — the endpoint still throws "Unable to resolve service for type
T" when the providing feature is off.

## What is deliberately NOT a Crest dependency

- **`OrchardCore.Tenants`** — `DefaultTenantOnly = true`. A hard dependency from an
  always-enabled feature would be invalid on every non-default tenant.
  `CrestTenantsController` is already restricted to the default tenant and injects only
  `IOptions<TenantsOptions>`, which always resolves to a safe default
  (`TenantRemovalAllowed = false`).
- **`OrchardCore.Queries.Sql`** — a *provider* of `IQuerySource`, not a requirement.
  Crest enumerates whatever sources a tenant has and rejects unknown ones; declaring it
  would force a SQL query feature onto every tenant.
- **`OrchardCore.Workflows`** — no Crest code references it. It appears in the fruitful
  host's admin-menu layout and in the `legacy-frame-workflows` Playwright check, both
  host-level concerns.
- **`Accounting`** — a standalone business module that depends on Crest, not the reverse.

These four are host choices, which is why the three the fruitful host wants
(`Accounting`, `OrchardCore.Workflows`, `OrchardCore.Tenants`) are listed in its setup
recipe's `feature` step. Everything else Crest needs arrives through the dependency
graph.

## Verifying a change

A fresh provision is the only proof, because an existing tenant keeps whatever features
it was provisioned with:

```bash
bash dev/dev.sh reset
bash dev/dev.sh server        # wait for /Login -> 200
curl -s -b "<auth cookies>" http://localhost:5010/api/crest/features
```

A feature that is enabled without appearing in the recipe's `enable` list came from the
dependency graph — that is the result you want.
