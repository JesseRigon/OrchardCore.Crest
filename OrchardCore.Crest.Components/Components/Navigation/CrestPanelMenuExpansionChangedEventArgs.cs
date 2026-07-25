namespace Crest.Components.Navigation;

public sealed class CrestPanelMenuExpansionChangedEventArgs(CrestPanelMenuItem item, bool expanded)
{
    public CrestPanelMenuItem Item { get; } = item;

    public bool Expanded { get; } = expanded;
}
