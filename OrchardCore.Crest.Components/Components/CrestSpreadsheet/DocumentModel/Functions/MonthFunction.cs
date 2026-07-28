#nullable enable

using System;

namespace Crest.Components.Primitives.Documents.Spreadsheet;

class MonthFunction : DatePartFunctionBase
{
    public override string Name => "MONTH";

    protected override int GetPart(DateTime dateTime) => dateTime.Month;
}