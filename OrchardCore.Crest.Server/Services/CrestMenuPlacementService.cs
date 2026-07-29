using System.Text.Json.Serialization;
using OrchardCore.Data.Documents;
using OrchardCore.Documents;

namespace Crest.Services;

// Vanilla OrchardCore has no concept of "which navigation surface does this custom admin
// menu belong to" — OrchardCore.AdminMenu's own AdminMenu document (see
// OrchardCore.AdminMenu.Abstractions.Models.AdminMenu, a NuGet package type we can't edit)
// unconditionally injects every enabled custom menu into the "admin" sidebar tree. This is
// a Crest-owned companion document, keyed by that same AdminMenu.Id, tracking which
// placement each custom menu actually belongs to (default Admin for anything without an
// entry, so pre-existing menus are unaffected) and — for Local/User placements — an
// independent Enabled flag, since the underlying AdminMenu.Enabled gets forced false for
// those to keep OrchardCore's own coordinator from also rendering them into the sidebar.
[JsonConverter(typeof(JsonStringEnumConverter<CrestMenuPlacement>))]
public enum CrestMenuPlacement
{
    Admin,
    Local,
    User,
}

public sealed class CrestMenuPlacementEntry
{
    public CrestMenuPlacement Placement { get; set; } = CrestMenuPlacement.Admin;
    public bool Enabled { get; set; } = true;
}

public sealed class CrestMenuPlacementDocument : Document
{
    public Dictionary<string, CrestMenuPlacementEntry> Entries { get; set; } = [];
}

public sealed class CrestMenuPlacementService(IDocumentManager<CrestMenuPlacementDocument> documents)
{
    public static readonly CrestMenuPlacementEntry DefaultEntry = new();

    public async ValueTask<CrestMenuPlacementEntry> GetAsync(string menuId)
    {
        var document = await documents.GetOrCreateImmutableAsync();
        return document.Entries.TryGetValue(menuId, out var entry) ? entry : DefaultEntry;
    }

    public async ValueTask<IReadOnlyDictionary<string, CrestMenuPlacementEntry>> GetAllAsync()
    {
        var document = await documents.GetOrCreateImmutableAsync();
        return document.Entries;
    }

    public async ValueTask SetAsync(string menuId, CrestMenuPlacement placement, bool enabled)
    {
        var document = await documents.GetOrCreateMutableAsync();
        document.Entries[menuId] = new CrestMenuPlacementEntry { Placement = placement, Enabled = enabled };
        await documents.UpdateAsync(document);
    }

    public async ValueTask RemoveAsync(string menuId)
    {
        var document = await documents.GetOrCreateMutableAsync();
        if (document.Entries.Remove(menuId))
        {
            await documents.UpdateAsync(document);
        }
    }
}
