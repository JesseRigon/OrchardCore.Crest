using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;

namespace Crest.Components.Primitives
{
    /// <summary>
    /// A CrestHtmlEditor tool which sets the background color of the selection.
    /// </summary>
    /// <example>
    /// <code>
    /// &lt;CrestHtmlEditor @bind-Value=@html&gt;
    ///  &lt;CrestHtmlEditorBackground /&gt;
    /// &lt;/CrestHtmlEdito&gt;
    /// @code {
    ///   string html = "@lt;strong&gt;Hello&lt;/strong&gt; world!"; 
    /// }
    /// </code>
    /// </example>
    public partial class CrestHtmlEditorBackground : CrestHtmlEditorColorBase
    {
        /// <inheritdoc />
        protected override string CommandName => "backColor";

        /// <summary>
        /// Specifies the default background color. Set to <c>"rgb(0, 0, 255)"</c> by default;
        /// </summary>
        [Parameter]
        public override string Value { get; set; } = "rgb(0, 0, 255)";
        private string? title;

        /// <summary>
        /// Specifies the title (tooltip) displayed when the user hovers the tool. Set to <c>"Background color"</c> by default.
        /// </summary>
        [Parameter]
        public string Title { get => title ?? Localize(nameof(CrestStrings.HtmlEditorBackground_Title)); set => title = value; }
    }
}
