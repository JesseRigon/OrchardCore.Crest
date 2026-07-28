using Crest.Components.Primitives;

namespace Crest.Components.Primitives;

/// <summary>
/// Represents the common <see cref="CrestSelectBar{TValue}" /> API used by
/// its items. Injected as a cascading property in <see cref="CrestSelectBarItem" />.
/// </summary>
public interface ICrestSelectBar
{
    /// <summary>
    /// Adds the specified item to the select bar.
    /// </summary>
    /// <param name="item">The item to add.</param>
    void AddItem(CrestSelectBarItem item);

    /// <summary>
    /// Removes the specified item from the select bar.
    /// </summary>
    /// <param name="item">The item.</param>
    void RemoveItem(CrestSelectBarItem item);

    /// <summary>
    /// Refreshes this instance.
    /// </summary>
    void Refresh();
}

