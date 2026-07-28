using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Forms;
using Crest.Components.Primitives;

namespace Crest.Components.Primitives;

/// <summary>
/// Specifies the interface that form components must implement in order to be supported by <see cref="CrestTemplateForm{TItem}" />.
/// </summary>
public interface ICrestFormComponent
{
    /// <summary>
    /// Gets a value indicating whether this component is bound.
    /// </summary>
    /// <value><c>true</c> if this component is bound; otherwise, <c>false</c>.</value>
    bool IsBound { get; }

    /// <summary>
    /// Gets a value indicating whether the component has value.
    /// </summary>
    /// <value><c>true</c> if this instance has value; otherwise, <c>false</c>.</value>
    bool HasValue { get; }

    /// <summary>
    /// Gets the value of the component.
    /// </summary>
    /// <returns>the value of the component - for example the text of CrestTextBox.</returns>
    object? GetValue();

    /// <summary>
    /// Gets or sets the name of the component.
    /// </summary>
    /// <value>The name.</value>
    string? Name { get; set; }

    /// <summary>
    /// Gets the field identifier.
    /// </summary>
    /// <value>The field identifier.</value>
    FieldIdentifier FieldIdentifier { get; set; }

    /// <summary>
    /// Sets the focus.
    /// </summary>
    /// <returns>A ValueTask representing the asynchronous operation.</returns>
    ValueTask FocusAsync();

    /// <summary>
    /// Sets the Disabled state of the component
    /// </summary>
    bool Disabled { get; set; }

    /// <summary>
    /// Sets the Visible state of the component
    /// </summary>
    bool Visible { get; set; }

    /// <summary>
    /// Sets the FormFieldContext of the component
    /// </summary>
    IFormFieldContext? FormFieldContext { get; }
}

