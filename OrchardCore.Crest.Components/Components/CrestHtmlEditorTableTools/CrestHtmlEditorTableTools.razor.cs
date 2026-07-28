using Microsoft.AspNetCore.Components;

namespace Crest.Components.Primitives
{
    /// <summary>
    /// A built-in HTML editor toolbar group for table insertion and table commands.
    /// </summary>
    public partial class CrestHtmlEditorTableTools : CrestHtmlEditorButtonBase
    {
        /// <summary>
        /// Gets or sets localizable strings used by the table tools. Falls back to <see cref="CrestHtmlEditor.TableStrings" />.
        /// </summary>
        [Parameter]
        public HtmlEditorTableStrings? TableStrings { get; set; }
    }
}
