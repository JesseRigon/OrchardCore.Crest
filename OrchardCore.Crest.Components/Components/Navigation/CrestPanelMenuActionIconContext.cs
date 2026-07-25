namespace Crest.Components.Navigation;

public sealed record CrestPanelMenuActionIconContext(
    CrestPanelMenuItem Item,
    string CssClass,
    string IconKey,
    string Title);
