# Blazing Orchard Legacy Frame

`BlazingOrchard.LegacyFrame` is a hidden Orchard admin theme owned by `BlazingOrchard.Server`. It is backend infrastructure used by Blazing Orchard to render standard Orchard Core admin pages inside Blazor iframes.

Requests that include `?legacy-frame=1` bypass the Blazor admin shell and are rendered with this stripped frame theme. The layout intentionally omits Orchard's normal admin navigation and header chrome because the UI chrome is owned by the Blazing Admin WASM shell in `BlazingOrchard.Components`.

This theme is part of the server overlay because it participates in Orchard theme selection and backend rendering. The iframe component and styling that display it belong in `BlazingOrchard.Components`.

Version format follows Blazing Orchard's five-part compatibility scheme:

```text
{orchard-major}.{orchard-minor}.{orchard-patch}.{blazing-security}.{blazing-bug}
```

Current compatibility version: `3.0.0.0.0`.
