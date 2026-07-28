using Microsoft.AspNetCore.Components;
using System;
using System.Globalization;
using System.Linq;

namespace Crest.Components.Primitives
{
    /// <summary>
    /// A Gravatar avatar component that displays a user's profile image from Gravatar.com based on their email address.
    /// CrestGravatar automatically fetches and displays the globally recognized avatar associated with an email.
    /// Gravatar (Globally Recognized Avatar) is a service that associates profile images with email addresses.
    /// Fetches avatar using MD5 hash of email address, requires no storage or management of avatar images, shows default retro-style avatar if email has no Gravatar,
    /// features configurable pixel dimensions, and uses secure.gravatar.com to retrieve images.
    /// Generates a Gravatar URL from the email and displays it as an image. If the email doesn't have a Gravatar account, a retro-style default avatar is shown.
    /// Commonly used in user profiles, comment sections, or anywhere user identity is displayed.
    /// </summary>
    /// <example>
    /// Basic Gravatar:
    /// <code>
    /// &lt;CrestGravatar Email="user@example.com" /&gt;
    /// </code>
    /// Large Gravatar with custom alternate text:
    /// <code>
    /// &lt;CrestGravatar Email=@currentUser.Email Size="80" AlternateText=@currentUser.Name /&gt;
    /// </code>
    /// Gravatar in profile header:
    /// <code>
    /// &lt;CrestStack Orientation="Orientation.Horizontal" Gap="1rem" AlignItems="AlignItems.Center"&gt;
    ///     &lt;CrestGravatar Email=@user.Email Size="64" /&gt;
    ///     &lt;CrestText TextStyle="TextStyle.H5"&gt;@user.Name&lt;/CrestText&gt;
    /// &lt;/CrestStack&gt;
    /// </code>
    /// </example>
    public partial class CrestGravatar : CrestComponent
    {
        /// <summary>
        /// Gets or sets the email address used to fetch the Gravatar image.
        /// The email is hashed (MD5) and used to query Gravatar.com for the associated profile image.
        /// </summary>
        /// <value>The email address.</value>
        [Parameter]
        public string? Email { get; set; }

        private string? alternateText;

        /// <summary>
        /// Gets or sets the alternate text describing the avatar for accessibility.
        /// This text is read by screen readers and displayed if the image fails to load.
        /// </summary>
        /// <value>The image alternate text. Default is "gravatar".</value>
        [Parameter]
        public string AlternateText { get => alternateText ?? Localize(nameof(CrestStrings.Gravatar_AlternateText)); set => alternateText = value; }

        /// <summary>
        /// Gets or sets the size of the avatar image in pixels (both width and height).
        /// Gravatar provides square images at various sizes.
        /// </summary>
        /// <value>The avatar size in pixels. Default is 36.</value>
        [Parameter]
        public int Size { get; set; } = 36;

        /// <summary>
        /// Gets gravatar URL.
        /// </summary>
        protected string Url
        {
            get
            {
                var md5Email = MD5.Calculate(System.Text.Encoding.ASCII.GetBytes(Email != null ? Email : ""));

                var style = "retro";

                return $"https://secure.gravatar.com/avatar/{md5Email}?d={style}&s={Size}";
            }
        }

        string GetAlternateText()
        {
            if (Attributes != null && Attributes.TryGetValue("alt", out var @alt) && !string.IsNullOrEmpty(Convert.ToString(@alt, CultureInfo.InvariantCulture)))
            {
                return $"{AlternateText} {@alt}";
            }

            return AlternateText;
        }

        /// <inheritdoc />
        protected override string GetComponentCssClass()
        {
            return "rz-gravatar";
        }
    }
}
