#nullable enable

using System;

namespace Crest.Components.Primitives.Documents.Spreadsheet;

class RoundFunction : RoundFunctionBase
{
    public override string Name => "ROUND";

    protected override double Round(double value)
    {
        return Math.Round(value, MidpointRounding.AwayFromZero);
    }
}