using Microsoft.AspNetCore.Mvc;
using Crest.Services;
using OrchardCore.Recipes;
using OrchardCore.Recipes.Models;
using OrchardCore.Recipes.Services;

namespace Crest.ViewModels;

public sealed record CrestRecipe(string Name, string? DisplayName, string? Description, string? FileName, string? BasePath, string[]? Tags)
{ public static CrestRecipe From(RecipeDescriptor recipe) => new(recipe.Name, recipe.DisplayName, recipe.Description, recipe.RecipeFileInfo.Name, recipe.BasePath, recipe.Tags); }
public sealed record CrestRecipeKey(string? BasePath, string? FileName);
