using Microsoft.AspNetCore.Components;

namespace Crest.Components.Primitives
{
    /// <summary>
    /// A CrestHtmlEditor tool which indents the selection.
    /// </summary>
    /// <example>
    /// <code>
    /// &lt;CrestHtmlEditor @bind-Value=@html&gt;
    ///  &lt;CrestHtmlEditorIndent /&gt;
    /// &lt;/CrestHtmlEdito&gt;
    /// @code {
    ///   string html = "@lt;strong&gt;Hello&lt;/strong&gt; world!"; 
    /// }
    /// </code>
    /// </example>
    public partial class CrestHtmlEditorIndent : CrestHtmlEditorButtonBase
    {
        /// <inheritdoc />
        protected override string CommandName => "indent";

        private string? title;

        /// <summary>
        /// Specifies the title (tooltip) displayed when the user hovers the tool. Set to <c>"Indent"</c> by default.
        /// </summary>
        [Parameter]
        public string Title { get => title ?? Localize(nameof(CrestStrings.HtmlEditorIndent_Title)); set => title = value; }
    }
}
