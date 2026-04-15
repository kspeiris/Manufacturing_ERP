# Phase 6 Package Status

This package adds:
- command-level authorization checks across major CRUD and save actions
- full ledger detail loading with date filters
- customer/supplier statement printing (HTML export)
- embedded Report Center preview for HTML exports
- FastReport-ready desktop package references
- updated compile-fix and Windows verification notes

## What changed specifically
- authorization checks added to key master-data CRUD actions
- authorization checks added to ledger and supplier-payment finance actions
- supplier payments now settle referenced supplier invoice balances/status
- ledger detail screens now support from/to date filtering
- statement exports generate HTML files for customer and supplier ledgers
- report center now previews exported HTML reports in-app using WPF WebBrowser

## Honest limitation
I could not perform the final compile-fix pass on Windows from this environment. The project still needs real Windows-side build verification in Visual Studio or with the .NET 8 SDK.
