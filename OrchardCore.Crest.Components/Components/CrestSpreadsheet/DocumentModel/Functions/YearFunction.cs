#nullable enable

using System;

namespace Crest.Components.Primitives.Documents.Spreadsheet;

class YearFunction : DatePartFunctionBase
{
    public override string Name => "YEAR";

    protected override int GetPart(DateTime dateTime) => dateTime.Year;
}