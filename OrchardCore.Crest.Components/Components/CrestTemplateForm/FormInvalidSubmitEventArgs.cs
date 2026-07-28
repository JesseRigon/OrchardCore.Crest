using System.Collections.Generic;

namespace Crest.Components.Primitives;

/// <summary>
/// Supplies information about a <see cref="Crest.Components.Primitives.CrestTemplateForm{TItem}.InvalidSubmit" /> event that is being raised.
/// </summary>
public class FormInvalidSubmitEventArgs
{
    /// <summary>
    /// Gets the validation errors.
    /// </summary>
    public IEnumerable<string>? Errors { get; set; }
}

