# Development and Testing

## Development prerequisites

The project uses .NET 10 and .NET MAUI.

Typical requirements:

- .NET 10 SDK
- MAUI workload
- Visual Studio with MAUI tooling for Windows development
- Android SDK/emulator tooling for Android development
- Java 17 for the Android CI path

Restore the solution with:

```bash
dotnet workload install maui
dotnet restore shopping-app.slnx
```

## Development-data warning

The current app source has:

```csharp
private const bool ResetTestDataOnStartup = true;
```

This is intentionally convenient for repeatable development testing but destructive for real data.

With it enabled, each launch:

1. deletes ShoppingListProduct rows;
2. deletes Products and Shopping Lists;
3. deletes lookup/tax rows;
4. resets SQLite autoincrement counters;
5. restores built-in lookup rows;
6. inserts the development sample dataset;
7. recalculates saved Shopping List totals.

Before the app is used for persistent personal data, set this to `false` and test the non-reset startup path.

## Sample dataset

The current development seeder creates a repeatable set used by manual testing:

- 7 Product Categories
- 9 Storage Locations
- 1 TaxRate (Ontario HST at 13%)
- 28 sample Products
- 12 sample Shopping Lists
- 8 ShoppingListProduct rows per seeded Shopping List

The pending October 11, 2026 list intentionally contains both checked and unchecked examples so pickup, price, and totals behavior can be tested quickly.

## Unit tests

xUnit tests live in:

```text
ShoppingApp.Tests/
```

Run them with:

```bash
dotnet test ShoppingApp.Tests/ShoppingApp.Tests.csproj
```

Coverage currently includes areas such as:

- Product database behavior and schema compatibility;
- product category rules;
- Product search/sorting;
- lookup selection;
- Shopping List search/sorting;
- ShoppingListProduct persistence and selection;
- price parsing/validation;
- pickup-change behavior;
- subtotal/tax/total calculations;
- TaxRate behavior;
- test-data seeding;
- development debug-artifact paths/reset behavior.

## Windows UI-test project

`ShoppingApp.UITests.Windows/` exists as the starting point for Appium-based Windows UI testing. It is currently a placeholder rather than a complete automated end-to-end suite.

Use the human checklist for Windows UI behavior until meaningful Appium tests are added.

## Human regression testing

`HUMAN_TESTING_CHECKLIST.md` is the source of truth for behavior that benefits from a real UI, operating system, keyboard/mouse/touch input, or visual inspection.

Feature work should add automated tests where practical **and** add checklist coverage for OS/UI behavior that cannot be reliably unit tested.

## GitHub Actions

The repository currently contains three workflows.

### Build Shopping App

`.github/workflows/build.yml`

Primary CI pipeline. It runs the automated test/build gates and, on the main integration path, calls the reusable Android emulator smoke test after the earlier gates succeed.

### Build Android APK

`.github/workflows/android-apk.yml`

Manual workflow that produces an installable Android APK artifact for phone testing.

### Test Android Emulator

`.github/workflows/android-emulator-smoke-test.yml`

Can be started manually and can also be called by another workflow.

Current smoke-test behavior:

1. install MAUI Android tooling;
2. create a temporary Android debug keystore;
3. publish a self-contained Debug APK with Fast Deployment disabled;
4. select only the signed APK;
5. boot an API 35 x86_64 Pixel 6 emulator with KVM acceleration;
6. wait until ADB and Android report a completed boot;
7. verify the APK signature with `apksigner`;
8. install the APK;
9. resolve and launch the app's launcher Activity;
10. wait for MAUI startup;
11. capture screenshot, logcat, process/package/activity diagnostics, and app debug log when available;
12. verify the shopping app is still the foreground activity.

A passing smoke test proves the app installs, launches, and survives startup on the Android emulator. It does **not** yet click through the application.

GitHub Issue #28 tracks the next step: automated Android UI navigation through core Shopping List/Product flows.

## Debug artifacts

`Data/DevelopmentDebugArtifacts.cs` manages the development database and runtime log.

On a normal Windows repository Debug run:

```text
DebugData/shopping.db3
DebugData/debug.log
```

`debug.log` is reset at app startup so a failure report contains one run's breadcrumbs.

When investigating a navigation or runtime crash, preserve the log from the failing run before launching the app again.

## Android smoke-test artifacts

The emulator workflow uploads `android-emulator-smoke-test-results` when diagnostics exist. Depending on how far execution reached, it can contain:

- `app-after-launch.png`
- `logcat.txt`
- `app-debug.log`
- `process-list.txt`
- `package-dumpsys.txt`
- `dumpsys-activities-head.txt`
- `resumed-activity.txt`

These files are preferable to guessing from the red/green workflow summary when the emulator test fails.

## Merge discipline

For feature changes:

- add or update automated tests for testable logic;
- build the affected platform;
- run the relevant Human Testing Checklist items;
- keep schema changes backward-compatible or add an explicit migration;
- update these technical notes when an architectural assumption changes;
- avoid unnecessary GitHub Actions runs when local validation is sufficient.
