using Microsoft.AspNetCore.Components;
using Crest.Components.Primitives.Rendering;

namespace Crest.Components.Primitives
{
    /// <summary>
    /// A card container component that groups related content with a consistent visual design and optional elevation.
    /// CrestCard provides a versatile styled container for displaying information, images, actions, and other content in a structured format.
    /// Supports different visual variants (Filled, Flat, Outlined, Text) that affect the card's appearance.
    /// Works well in grid layouts (using CrestRow/CrestColumn) or can be stacked vertically.
    /// Ideal for grouping related information, creating dashboard widgets, displaying product information, or organizing form sections.
    /// Combine with other Crest components like CrestImage, CrestText, and CrestButton for rich card content.
    /// </summary>
    /// <example>
    /// Basic card with content:
    /// <code>
    /// &lt;CrestCard&gt;
    ///     &lt;CrestText TextStyle="TextStyle.H6"&gt;Card Title&lt;/CrestText&gt;
    ///     &lt;CrestText&gt;Card content goes here...&lt;/CrestText&gt;
    /// &lt;/CrestCard&gt;
    /// </code>
    /// Card with custom variant:
    /// <code>
    /// &lt;CrestCard Variant="Variant.Outlined" Style="padding: 2rem;"&gt;
    ///     &lt;CrestImage Path="product.jpg" Style="width: 100%; height: 200px; object-fit: cover;" /&gt;
    ///     &lt;CrestText TextStyle="TextStyle.H5"&gt;Product Name&lt;/CrestText&gt;
    ///     &lt;CrestText&gt;Product description...&lt;/CrestText&gt;
    ///     &lt;CrestButton Text="Buy Now" ButtonStyle="ButtonStyle.Primary" /&gt;
    /// &lt;/CrestCard&gt;
    /// </code>
    /// </example>
    public partial class CrestCard : CrestComponentWithChildren
    {
        /// <inheritdoc />
        protected override string GetComponentCssClass() => ClassList.Create("rz-card")
                                                                     .AddVariant(Variant)
                                                                     .ToString();

        /// <summary>
        /// Gets or sets the visual design variant of the card.
        /// Controls the card's appearance: Filled (solid background with elevation), Flat (subtle background), 
        /// Outlined (border only), or Text (minimal styling).
        /// </summary>
        /// <value>The card variant. Default is <see cref="Variant.Filled"/>.</value>
        [Parameter]
        public Variant Variant { get; set; } = Variant.Filled;
    }
}