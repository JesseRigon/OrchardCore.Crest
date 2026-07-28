using Microsoft.AspNetCore.Components;

namespace Crest.Components.Navigation;

public sealed class CrestPanelMenuItem
{
    public string Key { get; init; } = Guid.NewGuid().ToString("n");

    public string Text { get; init; } = string.Empty;

    public string? Href { get; init; }

    public string? Target { get; init; }

    public RenderFragment? Icon { get; init; }

    public IReadOnlyList<CrestPanelMenuItem> Items { get; init; } = [];

    public bool IsActive { get; init; }

    public bool IsDisabled { get; init; }

    public bool IsSeparator { get; init; }

    public string? CssClass { get; init; }

    public string? LinkCssClass { get; init; }

    public object? Source { get; init; }

    public bool HasChildren => !IsSeparator && Items.Count > 0;

    public bool HasLink => !IsSeparator && !string.IsNullOrWhiteSpace(Href);

    /// <summary>
    /// The DOM id rendered on this item's wrapper, used for roving-focus keyboard
    /// navigation (aria-activedescendant and scroll-into-view targeting).
    /// </summary>
    public string ElementId => $"crest-panel-menu-item-{Key}";
}
