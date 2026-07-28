using Microsoft.AspNetCore.Components;

namespace Crest.Components.Primitives
{
    /// <summary>
    /// A CrestHtmlEditor tool which inserts an ordered list (<c>ol</c>).
    /// </summary>
    /// <example>
    /// <code>
    /// &lt;CrestHtmlEditor @bind-Value=@html&gt;
    ///  &lt;CrestHtmlEditorOrderedList /&gt;
    /// &lt;/CrestHtmlEdito&gt;
    /// @code {
    ///   string html = "@lt;strong&gt;Hello&lt;/strong&gt; world!"; 
    /// }
    /// </code>
    /// </example>
    public partial class CrestHtmlEditorOrderedList : CrestHtmlEditorButtonBase
    {
        /// <inheritdoc />
        protected override string CommandName => "insertOrderedList";

        private string? title;

        /// <summary>
        /// Specifies the title (tooltip) displayed when the user hovers the tool. Set to <c>"Ordered list"</c> by default.
        /// </summary>
        [Parameter]
        public string Title { get => title ?? Localize(nameof(CrestStrings.HtmlEditorOrderedList_Title)); set => title = value; }
    }
}
