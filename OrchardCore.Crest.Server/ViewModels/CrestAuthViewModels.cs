using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Users.Services;

namespace Crest.ViewModels;

public sealed record LoginRequest(string UserName, string Password, bool RememberMe);

public sealed record AuthUser(bool IsAuthenticated, string? UserName, string[] Roles);
