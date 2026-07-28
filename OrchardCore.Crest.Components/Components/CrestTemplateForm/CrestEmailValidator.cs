using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Components;

namespace Crest.Components.Primitives
{
    /// <summary>
    /// A validator component that verifies whether a text input contains a valid email address format.
    /// CrestEmailValidator uses the .NET EmailAddressAttribute to validate email format according to standard rules.
    /// Must be placed inside a <see cref="CrestTemplateForm{TItem}"/> and associated with a named input component.
    /// Checks email format using System.ComponentModel.DataAnnotations.EmailAddressAttribute validation rules.
    /// Empty or null values are considered valid - combine with <see cref="CrestRequiredValidator"/> to ensure the field is not empty.
    /// The validation runs when the form is submitted or when the component loses focus.
    /// </summary>
    /// <example>
    /// Basic email validation:
    /// <code>
    /// &lt;CrestTemplateForm TItem="Model" Data=@model&gt;
    ///   &lt;CrestTextBox style="display: block" Name="Email" @bind-Value=@model.Email /&gt;
    ///   &lt;CrestEmailValidator Component="Email" Text="Please enter a valid email address" Style="position: absolute" /&gt;
    /// &lt;/CrestTemplateForm&gt;
    /// @code {
    ///  class Model
    ///   {
    ///    public string Email { get; set; }
    ///  }
    ///  
    ///  Model model = new Model();
    /// }
    /// </code>
    /// Combined with required validator:
    /// <code>
    /// &lt;CrestTextBox Name="Email" @bind-Value=@model.Email /&gt;
    /// &lt;CrestRequiredValidator Component="Email" Text="Email is required" /&gt;
    /// &lt;CrestEmailValidator Component="Email" Text="Invalid email format" /&gt;
    /// </code>
    /// </example>
    public class CrestEmailValidator : ValidatorBase
    {
        /// <summary>
        /// Gets or sets the error message displayed when the email address format is invalid.
        /// Customize this message to provide clear feedback to users about the expected email format.
        /// </summary>
        /// <value>The validation error message. Default is "Invalid email".</value>
        [Parameter]
        public override string Text { get => text ?? Localize(nameof(CrestStrings.EmailValidator_Text)); set => text = value; }
        private string? text;

        /// <inheritdoc />
        protected override bool Validate(ICrestFormComponent component)
        {
            ArgumentNullException.ThrowIfNull(component);

            var value = component.GetValue();
            var valueAsString = value as string;

            if (string.IsNullOrEmpty(valueAsString))
            {
                return true;
            }

            var email = new EmailAddressAttribute();

            return email.IsValid(valueAsString);
        }
    }
}