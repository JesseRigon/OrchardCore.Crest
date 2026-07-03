# BlazingOrchard.Admin

BlazingOrchard.Admin registers the Orchard admin theme and contains the Blazor WebAssembly admin UI.

Layout:

```text
BlazingOrchard.Admin/
  BlazingOrchard.Admin.csproj   # server-side Orchard theme manifest assembly
  Manifest.cs                   # Orchard theme metadata for BlazingOrchard.Admin
  wasm/                         # browser-wasm Blazor application
    BlazingOrchard.Admin.Wasm.csproj
    Pages/
    Components/
    wwwroot/
```

The manifest and WebAssembly app remain separate projects because browser-wasm cannot reference the Orchard server runtime stack, but they now live under one theme folder.

## Versioning

This theme follows the BlazingOrchard five-part compatibility version:

```text
{orchard-major}.{orchard-minor}.{orchard-patch}.{blazing-security}.{blazing-bug}
```

Current compatibility version: `3.0.0.0.0`.

- `3.0.0` = Orchard Core compatibility line.
- trailing `.0.0` = BlazingOrchard-owned security and bug counters.

When Orchard Core moves to a new tested compatibility line, the first three parts change. The Blazing-owned counters reset on a new Orchard main release.
