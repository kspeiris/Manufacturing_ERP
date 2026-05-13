using ManufacturingERP.Application.Abstractions;
using ManufacturingERP.Application.Services;
using ManufacturingERP.Infrastructure.Persistence;
using ManufacturingERP.Shared.Constants;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace ManufacturingERP.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string dbPath)
    {
        var databasePath = Path.Combine(dbPath, AppConstants.DatabaseFileName);
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite($"Data Source={databasePath}"));

        services.AddScoped<IAppDbContext>(provider => provider.GetRequiredService<AppDbContext>());
        services.AddSingleton<CurrentUserService>();
        services.AddSingleton<PasswordHasherService>();
        services.AddSingleton(new DatabaseBackupService(databasePath));
        services.AddSingleton<MasterDataValidationService>();
        services.AddSingleton<AuthorizationService>();
        services.AddScoped<AuditService>();
        services.AddScoped<DashboardService>();
        services.AddScoped<InventoryService>();
        services.AddScoped<SalesService>();
        services.AddScoped<WarehouseService>();
        services.AddScoped<ProcurementService>();
        services.AddScoped<Phase3ProcurementService>();
        services.AddScoped<ProductionService>();
        services.AddScoped<AccountingService>();
        services.AddScoped<ReportingService>();
        services.AddScoped<UserManagementService>();
        services.AddScoped<PrintingService>();
        services.AddScoped<SupplierPaymentService>();
        services.AddScoped<LedgerService>();
        services.AddScoped<AnalyticsService>();
        services.AddScoped<PosSalesService>();
        services.AddScoped<MobileSyncService>();
        return services;
    }
}
