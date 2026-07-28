using Microsoft.AspNetCore.Components;

namespace Crest.Components.Primitives
{
    /// <summary>
    /// A CrestHtmlEditor tool which bolds the selection.
    /// </summary>
    /// <example>
    /// <code>
    /// &lt;CrestHtmlEditor @bind-Value=@html&gt;
    ///  &lt;CrestHtmlEditorBold /&gt;
    /// &lt;/CrestHtmlEdito&gt;
    /// @code {
    ///   string html = "@lt;strong&gt;Hello&lt;/strong&gt; world!";
    /// }
    /// </code>
    /// </example>
    public partial class CrestHtmlEditorBold : CrestHtmlEditorButtonBase
    {
        /// <inheritdoc />
        protected override string CommandName => "bold";

        private string? title;

        /// <summary>
        /// Specifies the title (tooltip) displayed when the user hovers the tool. Set to <c>"Bold"</c> by default.
        /// </summary>
        [Parameter]
        public string Title { get => title ?? Localize(nameof(CrestStrings.HtmlEditorBold_Title)); set => title = value; }

        /// <summary>
        /// Specifies the shortcut for the command. Set to <c>"Ctrl+B"</c> by default.
        /// </summary>
        [Parameter]
        public override string? Shortcut { get; set; } = "Ctrl+B";
    }
}
