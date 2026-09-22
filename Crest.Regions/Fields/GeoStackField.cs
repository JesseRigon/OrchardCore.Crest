using OrchardCore.ContentManagement;

namespace Crest.Regions.Fields;

/// <summary>
/// The machine half of an address: its resolved geo stack (exactly one node per level,
/// country down), boundary memberships and point. Attached by the module that owns the
/// address-bearing type (Parties attaches it to Address); read by tax, territories and
/// geocoding through the node ids alone.
/// </summary>
public class GeoStackField : ContentField
{
    public List<GeoStackEntry> Stack { get; set; } = [];
    public List<string> BoundaryNodeIds { get; set; } = [];
    public double? Latitude { get; set; }
    public double? Longitude { get; set; }

    public string? NodeAt(int level) => Stack.FirstOrDefault(entry => entry.Level == level)?.NodeId;
    public IReadOnlyList<string> NodeIds => Stack.OrderBy(entry => entry.Level).Select(entry => entry.NodeId).ToArray();
}

public sealed class GeoStackEntry
{
    public int Level { get; set; }
    public string NodeId { get; set; } = string.Empty;
}
