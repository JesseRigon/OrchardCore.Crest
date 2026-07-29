using System.Reflection;
using Elsa.Workflows.UIHints.CodeEditor;

namespace OrchardCore.Crest.Workflows.Queries.UI;

public class SqlCodeOptionsProvider : CodeEditorOptionsProviderBase
{
    protected override string GetLanguage(PropertyInfo propertyInfo, object? context) => "sql";
}