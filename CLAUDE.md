# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## What this is

CalcClock is a WinUI 3 desktop calculator for clock/time durations, inspired by the Windows default
Calculator app. Instead of plain numbers, its operands are durations broken into Year/Month/Day/Hour/
Minute/Second segments, and the user can enable/disable which of those segments are active. Disabling a
unit doesn't discard its magnitude - it rolls into the next smaller *enabled* unit (e.g. with Year, Month,
and Day disabled, a value that would be "1 day" is shown as "24 hours" instead, since Hour is the largest
enabled unit). By default only Hour, Minute, and Second are enabled.

v1 scope is basic arithmetic only: `+`, `-`, `×`, `÷`. Since multiplying/dividing two durations has no
sensible meaning, `×` and `÷` take a plain scalar number as the second operand, while `+` and `-` take
another full duration.

## Commands

```
dotnet build CalcClock.sln          # build everything
dotnet test CalcClock.sln           # run the Core test suite
dotnet test --filter FullyQualifiedName~ClockValueTests   # run a single test class
dotnet test --filter DisplayName~Decompose_NegativeValue  # run a single test by name

dotnet run --project src/CalcClock.App   # run the WinUI app

dotnet publish src/CalcClock.App/CalcClock.App.csproj -c Release -r win-x64 --self-contained true -o publish/win-x64
```

The app project builds/runs per-architecture (`win-x64`/`win-x86`/`win-arm64`, inferred from the host by
default). When building via the `.sln`, MSBuild resolves `Platform=x64` and the app's output lands under
`src/CalcClock.App/bin/x64/Debug/...`; building the `.csproj` directly (no explicit platform) puts it under
`src/CalcClock.App/bin/Debug/...`. Both are valid - just don't be surprised by the different path.

`publish/` is gitignored - it's a local build artifact, not something to commit.

## Architecture

The solution is split so the calculation engine has zero WinUI dependency and is fully unit-testable:

- **`src/CalcClock.Core`** - plain .NET class library with all calculation logic and no UI framework
  references. This is where almost all business logic lives and where new logic should go by default.
  - `ClockUnit` - the six units, `Year` (largest) through `Second` (smallest); enum order = magnitude order.
  - `UnitConversion` - fixed seconds-per-unit conversion factors. Year and Month aren't real fixed-length
    calendar units, so v1 deliberately uses fixed averages (365.25 days/year, 1/12 of that per month)
    rather than calendar-anchored math - there's no anchor date anywhere in this app.
  - `UnitSettings` - which units are currently enabled (default: Hour, Minute, Second). Enforces that at
    least one unit stays enabled. Raises `PropertyChanged` on toggle so the UI can react.
  - `ClockValue` - a duration, stored internally as `decimal TotalSeconds` so arithmetic (`+ - * /` operators)
    is exact and independent of which units happen to be enabled. `Decompose(UnitSettings)` is the only
    place that turns a value back into per-unit magnitudes for display, greedily from largest to smallest
    enabled unit, with the smallest enabled unit absorbing the (rounded) remainder.
  - `ClockEntryBuffer` - the digit-entry model for typing a duration: digits fill in from the right (like a
    timer/stopwatch picker) and shift left as more are typed. Every enabled unit holds 2 digits *except*
    the largest enabled unit, which is unbounded - it has to be able to absorb whatever cascades down from
    disabled larger units (e.g. Hour must be able to show values like 8760 when Year/Month/Day are off).
  - `CalculatorViewModel` - the UI-framework-agnostic state machine (digit/operator/equals/clear entry,
    pending operator, chained operations, divide-by-zero error state, expression history text). The WinUI
    page binds to this directly; it is not a thin pass-through, it's where the interaction logic lives.
    A key invariant to preserve if you touch this: whenever `CalculatorEntryMode.Scalar` is active, there is
    always a pending operand/operator, because Scalar mode can only be entered via `*` or `/`.
  - `UnitSegmentDisplay` - plain POCO (Unit/Label/Text) bound to by the segmented display's `ItemsControl`.

- **`src/CalcClock.App`** - the WinUI 3 (Windows App SDK) presentation layer, **unpackaged** (`WindowsPackageType=None`,
  `WindowsAppSDKSelfContained=true`, no MSIX/`Package.appxmanifest`). `MainPage.xaml.cs` is intentionally
  thin: it owns a single `CalculatorViewModel` instance, forwards button clicks to it, and syncs the units
  Flyout's `ToggleSwitch` controls against `Settings` (toggles aren't data-bound to avoid re-entrancy when a
  toggle-back is needed for the "can't disable the last unit" case). `Converters/` holds the small
  `IValueConverter`s needed because classic `{Binding}` (not `x:Bind`) is used throughout for simplicity.

- **`tests/CalcClock.Core.Tests`** - xUnit tests, exclusively against `CalcClock.Core`. There is no UI test
  project; UI logic is kept thin enough on purpose that it shouldn't need its own tests.

### A publish gotcha worth knowing

`dotnet publish` on this project (`Microsoft.WindowsAppSDK.WinUI.CSharp.Templates` is still an alpha
package) silently drops the compiled-XAML outputs (`App.xbf`, `MainPage.xbf`, `MainWindow.xbf`), the
project's own merged resources file (`CalcClock.App.pri`), and the `Assets` folder from the publish
output - `dotnet build` includes all of them correctly, but publish's `ComputeFilesToPublish` step doesn't
pick them up. Without them the published exe crashes on launch inside `Microsoft.UI.Xaml.dll` with
`STATUS_STOWED_EXCEPTION` (0xc000027b) - it is *not* related to trimming, ReadyToRun, or self-contained-ness
(all were tested independently; only the plain `dotnet build` output worked, and only publish was broken).
`CalcClock.App.csproj` has a `CopyMissingWinUIPublishAssets` target (`AfterTargets="Publish"`) that copies
these files from the regular build output into the publish directory to work around it. If a newer version
of the WindowsAppSDK/WinUI templates fixes this upstream, this target can likely be removed - verify with a
plain publish (no workaround) and check the published `.exe` actually launches before removing it.

### A display-formatting gotcha worth knowing

`decimal` has no negative zero. A negative `ClockValue` whose *largest enabled unit* happens to be zero
(e.g. `-15s` with Hour/Minute/Second enabled) will silently lose its sign if you naively format each
decomposed segment with its own sign. Both display paths (`CalculatorViewModel.RefreshDisplay` and
`FormatClockValue`) work around this by formatting every segment's *magnitude* and stamping a single `-`
onto the largest unit's text when `TotalSeconds < 0`. Keep this in mind if you touch either method -
`ClockValueTests.Decompose_NegativeValue_KeepsSignOnEveryUnit` and
`CalculatorViewModelTests.Subtract_ResultCanGoNegative` both guard this.
