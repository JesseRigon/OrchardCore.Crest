#nullable enable

namespace Crest.Components.Primitives.Documents.Spreadsheet;

class MaxFunction : MinMaxBase
{
    public override string Name => "MAX";
    protected override CellData Compute(System.Collections.Generic.List<double> numbers) => AggregationMethods.Max(numbers);
}