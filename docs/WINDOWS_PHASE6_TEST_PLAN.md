# Windows Phase 6 Test Plan

## Build
1. Open `ManufacturingERP.sln`
2. Restore NuGet packages
3. Build Debug
4. Build Release
5. Run automated tests:
   - `dotnet test`

## Functional verification
### Security
- Login as Admin, Manager, Accounts, Sales, Production, Warehouse
- Confirm menu visibility changes by role
- Attempt restricted actions and confirm command-level denial messages

### Master Data CRUD
- Products
- Customers
- Suppliers
- Vehicles
- Warehouses
- Routes

### Finance
- Create supplier invoice
- Create supplier payment referencing that invoice
- Confirm `PaidAmount`, `BalanceAmount`, and `Status` update
- Load customer and supplier ledgers with date filters
- Export customer/supplier statements

### Reporting
- Export sales register
- Export purchase register
- Export invoice HTML
- Export purchase order HTML
- Open Report Center and preview HTML exports

## Compile-fix pass
If build errors occur:
- fix package restore
- fix missing namespaces/usings
- regenerate EF migrations
- re-test after each fix
