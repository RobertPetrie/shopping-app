# Application Architecture

## High-level structure

Shopping App is a .NET MAUI single-project application. UI is written in XAML/C#, application data is local SQLite, and business rules that can be tested without a UI are kept in `Logic/`.

```text
MAUI Shell / Pages
       |
       +--> Logic/              pure filtering, validation, totals
       |
       +--> AppDatabase         local persistence API
                  |
                  +--> SQLite shopping.db3
```

## Target frameworks

`shopping-app.csproj` currently targets:

- `net10.0` — non-MAUI library target used so xUnit can reference application code.
- `net10.0-android`
- `net10.0-ios` on supported non-Linux hosts.
- `net10.0-maccatalyst` on supported non-Linux hosts.
- `net10.0-windows10.0.19041.0` on Windows.

The MAUI version is pinned through `MauiVersion` at 10.0.110.

## Shell and primary navigation

`AppShell.xaml` contains a bottom TabBar with three top-level destinations:

1. **Shopping Lists** — first screen at startup.
2. **Products**
3. **Settings**

The flyout is disabled.

Additional detail/selection pages are registered as Shell routes in `AppShell.xaml.cs`.

### Important selection behavior

The Products UI is reused in a special **Add Products** mode when selecting Products for a Shopping List. In that mode:

- the page title changes;
- regular product editing affordances are replaced by **Add to list** / **Added** controls;
- multiple products can be added without leaving the selector;
- Back returns to the selected Shopping List and causes it to refresh.

Navigation lifecycle logging in `AppShell.xaml.cs` exists partly to diagnose platform-specific Shell teardown issues.

## Dependency injection

`MauiProgram.CreateMauiApp()` registers:

- `AppDatabase` as a singleton.
- `ProductDetailPage` as transient.

The database itself does not open in the service constructor. `AppDatabase.GetConnectionAsync()` initializes it lazily on first use and reuses the resulting connection.

## Database layer

`Data/AppDatabase.cs` is the persistence boundary. It owns:

- database path selection;
- connection initialization;
- schema compatibility/migrations;
- reference-data initialization;
- Product CRUD;
- shopping-list persistence;
- ShoppingListProduct persistence;
- TaxRate persistence;
- whole-list total recalculation.

Pages should use this API rather than opening independent SQLite connections.

See [Data Model](Data-Model.md) for schema details.

## Business-logic layer

`Logic/` contains UI-independent behavior so it can be covered by unit tests.

Current classes include:

- `ProductListFilter` — Product search and sort rules.
- `ProductLookupSelection` — category/storage picker selection mapping.
- `ShoppingListListFilter` — Shopping List search/sort.
- `ShoppingListProductFilter` — open-list product filtering/sorting.
- `ShoppingListPickedUpChange` — distinguishes user checkbox changes from programmatic UI updates.
- `ShoppingListPriceInput` — validates/parses entered prices.
- `ShoppingListTotalsCalculator` — subtotal/tax/total calculation.
- `TaxRateInput` — validates/parses Settings tax percentages.

A new rule should generally live here when it can be expressed without MAUI controls or platform APIs.

## UI layer

Main screens:

- `Pages/ShoppingListsPage` — shopping-trip list.
- `Pages/ShoppingListProductsPage` — products/pickup state for one trip.
- `MainPage` — normal Product catalog and Add Products selection mode.
- `Pages/ProductDetailPage` — add/edit Product form.
- `Pages/SettingsPage` — app-wide sales tax setting.

`PriceEntryPopupPage` still exists in the project, but the active Windows-safe price-entry experience is implemented as an in-page overlay on `ShoppingListProductsPage`, avoiding problematic modal Shell navigation.

## Material UI and Button handler compatibility

The project uses `UraniumUI.Material` 3.0.0.

A Windows crash was traced to UraniumUI's `StatefulButtonHandler` receiving pointer input after MAUI had disconnected its native `PlatformView` during Shell teardown.

For that reason, `MauiProgram.cs` intentionally performs UraniumUI setup first and then re-registers:

```csharp
Microsoft.Maui.Handlers.ButtonHandler
```

for `Microsoft.Maui.Controls.Button`.

That ordering is deliberate. Removing or moving this registration can reintroduce intermittent Windows navigation crashes.

## Startup and development sample data

`App.CreateWindow()` creates `AppShell`.

When `ResetTestDataOnStartup` is enabled, the Window Created event runs `TestDataSeeder.ResetAndSeedAsync()`. Pages can await `App.TestDataReady` so they do not query incomplete sample data during startup.

This startup synchronization is important when changing page loading behavior.

## Local file locations

### Release builds

SQLite lives under:

```text
FileSystem.AppDataDirectory/shopping.db3
```

### Debug builds

`DevelopmentDebugArtifacts` chooses a debug-friendly location. On a normal Windows repository launch it finds the project root and uses:

```text
DebugData/
  shopping.db3
  debug.log
```

On environments where the project root cannot be found, it falls back to an app-data DebugData folder.

## Runtime diagnostics

Development logging is intentionally defensive: failure to write a log must never crash the app.

Current breadcrumbs include:

- app/platform startup;
- window creation;
- Shell navigation start/end;
- page navigation for the Shopping List → Add Products flow;
- Product additions to a Shopping List;
- Shopping List refresh counts;
- CLR unhandled exceptions;
- unobserved Task exceptions;
- WinUI unhandled exceptions and navigation stack state on Windows.

The Windows handler does **not** mark exceptions handled. It records diagnostic information and allows normal debugger/crash behavior to continue.
