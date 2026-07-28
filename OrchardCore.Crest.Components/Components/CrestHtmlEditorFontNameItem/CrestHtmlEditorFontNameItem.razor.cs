using Microsoft.AspNetCore.Components;

namespace Crest.Components.Primitives
{
    /// <summary>
    /// Adds a custom font to a <see cref="CrestHtmlEditorFontName" />.
    /// </summary>
    /// <example>
    /// <code>
    ///  &lt;CrestHtmlEditorFontName&gt;
    ///  &lt;CrestHtmlEditorFontNameItem Text="Times New Roman" Value='"Times New Roman"' /&gt;
    ///  &lt;/CrestHtmlEditorFontName&gt;
    /// </code>
    /// </example>
    public partial class CrestHtmlEditorFontNameItem
    {
        /// <summary>
        /// The name of the font e.g. <c>"Times New Roman"</c>.
        /// </summary>
        [Parameter]
        public string? Text { get; set; }

        /// <summary>
        /// The CSS value of the font. Use quotes if it contains spaces.
        /// </summary>
        [Parameter]
        public string? Value { get; set; }

        /// <summary>
        /// The CrestHtmlEditorFontName tool which this tool belongs to.
        /// </summary>
        [CascadingParameter]
        public CrestHtmlEditorFontName? Parent { get; set; }

        /// <inheritdoc />
        protected override void OnInitialized()
        {
            Parent?.AddFont(this);
        }
    }
}
