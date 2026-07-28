using Microsoft.AspNetCore.Components;

namespace Crest.Components.Primitives
{
    /// <summary>
    /// A CrestHtmlEditor tool which makes the selection italic.
    /// </summary>
    /// <example>
    /// <code>
    /// &lt;CrestHtmlEditor @bind-Value=@html&gt;
    ///  &lt;CrestHtmlEditorItalic /&gt;
    /// &lt;/CrestHtmlEdito&gt;
    /// @code {
    ///   string html = "@lt;strong&gt;Hello&lt;/strong&gt; world!";
    /// }
    /// </code>
    /// </example>
    public partial class CrestHtmlEditorItalic : CrestHtmlEditorButtonBase
    {
        /// <inheritdoc />
        protected override string CommandName => "italic";

        private string? title;

        /// <summary>
        /// Specifies the title (tooltip) displayed when the user hovers the tool. Set to <c>"Italic"</c> by default.
        /// </summary>
        [Parameter]
        public string Title { get => title ?? Localize(nameof(CrestStrings.HtmlEditorItalic_Title)); set => title = value; }

        /// <summary>
        /// Specifies the shortcut for the command. Set to <c>"Ctrl+I"</c> by default.
        /// </summary>
        [Parameter]
        public override string? Shortcut { get; set; } = "Ctrl+I";
    }
}
