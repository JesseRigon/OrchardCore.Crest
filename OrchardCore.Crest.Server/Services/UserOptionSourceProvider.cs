using OrchardCore.ContentManagement;
using OrchardCore.Users.Indexes;
using OrchardCore.Users.Models;
using YesSql;

namespace Crest.Services;

/// <summary>
/// Exposes users as a picker source, with as many columns as the dropdown asks for.
/// </summary>
/// <remarks>
/// This is what stock <c>UserPickerField</c> cannot do: it renders the user name and
/// nothing else, so "show the rep's name AND badge number" needs a source that can
/// project several columns of the same record. UserPickerField keeps its place for
/// plain user references - this covers the cases that need more.
///
/// Custom per-user values (a badge number, a department) live in the user's own
/// content properties, which is why <see cref="Columns.PropertyPrefix"/> exists as a
/// prefix: "Property:Badge" reads Badge from the user's content, so tenants can
/// display whatever their user profile carries without this provider knowing about
/// it.
/// </remarks>
public sealed class UserOptionSourceProvider(ISession session) : IOptionSourceProvider
{
    /// <summary>Source key for users; the qualifier is unused.</summary>
    public const string ProviderKey = "users";

    public string Key => ProviderKey;

    public static class Columns
    {
        public const string UserName = "UserName";
        public const string Email = "Email";
        public const string PhoneNumber = "PhoneNumber";
        public const string IsEnabled = "IsEnabled";

        /// <summary>Prefix for values read from the user's content properties, e.g.
        /// "Property:Badge" or "Property:UserProfile.Department".</summary>
        public const string PropertyPrefix = "Property:";
    }

    public Task<IReadOnlyList<OptionSourceColumnDescriptor>> DescribeColumnsAsync(string qualifier, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<OptionSourceColumnDescriptor>>(
        [
            new(Columns.UserName, "User name", IsDefaultDisplay: true),
            new(Columns.Email, "Email"),
            new(Columns.PhoneNumber, "Phone number"),
            new(Columns.IsEnabled, "Enabled"),
        ]);

    public async Task<IReadOnlyList<OptionRow>> QueryAsync(OptionSourceQuery query, CancellationToken cancellationToken = default)
    {
        var users = await session.Query<User, UserIndex>().ListAsync();
        var rows = users.Select(user => ToRow(user, query.Columns));

        rows = OptionFilterMatcher.Apply(rows, query.Filters);
        rows = OptionFilterMatcher.Search(rows, query.SearchText, query.SearchColumns is { Count: > 0 }
            ? query.SearchColumns
            : [Columns.UserName, Columns.Email]);

        return [.. rows
            .OrderBy(row => row.Values.TryGetValue(Columns.UserName, out var name) ? name : row.Id, StringComparer.OrdinalIgnoreCase)
            .Skip(query.Skip)
            .Take(query.Take)];
    }

    public async Task<IReadOnlyList<OptionRow>> GetByIdsAsync(string qualifier, IReadOnlyList<string> ids, IReadOnlyList<string> columns, CancellationToken cancellationToken = default)
    {
        if (ids.Count == 0)
        {
            return [];
        }

        // One query for the whole id set - rendering a document full of user
        // references must not become one lookup per line. (Filtering happens after
        // materializing, the same way CrestUsersController queries UserIndex.)
        var wanted = new HashSet<string>(ids, StringComparer.OrdinalIgnoreCase);
        var users = await session.Query<User, UserIndex>().ListAsync();

        return [.. users.Where(user => wanted.Contains(user.UserId)).Select(user => ToRow(user, columns))];
    }

    // Internal so the multi-column projection can be unit-tested directly - it is the
    // behaviour that justifies this provider existing.
    internal static OptionRow ToRow(User user, IReadOnlyList<string> columns)
    {
        var requested = columns is { Count: > 0 } ? columns : [Columns.UserName];
        var values = new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);

        foreach (var column in requested)
        {
            values[column] = column switch
            {
                Columns.UserName => user.UserName,
                Columns.Email => user.Email,
                Columns.PhoneNumber => user.PhoneNumber,
                Columns.IsEnabled => user.IsEnabled ? "true" : "false",
                _ when column.StartsWith(Columns.PropertyPrefix, StringComparison.OrdinalIgnoreCase) =>
                    ReadProperty(user, column[Columns.PropertyPrefix.Length..]),
                _ => null,
            };
        }

        // A user's identity IS its id, so Key stays null - which is exactly what the
        // index records for entity sources.
        return new OptionRow(user.UserId, values);
    }

    // Walks a dotted path through the user's content JSON, so a tenant can display
    // any value their user profile carries.
    private static string? ReadProperty(User user, string path)
    {
        System.Text.Json.Nodes.JsonNode? node = user.Properties;
        foreach (var segment in path.Split('.', StringSplitOptions.RemoveEmptyEntries))
        {
            if (node is null)
            {
                return null;
            }

            node = node[segment];
        }

        return node?.ToString();
    }
}
