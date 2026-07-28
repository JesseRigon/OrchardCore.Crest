using Microsoft.AspNetCore.Components;

namespace Crest.Components.Primitives
{
    /// <summary>
    /// A CrestHtmlEditor tool which sets the text color of the selection.
    /// </summary>
    /// <example>
    /// <code>
    /// &lt;CrestHtmlEditor @bind-Value=@html&gt;
    ///  &lt;CrestHtmlEditorColor /&gt;
    /// &lt;/CrestHtmlEdito&gt;
    /// @code {
    ///   string html = "@lt;strong&gt;Hello&lt;/strong&gt; world!"; 
    /// }
    /// </code>
    /// </example>
    public partial class CrestHtmlEditorColor : CrestHtmlEditorColorBase
    {
        /// <inheritdoc />
        protected override string CommandName => "foreColor";

        /// <summary>
        /// Specifies the default text color. Set to <c>"rgb(255, 0, 0)"</c> by default;
        /// </summary>
        [Parameter]
        public override string Value { get; set; } = "rgb(255, 0, 0)";
        private string? title;

        /// <summary>
        /// Specifies the title (tooltip) displayed when the user hovers the tool. Set to <c>"Text color"</c> by default.
        /// </summary>
        [Parameter]
        public string Title { get => title ?? Localize(nameof(CrestStrings.HtmlEditorColor_Title)); set => title = value; }
    }
}
