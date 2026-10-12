# Shopping App Technical Notes

This folder is the technical knowledge base for Shopping App. It keeps implementation details out of the public-facing README while preserving the information needed to understand, maintain, test, and extend the application.

## Pages

- [Features and Current Behavior](Features-and-Behavior.md) — what the app does today, important rules, built-in lookup data, and current limitations.
- [Data Model](Data-Model.md) — SQLite tables, relationships, storage formats, indexes, migration behavior, and tax/total rules.
- [Architecture](Architecture.md) — MAUI Shell structure, pages, data access, business logic, navigation, UI framework notes, and diagnostics.
- [Development and Testing](Development-and-Testing.md) — local development, test-data behavior, automated tests, GitHub Actions, Android smoke testing, debug files, and manual regression testing.

## Related repository documents

- [`README.md`](../../README.md) — public project overview.
- [`HUMAN_TESTING_CHECKLIST.md`](../../HUMAN_TESTING_CHECKLIST.md) — detailed manual regression checklist.
- [`shopping-app.csproj`](../../shopping-app.csproj) — target frameworks, packages, and MAUI configuration.
- [`Data/AppDatabase.cs`](../../Data/AppDatabase.cs) — database initialization and persistence behavior.
- [`Data/TestDataSeeder.cs`](../../Data/TestDataSeeder.cs) — development sample data.
- [`Logic/`](../../Logic) — testable non-UI rules.
- [`ShoppingApp.Tests/`](../../ShoppingApp.Tests) — automated xUnit coverage.

## Documentation rule

When a feature changes the database, navigation model, CI behavior, or another architectural assumption, update the appropriate page here in the same change. User-facing feature changes should also be reflected in the README when they materially change what the app can do.
