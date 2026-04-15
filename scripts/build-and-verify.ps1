dotnet restore
dotnet build ManufacturingERP.sln
dotnet test tests/ManufacturingERP.Tests/ManufacturingERP.Tests.csproj
dotnet ef migrations add Phase5Finalize --project src/ManufacturingERP.Infrastructure --startup-project src/ManufacturingERP.Desktop
dotnet ef database update --project src/ManufacturingERP.Infrastructure --startup-project src/ManufacturingERP.Desktop
