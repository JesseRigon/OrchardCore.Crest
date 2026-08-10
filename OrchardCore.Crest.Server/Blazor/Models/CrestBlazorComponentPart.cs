using OrchardCore.ContentManagement;

namespace Crest.Blazor.Models;

// A content item with this part is a tenant-placed reference to a registered Blazor
// component - the same idea as WidgetsListPart/BagPart's content-item-as-tree-node
// pattern, not a bespoke document format. ComponentName is looked up in
// ICrestBlazorComponentRegistry; Parameters is deliberately string-keyed, mirroring
// PlacementNode/Template, rather than a typed schema.
public class CrestBlazorComponentPart : ContentPart
{
    public string ComponentName { get; set; } = string.Empty;

    public Dictionary<string, string> Parameters { get; set; } = [];
}
