using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;

namespace Crest.Components.Primitives
{
    /// <summary>
    /// A tool which changes the font of the selected text.
    /// </summary>
    /// <example>
    /// <code>
    /// &lt;CrestHtmlEditor @bind-Value=@html&gt;
    ///  &lt;CrestHtmlEditorFontName /&gt;
    /// &lt;/CrestHtmlEdito&gt;
    /// @code {
    ///   string html = "@lt;strong&gt;Hello&lt;/strong&gt; world!"; 
    /// }
    /// </code>
    /// </example>
    public partial class CrestHtmlEditorFontName
    {
        [Inject]
        private IServiceProvider Services { get; set; } = default!;

        private Localizer? localizer;
        internal Localizer Localizer => localizer ??= Services.GetService<Localizer>() ?? Localizer.Default;
        string Localize(string key) => Localizer.Get(key, Editor?.UICulture ?? CultureInfo.CurrentUICulture);

        IList<CrestHtmlEditorFontNameItem> fonts = new List<CrestHtmlEditorFontNameItem>();

        internal void AddFont(CrestHtmlEditorFontNameItem font)
        {
            if (!fonts.Contains(font))
            {
                fonts.Add(font);
            }
        }

        /// <summary>
        /// Sets the child content.
        /// </summary>
        [Parameter]
        public RenderFragment? ChildContent { get; set; }

        private string? placeholder;

        /// <summary>
        /// Specifies the placeholder displayed to the user. Set to <c>"Font"</c> by default.
        /// </summary>
        [Parameter]
        public string Placeholder { get => placeholder ?? Localize(nameof(CrestStrings.HtmlEditorFontName_Placeholder)); set => placeholder = value; }

        private string? title;

        /// <summary>
        /// Specifies the title (tooltip) displayed when the user hovers the tool. Set to <c>"Font name"</c> by default.
        /// </summary>
        [Parameter]
        public string Title { get => title ?? Localize(nameof(CrestStrings.HtmlEditorFontName_Title)); set => title = value; }

        /// <summary>
        /// The CrestHtmlEditor component which this tool is part of.
        /// </summary>
        [CascadingParameter]
        public CrestHtmlEditor? Editor { get; set; }

        async Task OnChange(string value)
        {
            if (Editor != null)
            {
                await Editor.ExecuteCommandAsync("fontName", value);
            }
        }
    }
}
