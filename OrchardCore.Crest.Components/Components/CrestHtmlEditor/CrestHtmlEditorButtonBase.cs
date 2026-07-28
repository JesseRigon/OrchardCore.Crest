using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Rendering;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using Crest.Components.Primitives;

namespace Crest.Components.Primitives
{
    /// <summary>
    /// Base class that CrestHtmlEditor color picker tools inherit from.
    /// </summary>
    public abstract class CrestHtmlEditorButtonBase : ComponentBase, IDisposable
    {
        [Inject]
        private IServiceProvider Services { get; set; } = default!;

        private Localizer? localizer;

        internal Localizer Localizer => localizer ??=
            Services.GetService<Localizer>() ?? Localizer.Default;

        /// <summary>
        /// Gets a localized string for the specified resource key.
        /// </summary>
        public string Localize(string key) => Localizer.Get(key, Editor?.UICulture ?? CultureInfo.CurrentUICulture);

        /// <summary>
        /// The CrestHtmlEditor component which this tool is part of.
        /// </summary>
        [CascadingParameter]
        public CrestHtmlEditor? Editor { get; set; }

        /// <summary>
        /// Specifies the name of the command. It is available as <see cref="HtmlEditorExecuteEventArgs.CommandName" /> when
        /// <see cref="CrestHtmlEditor.Execute" /> is raised.
        /// </summary>
        protected virtual string? CommandName { get; }

        /// <summary>
        /// Specifies the shortcut for the command. Can be in the form of <c>"Ctrl+X"</c> or <c>"Alt+Shift+Z"</c>.
        /// </summary>
        [Parameter]
        public virtual string? Shortcut { get; set; }

        /// <summary>
        /// Handles the click event of the button. Executes the command.
        /// </summary>
        protected virtual async Task OnClick()
        {
            if (Editor != null && CommandName != null)
            {
                await Editor.ExecuteCommandAsync(CommandName);
            }
        }

        /// <inheritdoc />
        protected override void OnInitialized()
        {
            if (!string.IsNullOrEmpty(Shortcut))
            {
                Editor?.RegisterShortcut(Shortcut, OnClick);
            }
        }

        /// <summary>
        /// IDisposable implementation.
        /// </summary>
        public void Dispose()
        {
            if (!string.IsNullOrEmpty(Shortcut))
            {
                Editor?.UnregisterShortcut(Shortcut);
            }

            GC.SuppressFinalize(this);
        }
    }
}
