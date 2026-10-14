# Features and Current Behavior

## Product catalog

The Products tab is the reusable catalog behind shopping lists.

Current product behavior includes:

- Add and edit products.
- Required unique product names.
- Optional descriptions.
- Product Category selection.
- Optional Storage Location selection.
- Search while typing.
- A → Z and Z → A sorting.
- Storage Location sorting, with product-name sorting inside each location.
- Product-detail validation before saving.

A Product also has an optional `ImagePath` field in the data model. The database field exists, but product photo capture/storage/display is not implemented yet.

## Product categories

Seven built-in categories are inserted into every database:

| Sort | Category | Taxable by default |
| ---: | --- | :---: |
| 1 | Basic Grocery | No |
| 2 | Snack Food | Yes |
| 3 | Candy / Confectionery | Yes |
| 4 | Carbonated Beverage | Yes |
| 5 | Household / Non-Food | Yes |
| 6 | Prepared Food | Yes |
| 7 | Other | No |

Taxability belongs to the category, not the individual Product row.

The Product form requires a category for newly saved products. Older databases may contain products with no category because `ProductCategoryID` is nullable for migration compatibility.

There is not yet a user-facing category-management screen.

## Storage locations

Nine built-in home storage locations are available:

- Pantry
- Refrigerator
- Freezer
- Kitchen Cabinet
- Bathroom
- Laundry Room
- Basement
- Garage
- Other

The Product form displays them alphabetically. Storage Location is optional, and older products can remain unassigned.

There is not yet a user-facing storage-location management screen.

## Shopping lists

Shopping Lists is the first tab shown at startup.

The list screen currently supports:

- Dated shopping-list records.
- Display of saved total and pickup status.
- Search by the information displayed on a list.
- Newest-first and oldest-first sorting.
- Opening a list to see its products.

**Add Shopping List** opens a date popup (MM/dd/yyyy, initially today's local date) and then goes straight to **Add Products**. The first selected product saves the list and its first item in one database transaction; canceling before that selection creates no empty record. Each later product selection returns to the list so the user can add items gradually. The planned date can be edited by tapping it at the top of an existing shopping list.

## Shopping-list products

A shopping list contains references to existing Products through `ShoppingListProduct`.

On an open shopping list, the user can:

- Search products by name.
- Sort A → Z.
- Sort Z → A.
- Sort by pickup status, with unpicked items first.
- Open **Add Products** and choose one catalog product at a time, returning automatically to the list after each addition.
- See products already on the list as **Added** so duplicates cannot be inserted.
- Mark an item picked up.
- Enter the price paid when checking an item.
- Uncheck an item without losing its previously entered price.

Newly added list items start with:

- `PickedUp = false`
- `Price = 0`

## Subtotal, tax, and total

Running totals are intentionally based on what has actually been picked up.

- **Subtotal** = prices of picked-up items only.
- **Tax** = current app tax rate applied only to picked-up products whose Product Category has `IsTaxable = true`.
- **Total** = subtotal + tax.
- The calculated total is persisted back to `Shopping_List.TotalCost`.

Changing the tax rate causes existing shopping-list totals to be recalculated using the current picked-up items and category taxability.

## Settings

The Settings tab currently exposes the app-wide sales-tax percentage.

Development sample data seeds one TaxRate row named **Ontario HST** at **13%**. The database design is not tied to Ontario; the percentage can be changed in the app.

## Local data

There is currently no account system, cloud database, or sync service. SQLite is the source of truth on the device.

Release builds store `shopping.db3` in the app's private data directory. Debug builds use the development debug-data location described in [Development and Testing](Development-and-Testing.md).

## Important development behavior

`App.xaml.cs` now sets `ResetTestDataOnStartup = false` by default. Lists and catalog edits survive restarts, including Debug runs. Developers may temporarily enable test-data resetting for isolated manual testing, but **doing so deletes stored app records on launch**.

## UI compatibility note

UraniumUI 3.0.0 installs a custom `StatefulButtonHandler`. During Windows Shell page teardown it could receive pointer input after its native `PlatformView` had already been disconnected, producing intermittent `PlatformView cannot be null` crashes.

`MauiProgram.cs` deliberately re-registers MAUI's stock `ButtonHandler` **after** UraniumUI setup. Do not remove that override without repeating the Windows Add Products → Back → Sort navigation tests.
