using Crest.Components.Primitives;

namespace Crest.Components.Primitives;

/// <summary>
/// Supplies information about a <see cref="CrestHtmlEditor.Execute" /> event that is being raised.
/// </summary>
public class HtmlEditorExecuteEventArgs
{
    /// <summary>
    /// Gets the CrestHtmlEditor instance which raised the event.
    /// </summary>
    public CrestHtmlEditor Editor { get; set; }

    /// <summary>
    /// Initializes a new instance of the <see cref="HtmlEditorExecuteEventArgs"/> class.
    /// </summary>
    /// <param name="editor">The editor instance.</param>
    internal HtmlEditorExecuteEventArgs(CrestHtmlEditor editor)
    {
        Editor = editor;
    }

    /// <summary>
    /// Gets the name of the command which CrestHtmlEditor is executing.
    /// </summary>
    public string? CommandName { get; set; }
}

