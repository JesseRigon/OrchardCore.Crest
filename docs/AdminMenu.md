# Admin menu layout

Crest treats the built-in Orchard admin menu as a generated sidebar layout. Tenant changes made in the Admin Menus page are stored in the tenant document store as layout overrides.

## Recipe import

Hosts can import sidebar layout overrides from a recipe with the `CrestAdminMenuLayout` step:

```json
{
  "name": "CrestAdminMenuLayout",
  "file": "crest-admin-menu-layout.json"
}
```

The `file` value is resolved relative to the recipe file. Crest registers this recipe step, but it does not add the step to any host recipe automatically.

## UI export

The Admin Menus page shows an `Export layout JSON` button on the built-in Sidebar Layout. The button exports the current tenant layout to:

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
