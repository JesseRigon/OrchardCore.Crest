using System.Text.Json.Nodes;
using Crest.Services;
using Xunit;

namespace Crest.ContentPartLists.Tests;

// The JSON shape contract for custom data fields on options: values live under
// [PartName][FieldName] in the stock field shapes, the grid and the picker provider
// speak strings, and a blank write CLEARS (mirroring the plural/Value contract).
public class CrestOptionFieldAccessorReadTests
{
    private static JsonObject Document() => new()
    {
        ["Option"] = new JsonObject
        {
            ["Color"] = new JsonObject { ["Text"] = "Red" },
            ["SortWeight"] = new JsonObject { ["Value"] = 12.5m },
            ["Active"] = new JsonObject { ["Value"] = true },
            ["Blank"] = new JsonObject { ["Text"] = "  " },
        },
    };

    [Fact]
    public void Reads_a_text_field() =>
        Assert.Equal("Red", CrestOptionFieldAccessor.ReadValue(Document(), "Option", "Color"));

    [Fact]
    public void Reads_a_numeric_field_through_the_Value_shape() =>
        Assert.Equal("12.5", CrestOptionFieldAccessor.ReadValue(Document(), "Option", "SortWeight"));

    [Fact]
    public void Reads_a_boolean_field_through_the_Value_shape() =>
        Assert.Equal("true", CrestOptionFieldAccessor.ReadValue(Document(), "Option", "Active"));

    [Fact]
    public void Absent_part_field_or_blank_value_reads_as_null()
    {
        Assert.Null(CrestOptionFieldAccessor.ReadValue(Document(), "OtherPart", "Color"));
        Assert.Null(CrestOptionFieldAccessor.ReadValue(Document(), "Option", "Missing"));
        Assert.Null(CrestOptionFieldAccessor.ReadValue(Document(), "Option", "Blank"));
        Assert.Null(CrestOptionFieldAccessor.ReadValue(null, "Option", "Color"));
    }
}

public class CrestOptionFieldAccessorWriteTests
{
    [Fact]
    public void Text_fields_store_under_Text()
    {
        var node = CrestOptionFieldAccessor.ToFieldNode("TextField", "Color", "Red");
        Assert.Equal("Red", node?["Text"]?.GetValue<string>());
    }

    [Fact]
    public void Unknown_field_types_fall_back_to_the_Text_shape()
    {
        var node = CrestOptionFieldAccessor.ToFieldNode("SomeCustomField", "Anything", "x");
        Assert.Equal("x", node?["Text"]?.GetValue<string>());
    }

    [Fact]
    public void Numeric_fields_store_a_typed_number_not_a_string()
    {
        // A stringified number would hand typed readers (index providers, other
        // consumers of the item JSON) a value they cannot use.
        var node = CrestOptionFieldAccessor.ToFieldNode("NumericField", "SortWeight", "12.5");
        Assert.Equal(12.5m, node?["Value"]?.GetValue<decimal>());
    }

    [Fact]
    public void Boolean_fields_store_a_typed_bool()
    {
        var node = CrestOptionFieldAccessor.ToFieldNode("BooleanField", "Active", "true");
        Assert.True(node?["Value"]?.GetValue<bool>());
    }

    [Fact]
    public void A_blank_write_clears_the_field()
    {
        Assert.Null(CrestOptionFieldAccessor.ToFieldNode("TextField", "Color", ""));
        Assert.Null(CrestOptionFieldAccessor.ToFieldNode("TextField", "Color", "   "));
        Assert.Null(CrestOptionFieldAccessor.ToFieldNode("NumericField", "SortWeight", null));
    }

    [Fact]
    public void Unparseable_typed_values_are_refused_not_silently_stored()
    {
        Assert.Throws<InvalidOperationException>(() => CrestOptionFieldAccessor.ToFieldNode("NumericField", "SortWeight", "abc"));
        Assert.Throws<InvalidOperationException>(() => CrestOptionFieldAccessor.ToFieldNode("BooleanField", "Active", "maybe"));
    }

    [Fact]
    public void Values_are_trimmed_before_storing() =>
        Assert.Equal("Red", CrestOptionFieldAccessor.ToFieldNode("TextField", "Color", "  Red  ")?["Text"]?.GetValue<string>());
}
