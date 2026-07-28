namespace Crest.Components.Primitives.Documents.Spreadsheet;

/// <summary>
/// Represents a data point used to render spreadsheet charts via CrestChart.
/// </summary>
public class ChartDataPoint
{
    /// <summary>
    /// Gets or sets the category label.
    /// </summary>
    public string Category { get; set; } = "";

    /// <summary>
    /// Gets or sets the numeric value.
    /// </summary>
    public double Value { get; set; }
}
