using Microsoft.AspNetCore.Components;
using System.Linq;
using Crest.Components.Primitives.Rendering;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.JSInterop;
using System.Threading;
using System.Runtime.ExceptionServices;
using System;
using System.Globalization;

namespace Crest.Components.Primitives
{
    /// <summary>
    /// CrestBody component.
    /// </summary>
    public partial class CrestBody : CrestComponentWithChildren
    {
        private const string DefaultStyle = "margin-top: 51px; margin-bottom: 57px; margin-left:250px;";

        /// <summary>
        /// Gets or sets the style.
        /// </summary>
        /// <value>The style.</value>
        [Parameter]
        public override string? Style { get; set; } = DefaultStyle;

        /// <inheritdoc />
        protected override string GetComponentCssClass() => ClassList.Create("rz-body")
                                                                     .Add("rz-body-expanded", Expanded)
                                                                     .ToString();

        /// <summary>
        /// Toggles this instance width and left margin.
        /// </summary>
        public void Toggle()
        {
            Expanded = !Expanded;

            StateHasChanged();
        }

        /// <summary>
        /// The <see cref="CrestLayout" /> this component is nested in.
        /// </summary>
        [CascadingParameter]
        public CrestLayout? Layout { get; set; }

        /// <summary>
        /// Gets the style.
        /// </summary>
        /// <returns>System.String.</returns>
        protected string GetStyle()
        {
            if (Layout == null)
            {
                var marginLeft = 250;
                var style = Style;

                if (!string.IsNullOrEmpty(Style))
                {
                    var marginLeftStyle = Style.Split(';').Where(i => i.Split(':')[0].Contains("margin-left", StringComparison.OrdinalIgnoreCase)).FirstOrDefault();
                    if (!string.IsNullOrEmpty(marginLeftStyle) && marginLeftStyle.Contains("px", StringComparison.OrdinalIgnoreCase))
                    {
                        marginLeft = int.Parse(marginLeftStyle.Split(':')[1].Trim().Replace("px", "", StringComparison.OrdinalIgnoreCase).Split('.')[0].Trim(), CultureInfo.InvariantCulture);
                    }
                }

                return $"{Style}; margin-left: {(Expanded ? 0 : marginLeft)}px";
            }
            else
            {
                var style = Style;

                if (!string.IsNullOrEmpty(style))
                {
                    style = style.Replace(DefaultStyle, "", StringComparison.Ordinal);
                }

                return $"{style}";
            }
        }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="CrestBody"/> is expanded.
        /// </summary>
        /// <value><c>true</c> if expanded; otherwise, <c>false</c>.</value>
        [Parameter]
        public bool Expanded { get; set; } = false;

        /// <summary>
        /// Gets or sets a callback raised when the component is expanded or collapsed.
        /// </summary>
        /// <value>The expanded changed callback.</value>
        [Parameter]
        public EventCallback<bool> ExpandedChanged { get; set; }

        [Inject]
        NavigationManager? NavigationManager { get; set; }

        /// <inheritdoc />
        protected override Task OnInitializedAsync()
        {
            if (NavigationManager != null)
            {
                NavigationManager.LocationChanged += OnLocationChanged;
            }

            return base.OnInitializedAsync();
        }

        private void OnLocationChanged(object? sender, LocationChangedEventArgs e)
        {
            if (IsJSRuntimeAvailable && Layout != null && JSRuntime != null)
            {
                var uri = new Uri(e.Location);

                if (string.IsNullOrEmpty(uri.Fragment))
                {
                    JSRuntime.InvokeVoid("Crest.Components.Primitives.scrollToTop", GetId());
                }
            }
        }

        /// <inheritdoc />
        public override void Dispose()
        {
            if (NavigationManager != null)
            {
                NavigationManager.LocationChanged -= OnLocationChanged;
            }

            base.Dispose();

            GC.SuppressFinalize(this);
        }
    }
}