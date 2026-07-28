using Microsoft.AspNetCore.Components;

namespace Crest.Components.Primitives
{
    /// <summary>
    /// Adds a custom color to <see cref="CrestHtmlEditorBackground" />.
    /// </summary>
    /// <example>
    /// <code>
    ///  &lt;CrestHtmlEditorBackground &gt;
    ///     &lt;CrestHtmlEditorBackgroundItem Value="red" /&gt;
    ///     &lt;CrestHtmlEditorBackgroundItem Value="green" /&gt;
    ///  &lt;/CrestHtmlEditorBackground &gt;
    /// </code>
    /// </example>
    public partial class CrestHtmlEditorBackgroundItem
    {
        /// <summary>
        /// The custom color to add.
        /// </summary>
        [Parameter]
        public string? Value { get; set; }
    }
}
