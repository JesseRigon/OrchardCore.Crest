using Crest.Global;
using Crest.Regions.Indexes;
using Crest.Regions.Models;
using Crest.Regions.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OrchardCore.Environment.Shell;
using YesSql;

namespace Crest.Regions.Controllers;

/// <summary>
/// Tree population tooling, Default tenant only (plans/regions-and-locations.md ›
/// Populating the tree): upserts standard nodes, postal codes and boundaries into the
/// global store from the same JSON shape the embedded data files use. The cycle check
/// runs over the whole submitted batch before anything is written.
/// </summary>
[ApiController]
[AutoValidateAntiforgeryToken]
[Route(RegionsConstants.Routes.Api + "/geo/import")]
public sealed class GeoImportController(ICrestGlobalStore globalStore, ShellSettings shellSettings, IAuthorizationService authorization) : ControllerBase
{
    [HttpPost]
    [RequestSizeLimit(512 * 1024 * 1024)]
    public async Task<ActionResult<GeoImportResult>> ImportAsync([FromBody] GeoImportBatch batch, CancellationToken cancellationToken)
    {
        if (!shellSettings.IsDefaultShell())
        {
            return NotFound();
        }

        if (!await authorization.AuthorizeAsync(User, CrestGlobalPermissions.ManageGlobalReferenceData))
        {
            return Forbid();
        }

        var result = new GeoImportResult();
        await globalStore.WriteAsync(async (session, ct) =>
        {
            var existing = (await session.Query<GeoNode, GeoNodeIndex>().ListAsync(ct)).ToDictionary(node => node.NodeId, StringComparer.Ordinal);

            // Acyclicity over the merged graph: existing parents plus the batch's, before any write.
            var parents = existing.ToDictionary(entry => entry.Key, entry => (IReadOnlyList<string>)entry.Value.ParentIds, StringComparer.Ordinal);
            foreach (var node in batch.Nodes)
            {
                parents[node.NodeId] = node.ParentIds;
            }

            foreach (var node in batch.Nodes)
            {
                if (ReachesItself(node.NodeId, parents))
                {
                    result.Errors.Add($"Node '{node.NodeId}' would create a cycle through its parents.");
                }
            }

            if (result.Errors.Count > 0)
            {
                throw new InvalidOperationException(string.Join(" ", result.Errors));
            }

            foreach (var incoming in batch.Nodes)
            {
                if (existing.TryGetValue(incoming.NodeId, out var current))
                {
                    current.Country = incoming.Country;
                    current.Level = incoming.Level;
                    current.Kind = incoming.Kind;
                    current.Name = incoming.Name;
                    current.Code = incoming.Code;
                    current.Abbreviation = incoming.Abbreviation;
                    current.ParentIds = incoming.ParentIds;
                    current.GroupIds = incoming.GroupIds;
                    current.Latitude = incoming.Latitude;
                    current.Longitude = incoming.Longitude;
                    await session.SaveAsync(current);
                    result.NodesUpdated++;
                }
                else
                {
                    await session.SaveAsync(incoming);
                    existing[incoming.NodeId] = incoming;
                    result.NodesAdded++;
                }
            }

            foreach (var incoming in batch.PostalCodes)
            {
                var current = await session.Query<GeoPostalCode, GeoPostalCodeIndex>(index => index.Country == incoming.Country && index.Code == incoming.Code).FirstOrDefaultAsync(ct);
                if (current is null)
                {
                    await session.SaveAsync(incoming);
                }
                else
                {
                    current.NodeIds = incoming.NodeIds;
                    current.Latitude = incoming.Latitude;
                    current.Longitude = incoming.Longitude;
                    await session.SaveAsync(current);
                }

                result.PostalCodes++;
            }

            foreach (var incoming in batch.Boundaries)
            {
                var current = await session.Query<GeoBoundary, GeoBoundaryIndex>(index => index.NodeId == incoming.NodeId).FirstOrDefaultAsync(ct);
                if (current is null)
                {
                    await session.SaveAsync(incoming);
                }
                else
                {
                    current.Format = incoming.Format;
                    current.Geometry = incoming.Geometry;
                    await session.SaveAsync(current);
                }

                result.Boundaries++;
            }
        }, cancellationToken);

        return result;
    }

    /// <summary>A Census Gazetteer file (counties at level 3, or another level's file with ?level=) upserted as standard nodes.</summary>
    [HttpPost("gazetteer")]
    [RequestSizeLimit(512 * 1024 * 1024)]
    public async Task<ActionResult<GeoImportResult>> ImportGazetteerAsync(IFormFile file, [FromQuery] int level = 3, CancellationToken cancellationToken = default)
    {
        if (!shellSettings.IsDefaultShell())
        {
            return NotFound();
        }

        if (!await authorization.AuthorizeAsync(User, CrestGlobalPermissions.ManageGlobalReferenceData))
        {
            return Forbid();
        }

        var batch = new GeoImportBatch();
        using (var reader = new StreamReader(file.OpenReadStream()))
        {
            batch.Nodes.AddRange(CensusGazetteerParser.Read(reader, level));
        }

        return await ImportAsync(batch, cancellationToken);
    }

    private static bool ReachesItself(string nodeId, IReadOnlyDictionary<string, IReadOnlyList<string>> parents)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var stack = new Stack<string>(parents.TryGetValue(nodeId, out var direct) ? direct : []);
        while (stack.Count > 0)
        {
            var current = stack.Pop();
            if (current == nodeId)
            {
                return true;
            }

            if (seen.Add(current) && parents.TryGetValue(current, out var next))
            {
                foreach (var parent in next)
                {
                    stack.Push(parent);
                }
            }
        }

        return false;
    }

    public sealed class GeoImportBatch
    {
        public List<GeoNode> Nodes { get; set; } = [];
        public List<GeoPostalCode> PostalCodes { get; set; } = [];
        public List<GeoBoundary> Boundaries { get; set; } = [];
    }

    public sealed class GeoImportResult
    {
        public int NodesAdded { get; set; }
        public int NodesUpdated { get; set; }
        public int PostalCodes { get; set; }
        public int Boundaries { get; set; }
        public List<string> Errors { get; set; } = [];
    }
}
