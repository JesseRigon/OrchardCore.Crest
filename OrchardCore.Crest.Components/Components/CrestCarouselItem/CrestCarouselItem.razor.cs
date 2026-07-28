using Microsoft.AspNetCore.Components;
using Crest.Components.Primitives.Rendering;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Crest.Components.Primitives
{
    /// <summary>
    /// CrestCarouselItem component.
    /// </summary>
    public partial class CrestCarouselItem : IDisposable
    {        
        /// <summary>
        /// Gets the class list.
        /// </summary>
        /// <value>The class list.</value>
        string Class => ClassList.Create("rz-carousel-item")
                                 .Add(Attributes)
                                 .ToString();
        
        /// <summary>
        /// Gets or sets the arbitrary attributes.
        /// </summary>
        /// <value>The arbitrary attributes.</value>
        [Parameter(CaptureUnmatchedValues = true)]
        public IDictionary<string, object>? Attributes { get; set; }

        /// <summary>
        /// Gets or sets the child content.
        /// </summary>
        /// <value>The child content.</value>
        [Parameter]
        public RenderFragment? ChildContent { get; set; }

        /// <summary>
        /// Gets or sets the tabs.
        /// </summary>
        /// <value>The tabs.</value>
        [CascadingParameter]
        public CrestCarousel? Carousel { get; set; }

        /// <inheritdoc />
        protected override async Task OnInitializedAsync()
        {
            await base.OnInitializedAsync();

            if (Carousel != null)
            {
                Carousel.AddItem(this);
                itemIndex = Carousel.items.IndexOf(this);
            }
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Carousel?.RemoveItem(this);
            GC.SuppressFinalize(this);
        }

        string? ItemStyle => Carousel != null && Carousel.ItemsPerPage > 1
            ? $"flex: 0 0 calc(100% / {Carousel.ItemsPerPage}); width: calc(100% / {Carousel.ItemsPerPage})"
            : null;

        int itemIndex;
        internal ElementReference element;

        internal bool IsActive => Carousel == null || Carousel.IsItemActive(Carousel.items.IndexOf(this));

        internal void Refresh()
        {
            StateHasChanged();
        }
    }
}
