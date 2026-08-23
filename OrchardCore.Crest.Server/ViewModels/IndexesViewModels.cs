using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Crest.Services;
using OrchardCore.Indexing;
using OrchardCore.Indexing.Core;
using OrchardCore.Indexing.Models;

namespace Crest.ViewModels;

public sealed record CrestIndex(string Id, string? Name, string? Provider, string? IndexName, string? Type, string? CreatedUtc)
{ public static CrestIndex From(IndexProfile profile) => new(profile.Id, profile.Name, profile.ProviderName, profile.IndexName, profile.Type, profile.CreatedUtc.ToString("O")); }
