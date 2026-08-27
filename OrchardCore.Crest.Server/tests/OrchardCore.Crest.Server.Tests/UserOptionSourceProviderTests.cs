using System.Text.Json.Nodes;
using Crest.Services;
using OrchardCore.Users.Models;
using Xunit;

namespace Crest.Server.Tests;

// ToRow is the multi-column projection that stock UserPickerField cannot do, so it is
// worth pinning directly.
public class UserOptionSourceProviderColumnTests
{
    private static Dictionary<string, string?> Project(User user, params string[] columns) =>
        new(UserOptionSourceProvider.ToRow(user, columns).Values, StringComparer.OrdinalIgnoreCase);

    private static OptionRow ProjectRow(User user, params string[] columns) =>
        UserOptionSourceProvider.ToRow(user, columns);

    private static User Sample() => new()
    {
        UserId = "user-1",
        UserName = "ada",
        Email = "ada@example.com",
        PhoneNumber = "555-0100",
        IsEnabled = true,
        Properties = new JsonObject
        {
            ["Badge"] = "700",
            ["UserProfile"] = new JsonObject { ["Department"] = "Engineering" },
        },
    };

    [Fact]
    public void Projects_several_columns_of_one_record()
    {
        // The requirement that motivated this provider: a dropdown showing the user's
        // name AND badge number, not just the name.
        var values = Project(Sample(), "UserName", "Property:Badge");

        Assert.Equal("ada", values["UserName"]);
        Assert.Equal("700", values["Property:Badge"]);
    }

    [Fact]
    public void Reads_a_nested_property_path()
    {
        var values = Project(Sample(), "Property:UserProfile.Department");

        Assert.Equal("Engineering", values["Property:UserProfile.Department"]);
    }

    [Fact]
    public void A_missing_property_is_null_rather_than_an_error()
    {
        var values = Project(Sample(), "Property:Absent", "Property:UserProfile.Absent");

        Assert.Null(values["Property:Absent"]);
        Assert.Null(values["Property:UserProfile.Absent"]);
    }

    [Fact]
    public void Projects_the_built_in_columns()
    {
        var values = Project(Sample(), "Email", "PhoneNumber", "IsEnabled");

        Assert.Equal("ada@example.com", values["Email"]);
        Assert.Equal("555-0100", values["PhoneNumber"]);
        Assert.Equal("true", values["IsEnabled"]);
    }

    [Fact]
    public void Falls_back_to_the_default_column_when_none_are_requested()
    {
        var values = Project(Sample());

        Assert.Equal("ada", values["UserName"]);
    }

    [Fact]
    public void An_unknown_column_yields_null_rather_than_throwing()
    {
        var values = Project(Sample(), "NotAColumn");

        Assert.Null(values["NotAColumn"]);
    }

    [Fact]
    public void A_users_identity_is_its_id_so_no_separate_key_is_reported()
    {
        // The index stores a null SelectedKey for entity sources; this is where that
        // null originates.
        var row = ProjectRow(Sample(), "UserName");

        Assert.Equal("user-1", row.Id);
        Assert.Null(row.Key);
    }
}
