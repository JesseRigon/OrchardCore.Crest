using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Templates;
using OrchardCore.Templates.Models;
using OrchardCore.Templates.Services;
using TemplatesPermissions = OrchardCore.Templates.Permissions;

namespace Crest.ViewModels;

public sealed record CrestTemplate(string Name, string? Description, string Content);

public sealed record CrestTemplateWrite(string? Description, string? Content);
