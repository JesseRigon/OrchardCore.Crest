using Crest.Components.Primitives;

namespace Crest.Components.Primitives;

/// <summary>
/// Represents the common <see cref="CrestChipList{TValue}" /> API used by
/// chip list items. Injected as a cascading property in <see cref="CrestChipItem" />.
/// </summary>
public interface IRadzenChipList
{
    /// <summary>
    /// Adds the specified item to the chip list.
    /// </summary>
    /// <param name="item">The item to add.</param>
    void AddItem(CrestChipItem item);

    /// <summary>
    /// Removes the specified item from the chip list.
    /// </summary>
    /// <param name="item">The item.</param>
    void RemoveItem(CrestChipItem item);

    /// <summary>
    /// Refreshes this instance.
    /// </summary>
    void Refresh();
}
