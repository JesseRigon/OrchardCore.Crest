using System.Text.Json.Nodes;

namespace Crest.Components.Models;

// These mirror the server's ContentItemWriteRequest (api/crest/content-items): both
// the create (POST) and update (PUT) actions bind the SAME record, so ContentType is
// required on updates too, and Content must be a JsonObject - a bare JsonNode fails
// model binding with a 400.
public sealed record UpdateCrestContentItemRequest(
    string ContentType,
    string? DisplayText,
    JsonObject? Content = null,
    bool Publish = false);

public sealed record CreateCrestContentItemRequest(
    string ContentType,
    string? DisplayText,
    JsonObject? Content = null,
    bool Publish = false);

public sealed record CrestAntiforgeryToken(
    string HeaderName,
    string RequestToken);
