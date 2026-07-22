using System.Text.Json;
using Crest.Services;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace Crest.Recipes;

public sealed class CrestAdminMenuLayoutStep(CrestAdminMenuLayoutService layoutService) : NamedRecipeStepHandler("CrestAdminMenuLayout")
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        PropertyNameCaseInsensitive = true,
    };

    protected override async Task HandleAsync(RecipeExecutionContext context)
    {
        var fileName = context.Step["file"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(fileName))
        {
            context.Errors.Add("CrestAdminMenuLayout recipe step requires a 'file' value.");
            return;
        }

        var relativePath = Path.Combine(context.RecipeDescriptor.BasePath ?? string.Empty, fileName).Replace('\\', '/');
        var file = context.RecipeDescriptor.FileProvider.GetFileInfo(relativePath);
        if (!file.Exists)
        {
            context.Errors.Add($"CrestAdminMenuLayout file '{fileName}' was not found.");
            return;
        }

        await using var stream = file.CreateReadStream();
        var model = await JsonSerializer.DeserializeAsync<CrestAdminMenuLayoutFile>(stream, JsonOptions);
        if (model is null)
        {
            context.Errors.Add($"CrestAdminMenuLayout file '{fileName}' could not be parsed.");
            return;
        }

        await layoutService.ImportAsync(model);
    }
}
