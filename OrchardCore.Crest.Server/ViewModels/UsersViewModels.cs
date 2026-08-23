using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Crest.Services;
using OrchardCore.Users;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using OrchardCore.Users.Services;
using YesSql;

namespace Crest.ViewModels;

public sealed record CrestUserList(int Total, CrestUser[] Items);

public sealed record CrestUser(string Id, string? UserName, string? Email, string? PhoneNumber, bool EmailConfirmed, bool IsEnabled, bool TwoFactorEnabled, string[] Roles)
{
    public static CrestUser From(User user) => new(user.UserId, user.UserName, user.Email, user.PhoneNumber, user.EmailConfirmed, user.IsEnabled, user.TwoFactorEnabled, user.RoleNames?.ToArray() ?? []);
}

public sealed record CrestUserWrite(string? UserName, string? Email, string? PhoneNumber, bool EmailConfirmed, bool IsEnabled, string[]? Roles, string? Password);

public sealed record CrestUserEnabled(bool Enabled);
