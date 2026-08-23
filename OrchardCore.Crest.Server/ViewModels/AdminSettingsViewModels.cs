using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Admin;
using OrchardCore.Admin.Models;
using OrchardCore.Entities;
using OrchardCore.Settings;

namespace Crest.ViewModels;

public sealed record AdminSettingsUpdate(
    bool DisplayThemeToggler,
    bool DisplayMenuFilter,
    bool DisplayNewMenu,
    bool DisplayTitlesInTopbar);
