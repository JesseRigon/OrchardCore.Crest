#nullable enable

using System.Collections.Generic;

namespace Crest.Components.Primitives.Documents.Spreadsheet;

class ProductFunction : StatisticalAggregateFunctionBase
{
    public override string Name => "PRODUCT";

    protected override CellData Compute(List<double> numbers) => AggregationMethods.Product(numbers);
}
