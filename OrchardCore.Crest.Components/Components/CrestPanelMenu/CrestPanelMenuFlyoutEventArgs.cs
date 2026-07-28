using Microsoft.AspNetCore.Components.Web;

namespace Crest.Components.Navigation;

public sealed class CrestPanelMenuFlyoutEventArgs(CrestPanelMenuItem item, MouseEventArgs mouseEvent)
{
    public CrestPanelMenuItem Item { get; } = item;

    public MouseEventArgs MouseEvent { get; } = mouseEvent;
}
