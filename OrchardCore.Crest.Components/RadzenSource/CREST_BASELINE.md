# Radzen component source baseline

This directory contains the complete MIT-licensed `Radzen.Blazor` component-library
source, copied into and compiled by `OrchardCore.Crest.Components`. Crest no longer
uses the `Radzen.Blazor` NuGet package at runtime.

The first migration preserves Radzen's public namespaces and component names so
existing Crest code remains source-compatible. The implementation, static assets,
and future changes are Crest-owned. A later explicit breaking release can rename the
public API to Crest names.

The source remains under the MIT license in `LICENSE`. Preserve that notice in every
Crest component derived from this baseline. The current planned behavioral divergence
is Crest/Iconify icon references in place of Radzen's Material icon font.

## Layout

- Component `.razor` files and their `.razor.cs` code-behind files remain together
  at this directory's root so each component stays easy to inspect and can later be
  compiled as a normal Razor partial type.
- The shared and code-only C# implementation lives in the main component project at
  `../Utilities/Radzen/`: component bases, service classes, event args, data-grid/
  chart models, enums, and helpers.
- `Documents/`, `Spreadsheet/`, and `Rendering/` remain separate because each is a
  cohesive subsystem.

C# resolves code by namespace and project inclusion rather than directory path, so
this organization requires no reference or namespace changes.
