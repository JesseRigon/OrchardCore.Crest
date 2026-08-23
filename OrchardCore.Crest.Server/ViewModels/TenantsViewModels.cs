using Crest.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using OrchardCore.Environment.Shell;
using OrchardCore.Environment.Shell.Removing;
using OrchardCore.Tenants;
using TenantsPermissions = OrchardCore.Tenants.Permissions;

namespace Crest.ViewModels;

public sealed record CrestTenantCatalog(CrestTenant[] Tenants, string[] Categories, bool TenantRemovalAllowed);

public sealed record CrestTenantBulkAction(string Action, string[]? Names);

public sealed record CrestTenant(string Name, string State, string? Category, string? Description, string? RequestUrlHost, string? RequestUrlPrefix, bool IsDefault, bool IsRemovable)
{
    public static CrestTenant From(ShellSettings settings) => new(settings.Name, settings.State.ToString(), settings["Category"], settings["Description"], settings.RequestUrlHost, settings.RequestUrlPrefix, settings.IsDefaultShell(), settings.IsRemovable());
}
