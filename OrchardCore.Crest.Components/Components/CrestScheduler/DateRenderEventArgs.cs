using System;
using System.Collections.Generic;

namespace Crest.Components.Primitives;

/// <summary>
/// Supplies information about a <see cref="Crest.Components.Primitives.CrestDatePicker{TValue}.DateRender" /> event that is being raised.
/// </summary>
public class DateRenderEventArgs
{
    /// <summary>
    /// Gets or sets the HTML attributes that will be applied to item HTML element.
    /// </summary>
    /// <example>
    /// void OnDateRender(DateRenderEventArgs args)
    /// {
    ///     args.Attributes["style"] = "background-color: red; color: black;";
    /// }
    /// </example>
    /// <value>The attributes.</value>
    public IDictionary<string, object> Attributes { get; private set; } = new Dictionary<string, object>();

    /// <summary>
    /// Gets the date which the rendered item represents.
    /// </summary>
    public DateTime Date { get; internal set; }

    /// <summary>
    /// Gets or sets a value indicating whether the rendered item is disabled.
    /// </summary>
    /// <value><c>true</c> if disabled; otherwise, <c>false</c>.</value>
    public bool Disabled { get; set; }
}

