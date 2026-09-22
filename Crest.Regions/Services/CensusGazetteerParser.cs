using System.Globalization;
using Crest.Regions.Models;

namespace Crest.Regions.Services;

/// <summary>
/// Tree population tooling (plans/regions-and-locations.md phase 6): reads a US Census
/// Bureau Gazetteer file - tab separated, header row of USPS, GEOID, ANSICODE, NAME,
/// ..., INTPTLAT, INTPTLONG - and streams one standard node per row. County files give
/// level 3 under the state; the node id is "US-{state}-{county FIPS}" so it survives data
/// versions, the code is the full GEOID. Public-domain data; the raw file ships embedded.
/// </summary>
public static class CensusGazetteerParser
{
    public const string Country = "US";

    public static IEnumerable<GeoNode> ReadCounties(TextReader reader) => Read(reader, level: 3);

    public static IEnumerable<GeoNode> Read(TextReader reader, int level)
    {
        var header = reader.ReadLine();
        if (header is null)
        {
            yield break;
        }

        var columns = header.Split('\t').Select(column => column.Trim()).ToArray();
        var state = Array.IndexOf(columns, "USPS");
        var geoId = Array.IndexOf(columns, "GEOID");
        var name = Array.IndexOf(columns, "NAME");
        var latitude = Array.IndexOf(columns, "INTPTLAT");
        var longitude = Array.IndexOf(columns, "INTPTLONG");
        if (state < 0 || geoId < 0 || name < 0)
        {
            throw new InvalidDataException("Not a Census Gazetteer file: USPS, GEOID and NAME columns are required.");
        }

        while (reader.ReadLine() is { } line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var cells = line.Split('\t');
            var stateCode = cells[state].Trim();
            var id = cells[geoId].Trim();
            var stateNodeId = $"{Country}-{stateCode}";
            // A county GEOID is state FIPS (2) + county FIPS (3); the id keeps only the
            // county part since the state is already in the path.
            var local = id.Length > 2 ? id[2..] : id;

            yield return new GeoNode
            {
                NodeId = $"{stateNodeId}-{local}",
                Country = Country,
                Level = level,
                Kind = GeoNodeKinds.Level,
                Name = cells[name].Trim(),
                Code = id,
                ParentIds = [stateNodeId],
                Latitude = Parse(cells, latitude),
                Longitude = Parse(cells, longitude),
            };
        }
    }

    private static double? Parse(string[] cells, int index) =>
        index >= 0 && index < cells.Length && double.TryParse(cells[index].Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : null;
}
