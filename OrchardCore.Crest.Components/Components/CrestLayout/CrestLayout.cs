using Microsoft.AspNetCore.Components;
using Microsoft.Extensions.DependencyInjection;
using Crest.Components.Primitives.Rendering;
using System;

namespace Crest.Components.Primitives
{
    /// <summary>
    /// A layout container component that defines the overall structure of a Blazor application with header, sidebar, body, and footer sections.
    /// CrestLayout is typically used in MainLayout.razor to create a consistent page structure with optional collapsible sidebar and theme integration.
    /// Works with companion components: CrestHeader, CrestSidebar, CrestBody, and CrestFooter. Automatically integrates with ThemeService to apply theme-specific CSS classes.
    /// All sections are optional and can be used in any combination to create the desired page structure. The sidebar can be configured as collapsible, and the layout adjusts automatically when the sidebar expands or collapses.
    /// </summary>
    /// <example>
    /// Basic layout with all sections:
    /// <code>
    /// &lt;CrestLayout&gt;
    ///     &lt;CrestHeader&gt;
    ///         &lt;h1&gt;My Application&lt;/h1&gt;
    ///     &lt;/CrestHeader&gt;
    ///     &lt;CrestSidebar&gt;
    ///         &lt;CrestPanelMenu&gt;
    ///             @* Navigation menu items *@
    ///         &lt;/CrestPanelMenu&gt;
    ///     &lt;/CrestSidebar&gt;
    ///     &lt;CrestBody&gt;
    ///         @Body
    ///     &lt;/CrestBody&gt;
    ///     &lt;CrestFooter&gt;
    ///         © 2026 My Company
    ///     &lt;/CrestFooter&gt;
    /// &lt;/CrestLayout&gt;
    /// </code>
    /// </example>
    public partial class CrestLayout : CrestComponentWithChildren, IDisposable
    {
        [Inject]
        private IServiceProvider? ServiceProvider { get; set; }

        private ThemeService? themeService;

        /// <inheritdoc />
        protected override void OnInitialized()
        {
            themeService = ServiceProvider?.GetService<ThemeService>();

            if (themeService != null)
            {
                themeService.ThemeChanged += OnThemeChanged;
            }

            base.OnInitialized();
        }

        private void OnThemeChanged()
        {
            StateHasChanged();
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            if (themeService != null)
            {
                themeService.ThemeChanged -= OnThemeChanged;
            }

            base.Dispose();
            GC.SuppressFinalize(this);
        }

        /// <inheritdoc />
        protected override string GetComponentCssClass() => ClassList.Create("rz-layout")
            .Add($"rz-{themeService?.Theme}", themeService != null)
            .ToString();
    }
}