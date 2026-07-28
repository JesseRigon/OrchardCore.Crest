# Admin menu layout

Crest treats the built-in Orchard admin menu as a generated primary navigation. Tenant node/order/icon/separator changes made in the Admin Menus page are stored in the tenant document store as layout overrides.

Primary navigation component settings, such as collapse behavior, tier spacing, generated separators, and tier backgrounds, are tenant site settings. They are edited from the same Admin Menus page for UX, but they are not part of the menu layout overlay JSON.

## Recipe import

Hosts can import primary navigation overrides from a recipe with the `CrestAdminMenuLayout` step:

```json
{
  "name": "CrestAdminMenuLayout",
  "file": "crest-admin-menu-layout.json"
}
```

The `file` value is resolved relative to the recipe file. Crest registers this recipe step, but it does not add the step to any host recipe automatically.

## UI export

The Admin Menus page shows an `Export JSON` button on the built-in Primary Navigation. The button exports the current tenant layout to:

```text
<host content root>/recipes/crest-admin-menu-layout.json
```

In a normal source checkout, the host content root is the host app repository folder, so the default export lands in the host app's `recipes` folder and can be versioned with the host recipe.

The export endpoint is enabled automatically in `Development`. In other environments, the host must opt in:

```json
{
  "Crest": {
    "AdminMenuLayoutExport": {
      "Enabled": true
    }
  }
}
```

The endpoint still requires the current user to have Orchard's admin menu management permission.
