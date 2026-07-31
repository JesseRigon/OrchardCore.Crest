# OrchardCore.Crest tests

This directory is the entrypoint for every test that lives inside the `OrchardCore.Crest`
submodule. Host repos (`fruitful.orchard`, `OrchardCore.Crest.Host`) don't walk Crest's
internal project layout themselves — they both call `run-tests.sh` here, and this
directory is responsible for finding and running everything underneath it.

## Stack

Two different tools cover two different layers, deliberately not merged into one:

- **C# (unit/integration, server-side logic)** — [xUnit](https://xunit.net/) as the test
  runner, [`Verify`](https://github.com/VerifyTests/Verify) (`Verify.XunitV3`) for any
  snapshot/diff-style assertion (JSON, XML, HTTP responses, images — anything you'd
  otherwise hand-roll a baseline comparator for), and
  [`DotNetEnv`](https://github.com/tonerdo/dotnet-env) for loading `.env`-style
  credentials/ports/connection strings into test setup where needed. `NSubstitute` is
  the mocking library.
- **Browser E2E (admin UI)** — the existing Node/Playwright suite under
  `tests/playwright/` (this directory). Screenshot-diff checks use the harness at
  `tests/playwright/harness/screenshot-diff.js`: a committed `base/` baseline, a `new/`
  directory that's wiped and repopulated every run, and `UPDATE_BASE=1` to promote a
  fresh capture to the new baseline.

We adopted xUnit + Verify + DotNetEnv instead of building a bespoke JSON/HTML/API
baseline-diff harness — `Verify` already does exactly that (a committed `.verified.*`
file, a `.received.*` file on mismatch, a diff on failure) and is a maintained package,
so a custom harness would just be duplicate maintenance surface for no benefit.

Browser E2E deliberately stays on Node/Playwright rather than moving to
`Microsoft.Playwright` (the .NET binding) — the existing suite already works and is
wired into `dev.sh`; there's no reason to migrate it. `Microsoft.Playwright` remains an
option later if a concrete need for C#-side browser tests comes up, since it drives the
same underlying browser engine.

**Native (non-browser) UI testing is a known future need**, not yet implemented —
Avalonia is planned for this project, and Playwright (Node or .NET) cannot drive a
native desktop window. When that work starts, look at `Avalonia.Headless` (if the app
being tested is itself built in Avalonia) or FlaUI/WinAppDriver/Appium for OS-level
native UI automation.

## Layout and discovery

```
modules/OrchardCore.Crest/
  tests/
    run-tests.sh                 <- the entrypoint host dev.sh scripts call
    playwright/                  <- shared browser E2E suite (existing)
      checks/                    <- one file per check, hardcoded into run-admin-suite.js
      harness/                   <- auth, health, instance bootstrap, screenshot-diff
      run-admin-suite.js
      run-client-suite.js
  OrchardCore.Crest.Icons/
    tests/
      OrchardCore.Crest.Icons.Tests/
        OrchardCore.Crest.Icons.Tests.csproj   <- discovered and run by run-tests.sh
  OrchardCore.Crest.<OtherProject>/
    tests/
      <OtherProject>.Tests/...                 <- same convention
```

`run-tests.sh` discovers every `OrchardCore.Crest.*/tests/<ProjectName>/*.csproj` and
runs each with `dotnet test`, then runs this directory's own shared Playwright suite.
Adding a new C# test project to any `OrchardCore.Crest.*` subproject means: create it
under `<Subproject>/tests/<ProjectName>/`, and **exclude that `tests/` folder from the
parent project's own compile glob** — SDK-style projects (especially
`Microsoft.NET.Sdk.Razor` ones) default-glob every `.cs` file under the project
directory, so without an explicit exclusion the parent project will try to compile the
test files itself and fail on missing test package references. See
`OrchardCore.Crest.Icons.csproj`'s `<Compile Remove="tests\**\*.cs" />` for the pattern
to copy.

## Why the host repos don't do this scan themselves

`fruitful.orchard` has a flat `modules/*/tests/` layout (one `tests/` dir per module —
`Accounting`, `OrchardCore.Crest`, any future module). `OrchardCore.Crest` is the one
module that isn't flat — it's a submodule with its own nested subprojects, each
potentially owning a `tests/` dir. Rather than have every host repo's `dev.sh`
duplicate knowledge of that nested layout, `OrchardCore.Crest` owns discovering and
running its own tests, and reports pass/fail back to whichever host invoked it. This
keeps `OrchardCore.Crest.Host/dev/dev.sh` (which only ever needs to run this one
module's tests) a thin wrapper, and keeps `fruitful.orchard/dev/dev.sh`'s module loop
simple — it just special-cases `OrchardCore.Crest` as "delegate" instead of "scan
locally" like every other module.

## Credentials never live here

`run-tests.sh` and everything under this directory hold **no credentials, no `.env`
loading, and no server-lifecycle logic**. This submodule is checked out into multiple
independent host repos (`fruitful.orchard`, `OrchardCore.Crest.Host`, and potentially
others), each with its own environment, admin accounts, and database — a credential
baked in here would either leak between hosts or be wrong for at least one of them.

The boundary: `run-tests.sh` accepts `BASE_URL` as an already-resolved input (falling
back to `FRUITFUL_SERVER_URL`/`CREST_SERVER_URL` only as a convenience, not as its own
source of truth) and the Playwright harness (`harness/auth.js`) reads `ADMIN_USER`/
`ADMIN_PASSWORD`/`CLIENT_USER`/`CLIENT_PASSWORD` from the environment with generic
fallback defaults — it never hardcodes a real credential. Each host's own `dev/.env`
(e.g. `fruitful.orchard/dev/.env`'s `ORCHARD_AUTOSETUP_ADMIN_*` values) is what actually
supplies these at test time; that file is host-repo-local and never copied into or
read from this submodule.

If a future test needs a new credential or connection string, add the env var to the
*host's* `.env` and `dev.sh`, not to anything under `modules/OrchardCore.Crest/`.
