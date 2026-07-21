using System.Text.Json.Nodes;

namespace Crest.Components.Models;

public sealed record UpdateCrestContentItemRequest(
    string? DisplayText,
    JsonNode? Content = null,
    bool Publish = false);

public sealed record CreateCrestContentItemRequest(
    string ContentType,
    string? DisplayText,
    bool Publish = false);

public sealed record CrestAntiforgeryToken(
    string HeaderName,
    string RequestToken);
