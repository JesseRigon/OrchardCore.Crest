using Crest.Services;
using Xunit;

namespace Crest.Server.Tests;

// Data scope is authorization, not configuration. These tests pin the two properties
// that make it authorization: it FAILS CLOSED, and it cannot be bypassed by asking a
// different way (resolve-by-id rather than query).
public class OptionSourceScopeTests
{
    [Fact]
    public void Nothing_viewable_is_empty_rather_than_unrestricted()
    {
        // The distinction the whole design rests on. If "may see nothing" were treated
        // as "no restriction to apply", a user entitled to nothing would receive
        // everything - the exact inversion this guards against.
        Assert.True(OptionSourceScope.Nothing.IsEmpty);
        Assert.False(OptionSourceScope.Nothing.IsUnrestricted);
    }

    [Fact]
    public void Unrestricted_is_not_empty()
    {
        Assert.False(OptionSourceScope.Unrestricted.IsEmpty);
        Assert.True(OptionSourceScope.Unrestricted.IsUnrestricted);
    }

    [Fact]
    public void View_any_allows_every_item_of_that_type_whoever_owns_it()
    {
        var scope = new OptionSourceScope(["Customer"], [], "user-1");

        Assert.True(scope.Allows("Customer", owner: "someone-else"));
        Assert.True(scope.Allows("Customer", owner: null));
    }

    [Fact]
    public void View_own_allows_only_the_users_own_items()
    {
        var scope = new OptionSourceScope([], ["Customer"], "user-1");

        Assert.True(scope.Allows("Customer", owner: "user-1"));
        Assert.False(scope.Allows("Customer", owner: "user-2"));
    }

    [Fact]
    public void View_own_with_no_user_allows_nothing()
    {
        // An unauthenticated principal must not match items with a null owner by
        // accident.
        var scope = new OptionSourceScope([], ["Customer"], UserId: null);

        Assert.False(scope.Allows("Customer", owner: null));
    }

    [Fact]
    public void A_type_outside_the_scope_is_never_allowed()
    {
        var scope = new OptionSourceScope(["Customer"], ["Invoice"], "user-1");

        Assert.False(scope.Allows("Vendor", owner: "user-1"));
    }

    [Fact]
    public void Nothing_allows_nothing_at_all()
    {
        Assert.False(OptionSourceScope.Nothing.Allows("Customer", owner: "user-1"));
        Assert.False(OptionSourceScope.Nothing.Allows("Customer", owner: null));
    }

    [Fact]
    public void Unrestricted_allows_everything()
    {
        // Option lists are tenant CONFIGURATION rather than records, so they carry no
        // per-item authorization.
        Assert.True(OptionSourceScope.Unrestricted.Allows("Anything", owner: "whoever"));
    }
}

// Resolve is the bypass route: a user who cannot QUERY other people's records could
// otherwise enumerate them by feeding ids to resolve.
//
// An earlier pass returned a REDACTED placeholder row for out-of-scope ids. That was
// replaced: an out-of-scope record is ABSENT from the result, and the referencing
// item is then out of scope too (OptionSourceReferenceGuard). A placeholder still
// asserts "this reference exists and resolves", which a caller can act on - totalling
// a list, counting matches - as though the record were merely unnamed. Missing data
// is honest; fabricated data is not.
public class ResolveExclusionTests
{
    [Fact]
    public void There_is_no_redacted_row_shape_to_leak_through()
    {
        // Regression guard for the removal: OptionRow must carry no "redacted" flag,
        // because any such flag invites returning a row that should not exist.
        Assert.Null(typeof(OptionRow).GetProperty("IsRedacted"));
        Assert.Null(typeof(ContentItemOptionSourceProvider).GetMethod(
            "Redacted",
            System.Reflection.BindingFlags.Static
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Public));
    }

    [Fact]
    public void An_in_scope_item_projects_normally()
    {
        var item = new OrchardCore.ContentManagement.ContentItem
        {
            ContentItemId = "item-1",
            ContentType = "Customer",
            DisplayText = "Acme",
        };

        var row = ContentItemOptionSourceProvider.ToRow(item, []);

        Assert.Equal("item-1", row.Id);
        Assert.Equal("Acme", row.Values["DisplayText"]);
    }
}
