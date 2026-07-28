using Microsoft.AspNetCore.Components;
using System;
using System.Threading.Tasks;

namespace Crest.Components.Primitives
{
    /// <summary>
    /// CrestSelectBarItem component.
    /// </summary>
    public class CrestSelectBarItem : CrestComponent
    {
        /// <summary>
        /// Gets or sets the template.
        /// </summary>
        /// <value>The template.</value>
        [Parameter]
        public RenderFragment<CrestSelectBarItem>? Template { get; set; }

        /// <summary>
        /// Gets or sets the icon.
        /// </summary>
        /// <value>The icon.</value>
        [Parameter]
        public string? Icon { get; set; }

        /// <summary>
        /// Gets or sets the icon color.
        /// </summary>
        /// <value>The icon color.</value>
        [Parameter]
        public string? IconColor { get; set; }

        /// <summary>
        /// Gets or sets the image.
        /// </summary>
        /// <value>The image.</value>
        [Parameter]
        public string? Image { get; set; }

        /// <summary>
        /// Gets or sets the text.
        /// </summary>
        /// <value>The text.</value>
        [Parameter]
        public string? ImageAlternateText { get => imageAlternateText ?? Localize(nameof(CrestStrings.SelectBarItem_ImageAlternateText)); set => imageAlternateText = value; }

        private string? imageAlternateText;

        /// <summary>
        /// Gets or sets the image style.
        /// </summary>
        /// <value>The image style.</value>
        [Parameter]
        public string? ImageStyle { get; set; }

        /// <summary>
        /// Gets or sets the text.
        /// </summary>
        /// <value>The text.</value>
        [Parameter]
        public string? Text { get; set; }

        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        /// <value>The value.</value>
        [Parameter]
        public object? Value { get; set; }

        /// <summary>
        /// Gets or sets the accessible label (<c>aria-label</c>) for the item. Falls back to <see cref="Text"/> when
        /// not set. Use this to name icon-only items that have no visible <see cref="Text"/> (WCAG 4.1.2).
        /// </summary>
        /// <value>The accessible label.</value>
        [Parameter]
        public string? AriaLabel { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this <see cref="CrestSelectBarItem"/> is disabled.
        /// </summary>
        /// <value><c>true</c> if disabled; otherwise, <c>false</c>.</value>
        [Parameter]
        public bool Disabled { get; set; }

        IRadzenSelectBar? selectBar;

        /// <summary>
        /// Gets or sets the select bar.
        /// </summary>
        /// <value>The select bar.</value>
        [CascadingParameter]
        public IRadzenSelectBar? SelectBar
        {
            get
            {
                return selectBar;
            }
            set
            {
                if (selectBar != value)
                {
                    selectBar = value;
                    selectBar?.AddItem(this);
                }
            }
        }

        /// <summary>
        /// Disposes this instance.
        /// </summary>
        public override void Dispose()
        {
            base.Dispose();
            SelectBar?.RemoveItem(this);
            GC.SuppressFinalize(this);
        }

        internal void SetText(string value)
        {
            Text = value;
        }

        internal void SetValue(object value)
        {
            Value = value;
        }

        internal string? GetItemId()
        {
            return GetId();
        }

        /// <inheritdoc />
        public override async Task SetParametersAsync(ParameterView parameters)
        {
            var shouldRefresh = parameters.DidParameterChange(nameof(Disabled), Disabled) ||
                parameters.DidParameterChange(nameof(Text), Text) ||
                parameters.DidParameterChange(nameof(Value), Value) ||
                parameters.DidParameterChange(nameof(Icon), Icon) ||
                parameters.DidParameterChange(nameof(IconColor), IconColor) ||
                parameters.DidParameterChange(nameof(Image), Image) ||
                parameters.DidParameterChange(nameof(ImageStyle), ImageStyle);

            await base.SetParametersAsync(parameters);

            if (shouldRefresh && SelectBar != null)
            {
                SelectBar.Refresh();
            }
        }
    }
}