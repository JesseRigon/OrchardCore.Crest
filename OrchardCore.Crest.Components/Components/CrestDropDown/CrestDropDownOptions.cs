namespace Crest.Components.Inputs;

public enum CrestDropDownFilterCaseSensitivity
{
    CaseInsensitive,
    CaseSensitive,
}

public enum CrestDropDownFilterOperator
{
    Contains,
    StartsWith,
    EndsWith,
    Equals,
}

public enum CrestDropDownSurface
{
    Trigger,
    Option,
}

public sealed record CrestDropDownRenderContext<TItem>(
    TItem Item,
    CrestDropDownSurface Surface,
    bool IsSelected,
    string Label);
