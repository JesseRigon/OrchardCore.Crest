using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using Microsoft.AspNetCore.Components.Web;
using System.Threading.Tasks;

namespace Crest.Components.Primitives
{
    /// <summary>
    /// A menu item component used within CrestProfileMenu to define individual navigation or action items.
    /// CrestProfileMenuItem represents one clickable item in a profile menu dropdown with support for icons, navigation, and custom content.
    /// Used inside CrestProfileMenu to create user profile dropdown menus. Each item can navigate to a page (via Path), trigger an action (via click event), or display custom content (via Template).
    /// Common uses in profile menus include account settings, user profile page, logout/sign out, preferences, and help/documentation.
    /// Items support icons, images, text, and custom templates for flexible rendering.
    /// </summary>
    /// <example>
    /// Profile menu items in a profile menu:
    /// <code>
    /// &lt;CrestProfileMenu&gt;
    ///     &lt;Template&gt;
    ///         &lt;RadzenIcon Icon="account_circle" /&gt; John Doe
    ///     &lt;/Template&gt;
    ///     &lt;ChildContent&gt;
    ///         &lt;CrestProfileMenuItem Text="Profile" Icon="person" Path="/profile" /&gt;
    ///         &lt;CrestProfileMenuItem Text="Settings" Icon="settings" Path="/settings" /&gt;
    ///         &lt;CrestProfileMenuItem Text="Logout" Icon="logout" Value="logout" /&gt;
    ///     &lt;/ChildContent&gt;
    /// &lt;/CrestProfileMenu&gt;
    /// </code>
    /// </example>
    public partial class CrestProfileMenuItem : CrestComponent
    {
        /// <inheritdoc />
        protected override string GetComponentCssClass()
        {
            return "rz-navigation-item";
        }

        private string? imageAlternateText;

        /// <summary>
        /// Gets or sets the text.
        /// </summary>
        /// <value>The text.</value>
        [Parameter]
        public string ImageAlternateText { get => imageAlternateText ?? Localize(nameof(CrestStrings.ProfileMenuItem_ImageAlternateText)); set => imageAlternateText = value; }

        /// <summary>
        /// Gets or sets the target.
        /// </summary>
        /// <value>The target.</value>
        [Parameter]
        public string? Target { get; set; }

        /// <summary>
        /// Gets or sets the path.
        /// </summary>
        /// <value>The path.</value>
        [Parameter]
        public string? Path { get; set; }

        /// <summary>
        /// Gets or sets the navigation link match.
        /// </summary>
        /// <value>The navigation link match.</value>
        [Parameter]
        public NavLinkMatch Match { get; set; } = NavLinkMatch.Prefix;

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
        public string? Text { get; set; }

        /// <summary>
        /// Gets or sets the value.
        /// </summary>
        /// <value>The value.</value>
        [Parameter]
        public string? Value { get; set; }

        /// <summary>
        /// Gets or sets the template.
        /// </summary>
        /// <value>The template.</value>
        [Parameter]
        public RenderFragment? Template { get; set; }

        /// <summary>
        /// Gets or sets the menu.
        /// </summary>
        /// <value>The menu.</value>
        [CascadingParameter]
        public CrestProfileMenu? Menu { get; set; }


        /// <summary>
        /// Handles the click event.
        /// </summary>
        /// <param name="args">The <see cref="MouseEventArgs"/> instance containing the event data.</param>
        public async System.Threading.Tasks.Task OnClick(MouseEventArgs args)
        {
            if (Menu != null)
            {
                await Menu.Click.InvokeAsync(this);
            }
        }

        CrestProfileMenu? _parent;
        /// <summary>
        /// Gets or sets the parent.
        /// </summary>
        /// <value>The parent.</value>
        [CascadingParameter]
        public CrestProfileMenu? Parent
        {
            get
            {
                return _parent;
            }
            set
            {
                if (_parent != value)
                {
                    _parent = value;

                    _parent?.AddItem(this);
                }
            }
        }

        internal string GetItemCssClass()
        {
            return $"{GetCssClass()} {(Parent?.IsFocused(this) == true ? "rz-state-focused" : "")}".Trim();
        }

        internal string GetItemId()
        {
            return $"{GetId()}";
        }

        internal string GetItemTabIndex()
        {
            return "-1";
        }
    }
}
