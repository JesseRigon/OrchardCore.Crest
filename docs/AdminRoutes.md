# Admin route inventory

This list is generated from the current resolved Orchard admin navigation for the enabled features in the Fruitful host. A route is marked `native` only when it has a dedicated, functional Crest `.razor` page backed by Orchard services. Pending routes continue to use Orchard's existing admin UI until their native implementation is complete.

| Menu | Route | Status |
| --- | --- | --- |
| New > Customer | `/Admin/Contents/ContentTypes/Customer/Create` | native document editor |
| New > Organization | `/Admin/Contents/ContentTypes/Organization/Create` | native document editor |
| New > Partner | `/Admin/Contents/ContentTypes/Partner/Create` | native document editor |
| New > Person | `/Admin/Contents/ContentTypes/Person/Create` | native document editor |
| New > Taxonomy | `/Admin/Contents/ContentTypes/Taxonomy/Create` | native document editor |
| New > Vendor | `/Admin/Contents/ContentTypes/Vendor/Create` | native document editor |
| Content > Admin Menus | `/Admin/AdminMenu/List` | native |
| Content > Site Menus | `/Admin/Contents/ContentItems/Menu` | native |
| Content > Content Definition > Content Types | `/Admin/ContentTypes/List` | native browse |
| Content > Content Definition > Content Parts | `/Admin/ContentTypes/ListParts` | native browse |
| Content > Content Items | `/Admin/Contents/ContentItems` | native list/actions/document editor |
| Media > Profiles | `/Admin/MediaProfiles` | native list/editor |
| Media > Library | `/Admin/Media` | native browse/upload |
| Media > Options | `/Admin/Media/Options` | native read-only configuration |
| Design > Themes | `/Admin/Themes` | native |
| Design > Design System | `/Admin/DesignSystem` | native |
| Design > Templates | `/Admin/Templates` | native list/editor |
| Design > Zones | `/Admin/Settings/zones` | standard Orchard UI (deferred) |
| Design > Placements | `/Admin/Placements` | standard Orchard UI (deferred) |
| Design > Widgets | `/Admin/Layers` | standard Orchard UI (deferred) |
| Design > Shortcodes | `/Admin/Shortcodes` | standard Orchard UI (deferred) |
| Design > Icons | `/Admin/Design/Icons` | native |
| CRM > Customers | `/Admin/CRM/Customers` | native host module |
| Settings > General | `/Admin/Settings/general` | native |
| Settings > Admin | `/Admin/Settings/admin` | native |
| Settings > Access Control > Roles | `/Admin/Roles/Index` | native browse |
| Settings > Access Control > Users | `/Admin/Users/Index` | native list/create/edit/actions |
| Settings > Security > Security Headers | `/Admin/Settings/SecurityHeaders` | native settings editor |
| Settings > Security > User Login | `/Admin/Settings/userLogin` | native settings editor |
| Settings > Features | `/Admin/Features` | native |
| Settings > Recipes | `/Admin/Recipes` | native list/execute |
| Settings > Localization > Cultures | `/Admin/Settings/localization` | native settings editor |
| Settings > Search > Indexes | `/Admin/indexing` | native list/rebuild |
| Settings > Search > Queries > All Queries | `/Admin/Queries/Index` | pending |
| Settings > Search > Queries > Run SQL Query | `/Admin/Queries/Sql/Query` | pending |
| Settings > Deployments > Debugging | `/Admin/Settings/debugging` | pending |
| Settings > Deployments > JSON Import | `/Admin/DeploymentPlan/Import/Json` | pending |
| Settings > Deployments > Package Import | `/Admin/DeploymentPlan/Import/Index` | pending |
| Settings > Deployments > Remote Clients | `/Admin/Deployment/RemoteClient/Index` | pending |
| Settings > Deployments > Remote Instances | `/Admin/Deployment/RemoteInstance/Index` | pending |
| Settings > Deployments > Plans | `/Admin/DeploymentPlan/Index` | pending |
