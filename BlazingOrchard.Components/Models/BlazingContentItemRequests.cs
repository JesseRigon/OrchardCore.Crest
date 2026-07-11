using System.Text.Json.Nodes;

namespace BlazingOrchard.Components.Models;

public sealed record UpdateBlazingContentItemRequest(
    string? DisplayText,
    JsonNode? Content = null,
    bool Publish = false);

public sealed record CreateBlazingContentItemRequest(
    string ContentType,
    string? DisplayText,
    bool Publish = false);

public sealed record BlazingAntiforgeryToken(
    string HeaderName,
    string RequestToken);
