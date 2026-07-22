using Microsoft.AspNetCore.Mvc;
using Crest.Services;
using OrchardCore.Recipes;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace Crest.Controllers;

[ApiController]
[AutoValidateAntiforgeryToken]
[Route("api/crest/recipes")]
public sealed class CrestRecipesController(ICrestRequestAccess requestAccess) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<CrestRecipe[]>> ListAsync()
    {
        var access = await requestAccess.AuthorizeAsync(User, RecipePermissions.ManageRecipes);
        if (access is null) return Forbid();
        return Ok((await HarvestAsync(access)).Select(CrestRecipe.From).OrderBy(recipe => recipe.DisplayName).ToArray());
    }

    [HttpPost("execute")]
    public async Task<IActionResult> ExecuteAsync([FromBody] CrestRecipeKey key)
    {
        var access = await requestAccess.AuthorizeAsync(User, RecipePermissions.ManageRecipes);
        if (access is null) return Forbid();
        var recipe = (await HarvestAsync(access)).FirstOrDefault(candidate => candidate.BasePath == key.BasePath && candidate.RecipeFileInfo.Name == key.FileName);
        if (recipe is null) return NotFound();
        try { await access.GetRequiredService<IRecipeExecutor>().ExecuteAsync(Guid.NewGuid().ToString("n"), recipe, new Dictionary<string, object>(), CancellationToken.None); return NoContent(); }
        catch (Exception exception) { return BadRequest(exception.Message); }
    }

    private static async Task<IEnumerable<RecipeDescriptor>> HarvestAsync(CrestAuthorizedRequest access)
    {
        var harvesters = access.GetRequiredService<IEnumerable<IRecipeHarvester>>();
        return (await Task.WhenAll(harvesters.Select(harvester => harvester.HarvestRecipesAsync())))
            .SelectMany(recipes => recipes)
            .Where(recipe => !recipe.IsSetupRecipe && (recipe.Tags is null || !recipe.Tags.Contains("hidden", StringComparer.OrdinalIgnoreCase)));
    }
}

public sealed record CrestRecipe(string Name, string? DisplayName, string? Description, string? FileName, string? BasePath, string[]? Tags)
{ public static CrestRecipe From(RecipeDescriptor recipe) => new(recipe.Name, recipe.DisplayName, recipe.Description, recipe.RecipeFileInfo.Name, recipe.BasePath, recipe.Tags); }
public sealed record CrestRecipeKey(string? BasePath, string? FileName);
