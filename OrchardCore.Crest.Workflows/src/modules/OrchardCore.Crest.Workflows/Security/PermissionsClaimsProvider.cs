using System.Security.Claims;
using OrchardCore.Users;
using OrchardCore.Users.Services;

namespace OrchardCore.Crest.Workflows.Security;

public class PermissionsClaimsProvider : IUserClaimsProvider
{
    public Task GenerateAsync(IUser user, ClaimsIdentity claims)
    {
        claims.AddClaim(new("permissions", "*"));
        return Task.CompletedTask;
    }
}
