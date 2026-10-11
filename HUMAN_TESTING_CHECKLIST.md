# Human Testing Checklist

This file tracks the things that should be tested by an actual human using the app.

Automated tests should still be added whenever a feature can be tested reliably in code. This checklist is for behavior that is easier to verify by looking at and using the real app.

## Products

- [ ] Products screen loads without errors.
- [ ] Product cards display correctly in the Material-style layout.
- [ ] Product search filters the list immediately while typing.
- [ ] Product search ignores capitalization.
- [ ] Product search shows a sensible empty state when nothing matches.
- [ ] A → Z sorting works.
- [ ] Z → A sorting works.
- [ ] Search and sorting work together.
- [ ] Add Product button opens the product form.
- [ ] Editing an existing product opens the correct product.
- [ ] Saving a product returns to the Products screen.
- [ ] Newly saved or edited product is scrolled into view when appropriate.
- [ ] Saved-product highlight animation appears and fades normally.
- [ ] Product name spell checking appears when supported by the operating system.
- [ ] Product description spell checking appears when supported by the operating system.
- [ ] Product name and description text prediction behave normally on supported devices.
- [ ] Product Category picker shows all seven built-in categories in the expected order.
- [ ] A new product cannot be saved until the user chooses a category.
- [ ] The Product form shows a read-only Taxable field.
- [ ] Before a category is selected, the Taxable field says "Select a category".
- [ ] Selecting a non-taxable category changes the Taxable field to "No".
- [ ] Selecting a taxable category changes the Taxable field to "Yes".
- [ ] Changing categories updates the Taxable field immediately.
- [ ] The Taxable field cannot be edited directly.
- [ ] Saving a product preserves the selected category when it is edited again.
- [ ] Editing an older uncategorized product lets the user choose and save a category.
- [ ] Basic Grocery and Other categories are stored as non-taxable.
- [ ] Snack Food, Candy / Confectionery, Carbonated Beverage, Household / Non-Food, and Prepared Food are stored as taxable.
- [ ] An existing database upgrades without losing Product or ProductCategory records.
- [ ] Product form shows a Storage Location dropdown.
- [ ] Storage Location dropdown shows the nine built-in locations alphabetically.
- [ ] Storage Location can be left unselected.
- [ ] Saving a product preserves its selected storage location when edited again.
- [ ] Changing a product's storage location persists after returning to the Products screen.
- [ ] Existing products still open normally after the StorageLocationID migration.
- [ ] Sample products include examples from every built-in storage location.
- [ ] Opening the seeded Bathroom Tissue product preselects Household / Non-Food, shows Taxable = Yes, and preselects Bathroom.
- [ ] Opening Product Add/Edit always gives Category and Storage Location dropdown choices after startup test data has finished loading.

## Shopping Lists

- [ ] Shopping Lists is the first tab shown when the app opens.
- [ ] Shopping Lists screen loads without errors.
- [ ] Shopping list cards match the overall Products-screen layout and theme.
- [ ] Shopping date is displayed correctly.
- [ ] Total cost is displayed correctly.
- [ ] A list without a total shows "Total not entered".
- [ ] Picked-up lists show "Picked up".
- [ ] Unfinished lists show "Pending".
- [ ] Shopping-list search filters immediately while typing.
- [ ] Searching for a date finds the expected list.
- [ ] Searching for "picked" finds completed lists only.
- [ ] Searching for "pending" finds unfinished lists only.
- [ ] Newest-first sorting works.
- [ ] Oldest-first sorting works.
- [ ] Search and date sorting work together.
- [ ] Shopping-list empty state looks correct when there are no matches.
- [ ] Add Shopping List button is visible and styled correctly.
- [ ] Add Shopping List button currently shows the placeholder message instead of creating a blank record.

## Test Data

- [ ] With `ResetTestDataOnStartup = true`, development test data appears when the app starts.
- [ ] With `ResetTestDataOnStartup = true`, user-created products are removed after restarting the app.
- [ ] With `ResetTestDataOnStartup = true`, edits made to sample products are replaced by the original sample values after restarting.
- [ ] With `ResetTestDataOnStartup = true`, user-created shopping lists are removed after restarting the app.
- [ ] After each reset, exactly 28 sample products and 12 sample shopping lists are recreated.
- [ ] The pending sample shopping list appears with no total entered.
- [ ] With `ResetTestDataOnStartup = false`, restarting the app preserves existing database changes and does not reload test data.

## Navigation and Tabs

- [ ] Switching between Shopping Lists and Products works repeatedly.
- [ ] Returning to a tab refreshes its data when needed.
- [ ] Bottom tab icons and labels display correctly.
- [ ] No duplicate pages or unexpected navigation stack entries appear after repeated use.

## Windows

- [ ] App launches successfully on Windows.
- [ ] Material styling renders correctly in dark mode.
- [ ] Material styling renders correctly in light mode.
- [ ] Search boxes accept keyboard input normally.
- [ ] Buttons can be clicked with mouse and keyboard.
- [ ] Window resizing does not break the Products layout.
- [ ] Window resizing does not break the Shopping Lists layout.

## Android

- [ ] App launches successfully on Android.
- [ ] Bottom tabs are usable and visually correct.
- [ ] Product search works with the on-screen keyboard.
- [ ] Shopping-list search works with the on-screen keyboard.
- [ ] Spell checking and text prediction behave normally with the device keyboard.
- [ ] Scrolling remains smooth with the sample data loaded.
- [ ] Back navigation behaves normally from the product form.

## Before Merging a Feature

- [ ] Automated tests for the new feature have been added where practical.
- [ ] Automated tests pass.
- [ ] Windows build passes.
- [ ] Relevant items in this human-testing checklist have been verified.
- [ ] No obvious regressions were found in Products or Shopping Lists.

---

Add new items here whenever we build something that needs eyes, fingers, a keyboard, or the glorious unpredictability of a real operating system.
