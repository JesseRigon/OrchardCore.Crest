using Crest.Indexing;
using Crest.Services;
using Xunit;

namespace Crest.Server.Tests;

// Assignment matching: an item may be assigned to several targets at once, and a
// query asks for any of them or all of them.
public class CrestAssignmentMatchingTests
{
    private static CrestAssignmentIndex Row(string itemId, string kind, string targetId) =>
        new() { ContentItemId = itemId, Kind = kind, TargetId = targetId, Latest = true };

    [Fact]
    public void A_requirement_without_a_kind_matches_any_role()
    {
        var row = Row("item-1", "SalesRep", "user-1");

        Assert.True(CrestAssignmentService.Satisfies(row, new AssignmentRequirement("user-1")));
    }

    [Fact]
    public void A_requirement_with_a_kind_must_match_that_role()
    {
        var row = Row("item-1", "SalesRep", "user-1");

        Assert.True(CrestAssignmentService.Satisfies(row, new AssignmentRequirement("user-1", "SalesRep")));

        // Same target, different role - a technician assignment must not satisfy a
        // sales-rep requirement.
        Assert.False(CrestAssignmentService.Satisfies(row, new AssignmentRequirement("user-1", "Technician")));
    }

    [Fact]
    public void A_different_target_never_matches()
    {
        var row = Row("item-1", "SalesRep", "user-1");

        Assert.False(CrestAssignmentService.Satisfies(row, new AssignmentRequirement("user-2")));
    }

    [Fact]
    public void Target_and_kind_comparisons_ignore_case()
    {
        var row = Row("item-1", "SalesRep", "user-1");

        Assert.True(CrestAssignmentService.Satisfies(row, new AssignmentRequirement("USER-1", "salesrep")));
    }
}

// Scope combination. The distinction that carries the security weight is
// null AssignedIds ("no assignment constraint") vs empty ("constrained, nothing
// matched") - conflating them inverts the restriction.
public class AssignmentScopeTests
{
    [Fact]
    public void No_assignment_constraint_leaves_the_permission_result_alone()
    {
        var scope = new OptionSourceScope(["Customer"], [], "user-1");

        Assert.Null(scope.AssignedIds);
        Assert.False(scope.IsEmpty);
        Assert.True(scope.Allows("Customer", owner: "someone-else", contentItemId: "item-1"));
    }

    [Fact]
    public void An_assignment_constraint_that_matched_nothing_denies_everything()
    {
        // The inversion this guards against: a user with no assignments must see
        // NOTHING, not everything.
        var scope = new OptionSourceScope(["Customer"], [], "user-1") { AssignedIds = [] };

        Assert.True(scope.IsEmpty);
        Assert.False(scope.Allows("Customer", owner: "user-1", contentItemId: "item-1"));
    }

    [Fact]
    public void Assignment_narrows_to_the_assigned_items()
    {
        var scope = new OptionSourceScope(["Customer"], [], "user-1") { AssignedIds = ["item-1"] };

        Assert.True(scope.Allows("Customer", owner: "anyone", contentItemId: "item-1"));
        Assert.False(scope.Allows("Customer", owner: "anyone", contentItemId: "item-2"));
    }

    [Fact]
    public void Assignment_never_widens_what_the_permission_check_granted()
    {
        // Assigned, but the user cannot view this type at all - assignment must not
        // become an alternative route in.
        var scope = OptionSourceScope.Nothing with { AssignedIds = ["item-1"] };

        Assert.False(scope.Allows("Customer", owner: "user-1", contentItemId: "item-1"));
    }

    [Fact]
    public void Assignment_still_requires_ownership_when_only_own_is_granted()
    {
        var scope = new OptionSourceScope([], ["Customer"], "user-1") { AssignedIds = ["item-1"] };

        Assert.True(scope.Allows("Customer", owner: "user-1", contentItemId: "item-1"));

        // Assigned to them, but owned by someone else and they may only view their own.
        Assert.False(scope.Allows("Customer", owner: "user-2", contentItemId: "item-1"));
    }
}

// Transitive scope: an item that references something invisible is itself invisible.
public class OptionSourceReferenceGuardTests
{
    private static Func<string, IReadOnlyList<string>> Graph(Dictionary<string, string[]> edges) =>
        id => edges.TryGetValue(id, out var refs) ? refs : [];

    [Fact]
    public void An_item_with_no_references_survives()
    {
        Assert.True(OptionSourceReferenceGuard.Survives("invoice-1", Graph([]), _ => true));
    }

    [Fact]
    public void An_item_whose_references_are_all_visible_survives()
    {
        var graph = Graph(new() { ["invoice-1"] = ["party-1"] });

        Assert.True(OptionSourceReferenceGuard.Survives("invoice-1", graph, _ => true));
    }

    [Fact]
    public void An_item_referencing_something_invisible_does_not_survive()
    {
        // The rule: the invoice is in violation, not merely missing a party.
        var graph = Graph(new() { ["invoice-1"] = ["party-1"] });

        Assert.False(OptionSourceReferenceGuard.Survives("invoice-1", graph, id => id != "party-1"));
    }

    [Fact]
    public void Invisibility_propagates_through_a_chain()
    {
        // A manager who cannot see the assignee must not see the task that points at
        // them, even at one remove.
        var graph = Graph(new()
        {
            ["task-1"] = ["assignment-1"],
            ["assignment-1"] = ["user-profile-1"],
        });

        Assert.False(OptionSourceReferenceGuard.Survives("task-1", graph, id => id != "user-profile-1"));
    }

    [Fact]
    public void A_cycle_terminates_instead_of_recursing_forever()
    {
        var graph = Graph(new()
        {
            ["a"] = ["b"],
            ["b"] = ["a"],
        });

        Assert.True(OptionSourceReferenceGuard.Survives("a", graph, _ => true));
    }

    [Fact]
    public void A_cycle_containing_something_invisible_still_denies()
    {
        var graph = Graph(new()
        {
            ["a"] = ["b"],
            ["b"] = ["a", "secret"],
        });

        Assert.False(OptionSourceReferenceGuard.Survives("a", graph, id => id != "secret"));
    }

    [Fact]
    public void A_chain_deeper_than_the_budget_denies_rather_than_allows()
    {
        // Fail closed: an answer that cannot be computed within budget is not
        // evidence of permission.
        var edges = new Dictionary<string, string[]>();
        for (var i = 0; i < OptionSourceReferenceGuard.MaxDepth + 3; i++)
        {
            edges[$"n{i}"] = [$"n{i + 1}"];
        }

        Assert.False(OptionSourceReferenceGuard.Survives("n0", Graph(edges), _ => true));
    }

    [Fact]
    public void Filter_keeps_only_the_surviving_items()
    {
        var graph = Graph(new()
        {
            ["invoice-1"] = ["party-1"],
            ["invoice-2"] = ["party-2"],
        });

        var kept = OptionSourceReferenceGuard.Filter(
            new[] { "invoice-1", "invoice-2" },
            id => id,
            graph,
            id => id != "party-2");

        Assert.Equal(["invoice-1"], kept);
    }
}
