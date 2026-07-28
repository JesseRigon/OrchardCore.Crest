using System.Collections.Generic;

namespace Crest.Components.Primitives;

/// <summary>
/// Supplies information about a <see cref="Crest.Components.Primitives.CrestUpload.Change" /> event that is being raised.
/// </summary>
public class UploadChangeEventArgs
{
    /// <summary>
    /// Gets a collection of the selected files.
    /// </summary>
    public IEnumerable<FileInfo>? Files { get; set; }
}

