#nullable enable

using System;

namespace Crest.Components.Primitives.Documents.Spreadsheet;

class SqrtFunction : UnaryMathFunctionBase
{
    public override string Name => "SQRT";

    protected override CellData Compute(double number)
    {
        return number < 0 ? CellData.FromError(CellError.Num) : CellData.FromNumber(Math.Sqrt(number));
    }
}
