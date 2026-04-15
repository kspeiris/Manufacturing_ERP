# ManufacturingERP Desktop Solution

A substantial starter ERP desktop application for a manufacturing company with warehouse, manufacturing, route distribution, van sales, collections, and accounting foundations.

## Important
This package is a strong architectural starter, not a fully finished enterprise ERP. It includes:
- WPF + MVVM desktop shell
- Professional dark/light themed UI structure
- SQLite-ready Entity Framework Core setup
- Layered architecture
- Seed data and core entities
- Module navigation and dashboard
- Sample CRUD patterns
- Core services for inventory and van sales workflows

## Projects
- ManufacturingERP.Desktop - WPF UI
- ManufacturingERP.Application - services, DTOs, view-facing logic
- ManufacturingERP.Domain - entities, enums, abstractions
- ManufacturingERP.Infrastructure - EF Core DbContext, repositories, seeders
- ManufacturingERP.Shared - helpers and constants

## Recommended Setup
1. Install .NET 8 SDK on Windows
2. Open `ManufacturingERP.sln`
3. Restore NuGet packages
4. Set `ManufacturingERP.Desktop` as startup project
5. Build and run

## NuGet packages to restore
- Microsoft.EntityFrameworkCore
- Microsoft.EntityFrameworkCore.Sqlite
- Microsoft.EntityFrameworkCore.Design
- CommunityToolkit.Mvvm
- MaterialDesignThemes
- MaterialDesignColors

## Next Steps
- Add full CRUD windows for every master and transaction module
- Expand reporting layer
- Add authentication hashing and audit trail persistence
- Add printing and export services
- Add full accounting postings and payroll


## Phase 2 additions
This package now includes expanded ERP foundations for:
- Procurement
- Warehouse transactions
- Production order receipt
- Accounting journals
- Reporting workspace

See `docs/PHASE2_STATUS.md` for details.


## Phase 3 additions
This package now includes:
- CRUD dialogs for Products and Customers
- Customer Collections workspace
- Supplier Invoices and Purchase Returns
- User Management and Audit Logs
- Plain-text report export helpers
- EF Core migration placeholders


## Phase 6 additions
This package now includes:
- command-level authorization checks for key CRUD and finance actions
- date-filtered ledger detail reporting
- customer and supplier statement HTML printing
- embedded report preview for exported HTML files
- FastReport-ready package references


## Phase 7 additions
- POS-style cash sales screen
- dashboard analytics KPIs
- mobile sync workspace
- production costing workspace
- expanded accounting master scaffolding
