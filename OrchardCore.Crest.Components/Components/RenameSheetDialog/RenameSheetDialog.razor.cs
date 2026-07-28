using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Components;

using Crest.Components.Primitives;
namespace Crest.Components.Primitives.Spreadsheet;

#nullable enable

/// <summary>
/// Dialog for renaming a sheet in a spreadsheet.
/// </summary>
public partial class RenameSheetDialog : SpreadsheetDialogBase
{
    /// <summary>
    /// The current name of the sheet.
    /// </summary>
    [Parameter]
    public string Name { get; set; } = "";

    /// <summary>
    /// The names of existing sheets used for duplicate validation.
    /// </summary>
    [Parameter]
    public IReadOnlyList<string> ExistingNames { get; set; } = [];

    private string? error;

    private void OnOk()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            error = L(nameof(CrestStrings.Spreadsheet_NameCannotBeEmpty));
            return;
        }

        foreach (var existing in ExistingNames)
        {
            if (string.Equals(existing, Name, StringComparison.OrdinalIgnoreCase))
            {
                error = string.Format(System.Globalization.CultureInfo.CurrentCulture, L(nameof(CrestStrings.Spreadsheet_SheetNameAlreadyExists)), Name);
                return;
            }
        }

        DialogService.Close(Name);
    }
}
