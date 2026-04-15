param(
    [string]$MigrationName = "Phase3Baseline"
)

dotnet restore
dotnet ef migrations add $MigrationName --project src/ManufacturingERP.Infrastructure --startup-project src/ManufacturingERP.Desktop
dotnet ef database update --project src/ManufacturingERP.Infrastructure --startup-project src/ManufacturingERP.Desktop
