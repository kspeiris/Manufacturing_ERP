# EF Core Migrations

This package includes placeholder migration files because the .NET SDK and EF CLI were not available in the build environment used to assemble the ZIP.

After opening the solution on Windows:

```bash
dotnet restore
dotnet ef migrations add Phase3Baseline --project src/ManufacturingERP.Infrastructure --startup-project src/ManufacturingERP.Desktop
dotnet ef database update --project src/ManufacturingERP.Infrastructure --startup-project src/ManufacturingERP.Desktop
```

If you prefer Package Manager Console in Visual Studio:

```powershell
Add-Migration Phase3Baseline -Project ManufacturingERP.Infrastructure -StartupProject ManufacturingERP.Desktop
Update-Database -Project ManufacturingERP.Infrastructure -StartupProject ManufacturingERP.Desktop
```
