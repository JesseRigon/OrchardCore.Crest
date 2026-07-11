# Blazing Orchard Legacy Frame

`BlazingOrchard.LegacyFrame` is a hidden admin theme used by Blazing Orchard to render standard Orchard Core admin pages inside Blazor iframes.

Requests that include `?legacy-frame=1` bypass the Blazor admin shell and are rendered with this stripped frame theme. The layout intentionally omits Orchard's normal admin navigation and header chrome because Blazing Admin owns those areas.

Version format follows Blazing Orchard's five-part compatibility scheme:

```text
{orchard-major}.{orchard-minor}.{orchard-patch}.{blazing-security}.{blazing-bug}
```

Current compatibility version: `3.0.0.0.0`.
