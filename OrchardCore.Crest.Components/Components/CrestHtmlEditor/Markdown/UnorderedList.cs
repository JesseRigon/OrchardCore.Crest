using System;

namespace Crest.Components.Primitives.Documents.Markdown;

/// <summary>
/// Represents an unordered list: <c>- item</c>.
/// </summary>
public class UnorderedList : List
{
    /// <inheritdoc />
    public override void Accept(INodeVisitor visitor)
    {
        ArgumentNullException.ThrowIfNull(visitor);
        visitor.VisitUnorderedList(this);
    }
}