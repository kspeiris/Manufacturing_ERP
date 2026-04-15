dotnet restore
dotnet build ManufacturingERP.sln
dotnet test tests/ManufacturingERP.Tests/ManufacturingERP.Tests.csproj

# Regenerate EF migrations if model changes were made after extraction
dotnet ef migrations add Phase6Finalize --project src/ManufacturingERP.Infrastructure --startup-project src/ManufacturingERP.Desktop
dotnet ef database update --project src/ManufacturingERP.Infrastructure --startup-project src/ManufacturingERP.Desktop
