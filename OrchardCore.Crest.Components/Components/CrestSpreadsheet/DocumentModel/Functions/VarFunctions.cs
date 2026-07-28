#nullable enable

using System.Collections.Generic;

namespace Crest.Components.Primitives.Documents.Spreadsheet;

class VarFunction : StatisticalAggregateFunctionBase
{
    public override string Name => "VAR";

    protected override CellData Compute(List<double> numbers) => AggregationMethods.VarS(numbers);
}

class VarpFunction : StatisticalAggregateFunctionBase
{
    public override string Name => "VARP";

    protected override CellData Compute(List<double> numbers) => AggregationMethods.VarP(numbers);
}
