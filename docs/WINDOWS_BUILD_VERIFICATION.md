# Windows Build Verification Guide

This environment could not run Visual Studio or the Windows .NET desktop runtime, so final compile verification must be done on your Windows machine.

## Recommended steps
1. Install:
   - Visual Studio 2022
   - .NET 8 SDK
   - Desktop development with C#
   - EF Core CLI tools
2. Open `ManufacturingERP.sln`
3. Restore NuGet packages
4. Regenerate EF Core migrations
5. Build Debug and Release
6. Run smoke tests:
   - login
   - product/customer CRUD
   - supplier/vehicle/warehouse/route CRUD
   - procurement
   - supplier invoice and payment settlement
   - ledger loading
   - report export
7. Run automated tests:
   - `dotnet test`

## Commands
```powershell
dotnet restore
dotnet build ManufacturingERP.sln
dotnet test tests/ManufacturingERP.Tests/ManufacturingERP.Tests.csproj
dotnet ef migrations add Phase5Finalize --project src/ManufacturingERP.Infrastructure --startup-project src/ManufacturingERP.Desktop
dotnet ef database update --project src/ManufacturingERP.Infrastructure --startup-project src/ManufacturingERP.Desktop
```

## Expected manual checks
- Admin user: `admin / admin123`
- Menu visibility changes by role
- Supplier payment updates referenced supplier invoice balance/status
- Export folder contains text and HTML reports
- Report Center lists generated files and templates
