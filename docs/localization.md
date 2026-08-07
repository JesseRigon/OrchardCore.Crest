# Localization

Crest has two independent culture-resolution paths, because the two surfaces they serve are architecturally different.

## Admin (Blazor WASM)

`Crest.Admin` has no server-side round trip per render, so it resolves culture entirely client-side: `DisplayManager.RefreshManifestAsync` walks a priority chain (session override, stored user default, browser locale, tenant default) and writes one cookie (`crest_culture_{shellVersionId}`) with the final answer. See `plans/user-localization.md`'s "Resolution architecture" section for the full chain.

## Front end and anonymous visitors (server-rendered)

The tenant's front-end site (`OrchardCore.Crest.Site`) is plain server-rendered Razor/Liquid — no WASM client to resolve anything itself. It relies on the stock ASP.NET Core `RequestLocalizationOptions` pipeline instead:

1. `CrestCultureCookieOptionsConfiguration` (`OrchardCore.Crest.Server/Services/CrestCultureCookie.cs`) rebuilds `RequestCultureProviders` as `[CookieRequestCultureProvider, AcceptLanguageHeaderRequestCultureProvider]` — Crest's own tenant-wide cookie (the same one the admin's client-side chain writes) first, then the browser's `Accept-Language` header as the fallback for a visitor who hasn't run the admin client yet.
2. This runs as an `IPostConfigureOptions<RequestLocalizationOptions>`, not `IConfigureOptions<T>`, deliberately: stock OrchardCore.Localization's `AdminCookieCultureProvider` also inserts itself into this same options object via its own `IConfigureOptions<T>`, and ASP.NET Core does not guarantee configure-delegate ordering across independent DI registrations — two competing `Insert(0, ...)` calls race, and whichever ran last would win unpredictably. `IPostConfigureOptions<T>` is guaranteed to run after every `IConfigureOptions<T>`, so this wins deterministically instead of fighting the race.
3. The tenant's actual supported/default cultures come from `LocalizationSettings` (`OrchardCore.Localization`'s site settings, editable at `/Admin/Settings/localization` or via a recipe's `settings` step) — **as top-level keys of the step itself** (`LocalizationSettings`, not wrapped in an extra `Properties` key). `SettingsStep.cs`'s recipe handler writes any key it doesn't special-case directly into `site.Properties[key]`, so an extra wrapper key lands one level too deep and `GetOrCreate<T>`'s lookup silently returns an empty settings object instead of erroring — no exception, just cultures that quietly never resolve.

An anonymous visitor with no cookie yet gets whatever their browser sends via `Accept-Language`, falling back to the tenant's `DefaultCulture` if their locale isn't in `SupportedCultures`.

## Notice: server machine culture and invariant globalization

`LocalizationService.cs`'s own fallback — used only when a tenant has *no* `LocalizationSettings` document at all — is `[CultureInfo.InstalledUICulture.Name]`, i.e. the host machine's configured locale. In a container with `LANG=C.UTF-8` (no named .NET culture), `CultureInfo.InstalledUICulture` resolves to `""` (effectively `CultureInfo.InvariantCulture`), and that empty value silently becomes the tenant's "supported culture" whenever real settings are missing — not a thrown error, just every visitor getting invariant/base-English formatting regardless of `Accept-Language`. This is unrelated to ICU data being present (`CultureInfo.GetCultureInfo("es-ES")` resolves fine even with `LANG=C.UTF-8`); it's specifically that `InstalledUICulture` has nothing to map the OS locale name to. `dev/dev.sh` and `dev/reference-sample.sh` both pin `LANG=en_US.UTF-8` for local dev to avoid this trap — if you ever see anonymous visitors stuck on invariant/English formatting in a fresh environment (a new container image, a CI runner, a minimal deployment target) with otherwise-correct `LocalizationSettings`, check `LANG` on that host before assuming it's a code bug.
