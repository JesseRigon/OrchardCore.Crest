using Crest.Regions.Services;
using Xunit;

namespace Crest.Regions.Tests;

public class CensusGazetteerParserTests
{
    [Fact]
    public void The_shipped_county_gazetteer_parses_to_level_3_nodes_under_their_state()
    {
        using var reader = new StreamReader(Path.Combine(AppContext.BaseDirectory, "Data", "us-counties-gazetteer-2024.txt"));
        var nodes = CensusGazetteerParser.ReadCounties(reader).ToList();

        Assert.True(nodes.Count > 3000, $"Expected every US county, got {nodes.Count}.");
        var cook = Assert.Single(nodes, node => node.NodeId == "US-IL-031");
        Assert.Equal("Cook County", cook.Name);
        Assert.Equal("17031", cook.Code);
        Assert.Equal(3, cook.Level);
        Assert.Equal(["US-IL"], cook.ParentIds);
        Assert.NotNull(cook.Latitude);
        Assert.All(nodes, node => Assert.Single(node.ParentIds));
        Assert.Equal(nodes.Count, nodes.Select(node => node.NodeId).Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public void A_file_without_the_required_columns_is_refused()
    {
        using var reader = new StringReader("A\tB\n1\t2\n");
        Assert.Throws<InvalidDataException>(() => CensusGazetteerParser.ReadCounties(reader).ToList());
    }
}
