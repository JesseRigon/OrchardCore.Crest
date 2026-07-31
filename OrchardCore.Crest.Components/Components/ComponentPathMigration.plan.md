# Component path migration plan

Status: applied. This file is the path-change source of truth for the completed migration.

## Second-pass domain colocation

Applied: the initial pass used exact component-name matching. This follow-up moves coherent
component-family code that has a different filename into the component family that
owns its behavior, while leaving cross-cutting infrastructure in `Utilities/Crest`.

| Utility family | Destination component family |
|---|---|
| `DataGrid*.cs` | `Components/Primitives/CrestDataGrid/` |
| chart/axis/series/scale/legend/marker types | `Components/Primitives/CrestChart/` |
| `Scheduler*.cs`, `IScheduler*.cs`, appointment/date/month types | `Components/Primitives/CrestScheduler/` |
| `Gantt*.cs` | `Components/Primitives/CrestGantt/` |
| `HtmlEditor*.cs` | `Components/Primitives/CrestHtmlEditor/` |
| `Tree*.cs` | `Components/Primitives/CrestTree/` |
| `Upload*.cs`, preview-file types | `Components/Primitives/CrestUpload/` |
| form, validation, and dropdown-base types | `Components/Primitives/CrestTemplateForm/` |
| dialog, tooltip, notification, context-menu services | their corresponding component folders |
| AI chat, Google Map, pager, pick-list, tabs, and steps types | their corresponding component folders |
| component option enums, rendering modes, and event models | their corresponding component folders |

`CrestComponent`, `CrestComponentWithChildren`, component activation, generic
query/expression utilities, shared collection models, JS helpers, and dependency
registration remain in `Utilities/Crest` because they serve multiple component
families.

## Rules

1. For every `Components/**/<Component>.razor` (except `_Imports.razor`), create a sibling `<Component>/` folder.
2. Move that component's `.razor`, `.razor.cs`, and `.razor.css` companions into its folder. Components with code-behind but no Razor markup receive the same folder treatment.
3. Move sibling `Components/**/<Component>*.cs` support files into the same component folder, using the longest matching component name. This includes component-specific models, events, and bases.
4. Move `Utilities/Crest/<Component>*.cs` into the same component folder when a Crest component root exists. General-purpose shared utilities remain in `Utilities/Crest`.
5. Preserve namespaces. Because a Razor component folder is otherwise appended to its generated namespace, add an explicit `@namespace` directive to each moved primitive Razor component, set to its pre-move namespace. Place that directive after any leading Razor directives (`@inherits`, `@implements`, `@typeparam`, etc.) so their required ordering is retained. Existing explicit `@namespace` directives in Crest-authored components remain authoritative.
6. Do not move or overwrite the isolated duplicate implementations in `RadzenSource`.

## Path transformations

| Source | Destination | Scope |
|---|---|---|
| `Components/<Area>/<Component>.razor` | `Components/<Area>/<Component>/<Component>.razor` | every Razor component except imports |
| `Components/<Area>/<Component>.razor.cs` | `Components/<Area>/<Component>/<Component>.razor.cs` | companion code-behind where present |
| `Components/<Area>/<Component>.razor.css` | `Components/<Area>/<Component>/<Component>.razor.css` | scoped companion CSS where present |
| `Components/<Area>/<Component>*.cs` | `Components/<Area>/<Component>/<same filename>` | sibling, longest-prefix component support files |
| `Utilities/Crest/<Component>*.cs` | `Components/<Area>/<Component>/<same filename>` | only Crest-prefixed component support files listed below |
| `Components/Primitives/**/<Component>/<Component>.razor` | add explicit `@namespace` | preserves the namespace that existed before the folder was introduced |

## Matched utility files to colocate

```text
Utilities/Crest/CrestAccordionItem.cs -> Components/Primitives/CrestAccordion/CrestAccordionItem.cs
Utilities/Crest/CrestBarcodeEncoder.cs -> Components/Primitives/CrestBarcode/CrestBarcodeEncoder.cs
Utilities/Crest/CrestChartComponentBase.cs -> Components/Primitives/CrestChart/CrestChartComponentBase.cs
Utilities/Crest/CrestChartRangeNavigator.cs -> Components/Primitives/CrestChart/CrestChartRangeNavigator.cs
Utilities/Crest/CrestChartTooltipOptions.cs -> Components/Primitives/CrestChartTooltip/CrestChartTooltipOptions.cs
Utilities/Crest/CrestCheckBoxListItem.cs -> Components/Primitives/CrestCheckBoxList/CrestCheckBoxListItem.cs
Utilities/Crest/CrestColumnOptions.cs -> Components/Primitives/CrestColumn/CrestColumnOptions.cs
Utilities/Crest/CrestDataFilterProperty.cs -> Components/Primitives/CrestDataFilter/CrestDataFilterProperty.cs
Utilities/Crest/CrestDropZoneItemEventArgs.cs -> Components/Primitives/CrestDropZoneItem/CrestDropZoneItemEventArgs.cs
Utilities/Crest/CrestDropZoneItemRenderEventArgs.cs -> Components/Primitives/CrestDropZoneItem/CrestDropZoneItemRenderEventArgs.cs
Utilities/Crest/CrestGoogleMapMarker.cs -> Components/Primitives/CrestGoogleMap/CrestGoogleMapMarker.cs
Utilities/Crest/CrestHtmlEditorButtonBase.cs -> Components/Primitives/CrestHtmlEditor/CrestHtmlEditorButtonBase.cs
Utilities/Crest/CrestHtmlEditorColorBase.cs -> Components/Primitives/CrestHtmlEditorColor/CrestHtmlEditorColorBase.cs
Utilities/Crest/CrestHtmlEditorCommandState.cs -> Components/Primitives/CrestHtmlEditor/CrestHtmlEditorCommandState.cs
Utilities/Crest/CrestLayout.cs -> Components/Primitives/CrestLayout/CrestLayout.cs
Utilities/Crest/CrestNumericRangeValidator.cs -> Components/Primitives/CrestNumeric/CrestNumericRangeValidator.cs
Utilities/Crest/CrestRadioButtonListItem.cs -> Components/Primitives/CrestRadioButtonList/CrestRadioButtonListItem.cs
Utilities/Crest/CrestSSRSViewerParameter.cs -> Components/Primitives/CrestSSRSViewer/CrestSSRSViewerParameter.cs
Utilities/Crest/CrestSelectBarItem.cs -> Components/Primitives/CrestSelectBar/CrestSelectBarItem.cs
Utilities/Crest/CrestSplitterEventArgs.cs -> Components/Primitives/CrestSplitter/CrestSplitterEventArgs.cs
Utilities/Crest/CrestSplitterResizeEventArgs.cs -> Components/Primitives/CrestSplitter/CrestSplitterResizeEventArgs.cs
Utilities/Crest/CrestStepsItem.cs -> Components/Primitives/CrestSteps/CrestStepsItem.cs
Utilities/Crest/CrestTreeLevel.cs -> Components/Primitives/CrestTree/CrestTreeLevel.cs
```

## Explicit exclusions

- `Components/Primitives/_Imports.razor` remains at the primitive root.
- `Utilities/Crest` files with no component-root prefix match remain shared utilities.
- `RadzenSource/` remains the isolated duplicate/reference area.
