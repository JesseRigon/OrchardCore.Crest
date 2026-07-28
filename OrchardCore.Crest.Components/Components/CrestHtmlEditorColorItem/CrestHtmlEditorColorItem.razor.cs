using Microsoft.AspNetCore.Components;

namespace Crest.Components.Primitives
{
    /// <summary>
    /// Adds a custom color to <see cref="CrestHtmlEditorColor" />.
    /// </summary>
    /// <example>
    /// <code>
    ///  &lt;CrestHtmlEditorColor &gt;
    ///     &lt;CrestHtmlEditorColorItem Value="red" /&gt;
    ///     &lt;CrestHtmlEditorColorItem Value="green" /&gt;
    ///  &lt;/CrestHtmlEditorColor &gt;
    /// </code>
    /// </example>
    public partial class CrestHtmlEditorColorItem
    {
        /// <summary>
        /// The custom color to add.
        /// </summary>
        [Parameter]
        public string? Value { get; set; }
    }
}
