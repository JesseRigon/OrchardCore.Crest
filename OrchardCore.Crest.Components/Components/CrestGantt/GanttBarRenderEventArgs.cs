using System.Collections.Generic;

namespace Crest.Components.Primitives
{
    /// <summary>
    /// Supplies per-bar rendering customization in <see cref="CrestGantt{TItem}"/>.
    /// </summary>
    public class GanttBarRenderEventArgs<TItem>
    {
        /// <summary>
        /// The task data item.
        /// </summary>
        public TItem Data { get; set; } = default!;

        /// <summary>
        /// Additional CSS classes to apply to the task bar.
        /// </summary>
        public string CssClass { get; set; } = string.Empty;

        /// <summary>
        /// HTML attributes to add to the bar element (e.g. <c>style</c>).
        /// </summary>
        public IDictionary<string, object> Attributes { get; set; } = new Dictionary<string, object>();
    }
}
