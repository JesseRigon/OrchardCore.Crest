using Microsoft.AspNetCore.Components;

namespace Crest.Components.Primitives
{
    /// <summary>
    /// A CrestHtmlEditor tool which removes a link.
    /// </summary>
    /// <example>
    /// <code>
    /// &lt;CrestHtmlEditor @bind-Value=@html&gt;
    ///  &lt;CrestHtmlEditorUnlink /&gt;
    /// &lt;/CrestHtmlEdito&gt;
    /// @code {
    ///   string html = "@lt;strong&gt;Hello&lt;/strong&gt; world!"; 
    /// }
    /// </code>
    /// </example>
    public partial class CrestHtmlEditorUnlink : CrestHtmlEditorButtonBase
    {
        /// <inheritdoc />
        protected override string CommandName => "unlink";


        private string? title;

        /// <summary>
        /// Specifies the title (tooltip) displayed when the user hovers the tool. Set to <c>"Remove link"</c> by default.
        /// </summary>
        [Parameter]
        public string Title { get => title ?? Localize(nameof(CrestStrings.HtmlEditorUnlink_Title)); set => title = value; }
    }
}
