namespace Crest.Models;

/// <summary>An Option List as callers see it.</summary>
public sealed record CrestOptionListModel(
    string ContentItemId,
    string Key,
    string DisplayText,
    string Source,
    CrestOptionModel[] Options);

/// <summary>A single option. <paramref name="Key"/> is what code compares;
/// <paramref name="DisplayText"/> is what humans read.</summary>
public sealed record CrestOptionModel(
    string ContentItemId,
    string Key,
    string DisplayText,
    string Source,
    bool Hidden,
    int Position);

/// <summary>A module's declaration of one option it ships in a set.</summary>
public sealed record CrestOptionSeed(string Key, string DisplayText, int Position = 0);

/// <summary>A module's declaration of a set it owns, with its base options.</summary>
public sealed record CrestOptionListSeed(string Key, string DisplayText, IReadOnlyList<CrestOptionSeed> Options);

/// <summary>Outcome of validating a key before it is written.</summary>
public sealed record CrestOptionKeyValidation(bool IsValid, string? Error)
{
    public static readonly CrestOptionKeyValidation Valid = new(true, null);

    public static CrestOptionKeyValidation Invalid(string error) => new(false, error);
}
