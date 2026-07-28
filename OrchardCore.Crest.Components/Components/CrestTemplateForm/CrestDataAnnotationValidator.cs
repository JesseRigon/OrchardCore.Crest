using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Crest.Components.Primitives
{
    /// <summary>
    /// A validator component that validates form inputs using Data Annotations attributes defined on model properties.
    /// CrestDataAnnotationValidator enables automatic validation based on attributes like [Required], [StringLength], [Range], [EmailAddress], etc.
    /// Must be placed inside a <see cref="CrestTemplateForm{TItem}"/>.
    /// Uses the standard .NET validation attributes from System.ComponentModel.DataAnnotations. Reads all validation attributes on a model property and validates the input accordingly.
    /// Benefits include centralized validation (define rules once on the model, use everywhere), support for multiple validation attributes per property,
    /// built-in attributes (Required, StringLength, Range, EmailAddress, Phone, Url, RegularExpression, etc.), works with custom ValidationAttribute implementations,
    /// and multiple errors joined with MessageSeparator.
    /// Ideal when your validation rules are already defined on your data models using data annotations. Automatically extracts error messages from the attributes' ErrorMessage properties.
    /// </summary>
    /// <example>
    /// Model-based validation with data annotations:
    /// <code>
    /// &lt;CrestTemplateForm TItem="UserModel" Data=@user&gt;
    ///     &lt;CrestTextBox Name="Email" @bind-Value=@user.Email /&gt;
    ///     &lt;CrestDataAnnotationValidator Component="Email" /&gt;
    /// &lt;/CrestTemplateForm&gt;
    /// @code {
    ///     class UserModel
    ///     {
    ///         [Required(ErrorMessage = "Email is required")]
    ///         [EmailAddress(ErrorMessage = "Invalid email format")]
    ///         [StringLength(100, ErrorMessage = "Email too long")]
    ///         public string Email { get; set; }
    ///     }
    ///     UserModel user = new UserModel();
    /// }
    /// </code>
    /// Custom error separator:
    /// <code>
    /// &lt;CrestDataAnnotationValidator Component="Name" MessageSeparator=" | " /&gt;
    /// </code>
    /// </example>
    [UnconditionalSuppressMessage(TrimMessages.Trimming, TrimMessages.IL2026, Justification = TrimMessages.ModelTypePreserved)]
    public class CrestDataAnnotationValidator : ValidatorBase
    {
        /// <summary>
        /// Gets or sets the validation error message.
        /// This property is automatically populated with error messages from data annotation attributes when validation fails.
        /// If multiple attributes fail, messages are joined using <see cref="MessageSeparator"/>.
        /// </summary>
        /// <value>The validation error message(s).</value>
        [Parameter]
        public override string Text { get; set; } = string.Empty;

        private string? messageSeparator;

        /// <summary>
        /// Gets or sets the text used to join multiple validation error messages.
        /// When multiple data annotation attributes fail (e.g., both Required and StringLength), their messages are combined with this separator.
        /// </summary>
        /// <value>The message separator text. Default is " and ".</value>
        [Parameter]
        public string MessageSeparator { get => messageSeparator ?? Localize(nameof(CrestStrings.DataAnnotationValidator_MessageSeparator)); set => messageSeparator = value; }

        /// <summary>
        /// Service provider injected from the Dependency Injection (DI) container.
        /// </summary>
        [Inject]
        public IServiceProvider? ServiceProvider { get; set; }

        /// <inheritdoc />
        [UnconditionalSuppressMessage(TrimMessages.Trimming, TrimMessages.IL2067, Justification = TrimMessages.ModelTypePreserved)]
        [UnconditionalSuppressMessage(TrimMessages.Trimming, TrimMessages.IL2070, Justification = TrimMessages.ModelTypePreserved)]
        [UnconditionalSuppressMessage(TrimMessages.Trimming, TrimMessages.IL2072, Justification = TrimMessages.ModelTypePreserved)]
        [UnconditionalSuppressMessage(TrimMessages.Trimming, TrimMessages.IL2080, Justification = TrimMessages.ModelTypePreserved)]
        protected override bool Validate(IRadzenFormComponent component)
        {
            ArgumentNullException.ThrowIfNull(component);

            var validationResults = new List<ValidationResult>();

            var model = component.FieldIdentifier.Model;

            var getter = PropertyAccess.Getter<object>(model, component.FieldIdentifier.FieldName);

            var value = getter(model);

            var validationContext = new ValidationContext(model, ServiceProvider, null)
            {
                MemberName = component.FieldIdentifier.FieldName
            };

            var isValid = Validator.TryValidateProperty(value, validationContext, validationResults);

            if (!isValid)
            {
                Text = string.Join(MessageSeparator, validationResults.Select(vr => vr.ErrorMessage));
            }

            return isValid;
        }
    }
}

