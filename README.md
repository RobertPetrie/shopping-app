# Shopping App

[![Build Shopping App](https://github.com/RobertPetrie/shopping-app/actions/workflows/build.yml/badge.svg)](https://github.com/RobertPetrie/shopping-app/actions/workflows/build.yml)

Shopping App is a cross-platform .NET MAUI app for keeping a reusable product catalog and using it to build practical shopping lists.

Instead of typing the same groceries and household items every week, you keep products once, organize them by category and where they are stored at home, then add them to shopping trips as needed. While shopping, items can be checked off, the price paid can be recorded, and the app keeps a running subtotal and tax-aware total.

## What it does

- **Reusable product catalog** — add and edit products, descriptions, categories, and home storage locations.
- **Fast product search and sorting** — search as you type and sort alphabetically or by storage location.
- **Shopping lists** — open dated shopping trips, search past lists, and sort them by date.
- **Add products to a list** — reuse the existing catalog instead of retyping item names.
- **Pickup tracking** — check items off while shopping and sort by picked-up status.
- **Price entry** — record the price paid when an item is picked up.
- **Running totals** — subtotal only the items picked up and apply sales tax only to taxable product categories.
- **Configurable sales tax** — change the app-wide tax percentage from Settings.
- **Local-first storage** — app data is kept in a local SQLite database. There is no cloud account or sync service in the current app.

## Current status

Shopping App is under active development.

Windows and Android are the platforms currently exercised most heavily, including automated Windows builds and an Android emulator launch smoke test in GitHub Actions. The project also contains .NET MAUI targets for iOS and Mac Catalyst.

> **Development-data warning:** the current development build has `ResetTestDataOnStartup = true`. Every launch wipes the local database and recreates sample data. Do not use the current development build for important personal data until that setting is disabled.

## Technology

- .NET 10
- .NET MAUI 10.0.110
- C# and XAML
- SQLite via `sqlite-net-pcl`
- UraniumUI Material styling
- xUnit automated tests
- GitHub Actions for build and Android emulator smoke testing

## Technical documentation

The repository keeps detailed design and maintenance notes under [`docs/wiki`](docs/wiki/Home.md):

- [Features and current behavior](docs/wiki/Features-and-Behavior.md)
- [Data model and SQLite design](docs/wiki/Data-Model.md)
- [Application architecture](docs/wiki/Architecture.md)
- [Development, testing, diagnostics, and CI](docs/wiki/Development-and-Testing.md)

The hands-on regression checklist lives in [HUMAN_TESTING_CHECKLIST.md](HUMAN_TESTING_CHECKLIST.md).

## Building the project

The easiest development environment is Visual Studio with the .NET MAUI workload installed. The solution is:

```text
shopping-app.slnx
```

From the command line, install the MAUI workload and restore the project:

```bash
dotnet workload install maui
dotnet restore shopping-app.slnx
```

Run the unit tests with:

```bash
dotnet test ShoppingApp.Tests/ShoppingApp.Tests.csproj
```

Platform builds require the corresponding MAUI platform tooling on the host operating system.

## License

Shopping App is licensed under the [MIT License](LICENSE).
