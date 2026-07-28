using System;
using Microsoft.AspNetCore.Components;
using Crest.Components.Primitives.Rendering;

using Crest.Components.Primitives.Documents.Spreadsheet;
namespace Crest.Components.Primitives.Spreadsheet;

#nullable enable

/// <summary>
/// Renders the autofill handle and drag preview overlay for a spreadsheet.
/// </summary>
public partial class AutofillOverlay : IDisposable
{
    /// <summary>
    /// Gets or sets the worksheet.
    /// </summary>
    [Parameter]
    public Worksheet Worksheet { get; set; } = default!;

    /// <summary>
    /// Gets or sets the context for the virtual grid.
    /// </summary>
    [Parameter]
    public IVirtualGridContext Context { get; set; } = default!;

    private readonly EventBinding<Selection> selectionBinding;
    private readonly EventBinding<Worksheet> worksheetBinding;

    /// <summary>
    /// Initializes a new instance of the <see cref="AutofillOverlay"/> class.
    /// </summary>
    public AutofillOverlay()
    {
        selectionBinding = new EventBinding<Selection>(
            s => s.Changed += OnChanged,
            s => s.Changed -= OnChanged);

        worksheetBinding = new EventBinding<Worksheet>(
            w => w.AutofillPreviewChanged += OnChanged,
            w => w.AutofillPreviewChanged -= OnChanged);
    }

    /// <inheritdoc/>
    protected override void OnParametersSet()
    {
        selectionBinding.Bind(Worksheet?.Selection);
        worksheetBinding.Bind(Worksheet);
    }

    private void OnChanged()
    {
        StateHasChanged();
    }

    private static string HandleClass(bool frozenRow, bool frozenColumn)
    {
        return ClassList.Create("rz-spreadsheet-autofill-handle")
            .Add("rz-spreadsheet-frozen-row", frozenRow)
            .Add("rz-spreadsheet-frozen-column", frozenColumn)
            .ToString();
    }

    void IDisposable.Dispose()
    {
        selectionBinding.Dispose();
        worksheetBinding.Dispose();
    }
}
