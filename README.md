# shopping-app

## Database structure

The app stores its data locally in `shopping.db3`, in the device's app data directory. A shopping list can contain many products, and a product can appear in many shopping lists. `ShoppingListProduct` links them and records the price for each product in that list.

### Entity relationship diagram

The diagram uses logical types; SQLite storage details are noted below.

```mermaid
erDiagram
    Shopping_List ||--o{ ShoppingListProduct : contains
    Product ||--o{ ShoppingListProduct : appears_in

    Product {
        int ID PK "Auto-generated"
        string Name UK "Required"
        string Description "Nullable"
        datetime CreatedOn "Required, UTC"
        datetime ModifiedOn "Nullable"
    }

    Shopping_List {
        int ID PK "Auto-generated"
        date ShoppingDate "Nullable"
        datetime CreatedOn "Required, UTC"
        datetime ModifiedOn "Nullable"
        decimal TotalCost "Nullable"
        boolean PickedUp "Required"
    }

    ShoppingListProduct {
        int ShoppingListID PK, FK "References Shopping_List.ID"
        int ProductID PK, FK "References Product.ID"
        decimal Price "Required"
        datetime CreatedOn "Required, UTC"
    }
```

### Product

Catalog of products available to add to shopping lists.

| Column | Logical type | Nullable | Rules |
| --- | --- | --- | --- |
| `ID` | Integer | No | Auto-generated primary key. |
| `Name` | String | No | Unique; comparison is case-sensitive. The insertion method trims names and rejects blank values. |
| `Description` | String | Yes | Optional description. |
| `CreatedOn` | DateTime | No | Set to UTC by the app's insertion method. |
| `ModifiedOn` | DateTime | Yes | Initially null; update logic is not implemented yet. |

### Shopping_List

A shopping list with an optional shopping date and total cost.

| Column | Logical type | Nullable | Rules |
| --- | --- | --- | --- |
| `ID` | Integer | No | Auto-generated primary key. |
| `ShoppingDate` | Date | Yes | Calendar date only, with no time component. |
| `CreatedOn` | DateTime | No | Set to UTC by the app's insertion method. |
| `ModifiedOn` | DateTime | Yes | Initially null; update logic is not implemented yet. |
| `TotalCost` | Decimal currency | Yes | Amount with at most two decimal places. Not automatically calculated from linked product prices yet. |
| `PickedUp` | Boolean | No | Defaults to `false` in the app. |

### ShoppingListProduct

Links an existing product to an existing shopping list, with its price for that list.

| Column | Logical type | Nullable | Rules |
| --- | --- | --- | --- |
| `ShoppingListID` | Integer | No | Part of the composite primary key; foreign key to `Shopping_List.ID`. |
| `ProductID` | Integer | No | Part of the composite primary key; foreign key to `Product.ID`. |
| `Price` | Decimal currency | No | Amount with at most two decimal places. |
| `CreatedOn` | DateTime | No | Set to UTC by the app's insertion method. |

The primary key is `(ShoppingListID, ProductID)`, so a product can appear only once per shopping list. No separate ID column is needed. The same product can have different prices in different lists.

Both foreign keys use `ON DELETE RESTRICT` and `ON UPDATE RESTRICT`: linked entries must be removed before deleting a referenced parent, and referenced parent IDs cannot be changed while links exist. An additional index on `ProductID` supports lookups and foreign-key checks.

### SQLite storage details

- `Price` and `TotalCost` are stored as integer cents (for example, `12.34` becomes `1234`) and exposed as `decimal` values in C#. The model setters reject amounts with more than two decimal places.
- `ShoppingDate` is stored as `yyyy-MM-dd` text and exposed as `DateOnly?` in C#.
- DateTime values are stored as .NET ticks. Insertion methods populate `CreatedOn` using `DateTime.UtcNow`; this is application behavior, not a database default or trigger.
- `PickedUp` is stored as `0` or `1`. Its initial `false` value is supplied by the app, not a database default.
- Date and currency convenience properties marked `[Ignore]` do not create extra columns.
- Tables are initialized on first database access. Foreign-key enforcement is enabled for the shared connection. Future changes to existing constraints require schema migrations.
