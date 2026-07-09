using ManufacturingERP.Infrastructure;
using ManufacturingERP.Infrastructure.Persistence;
using ManufacturingERP.Shared.Constants;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Serilog;

namespace ManufacturingERP.Desktop;

public partial class App : System.Windows.Application
{
    public static IServiceProvider Services { get; private set; } = default!;
    private IServiceScope? _loginWindowScope;
    private IServiceScope? _mainWindowScope;

    protected override async void OnStartup(System.Windows.StartupEventArgs e)
    {
        base.OnStartup(e);
        ShutdownMode = System.Windows.ShutdownMode.OnExplicitShutdown;

        // Bootstrap Serilog before DI so startup errors are captured.
        var logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "logs");
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.Debug(outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}")
            .WriteTo.File(
                path: Path.Combine(logDirectory, "app-.log"),
                rollingInterval: RollingInterval.Day,
                retainedFileCountLimit: 7,
                outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
            .CreateLogger();

        Log.Information("ManufacturingERP starting up");

        try
        {
            var serviceCollection = new ServiceCollection();
            var appBaseDirectory = AppDomain.CurrentDomain.BaseDirectory;
            var databasePath = Path.Combine(appBaseDirectory, AppConstants.DatabaseFileName);
            serviceCollection.AddInfrastructure(appBaseDirectory);

            // Wire Serilog into Microsoft.Extensions.Logging (ILogger<T>).
            serviceCollection.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddSerilog(dispose: true);
            });

            RegisterDesktopServices(serviceCollection);

            Services = serviceCollection.BuildServiceProvider(new ServiceProviderOptions
            {
                ValidateOnBuild = true,
                ValidateScopes = true
            });

            EnsureValidSqliteDatabase(databasePath);

            using var scope = Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            await db.Database.MigrateAsync();
            await DbSeeder.SeedAsync(db);

            Log.Information("Database migration and seeding complete");
            ShowLoginWindow();
        }
        catch (Exception ex)
        {
            Log.Fatal(ex, "Fatal error during startup");
            System.Windows.MessageBox.Show(
                ex.ToString(),
                "Startup Error",
                System.Windows.MessageBoxButton.OK,
                System.Windows.MessageBoxImage.Error);
            Shutdown(-1);
        }
    }

    protected override void OnExit(System.Windows.ExitEventArgs e)
    {
        _loginWindowScope?.Dispose();
        _mainWindowScope?.Dispose();

        if (Services is IDisposable disposable)
            disposable.Dispose();

        Log.Information("ManufacturingERP shutting down");
        Log.CloseAndFlush();

        base.OnExit(e);
    }

    public static void OpenMainWindow()
    {
        ((App)Current).OpenMainWindowInternal();
    }

    private static void RegisterDesktopServices(IServiceCollection serviceCollection)
    {
        serviceCollection.AddScoped<Views.LoginWindow>();
        serviceCollection.AddScoped<Views.MainWindow>();

        serviceCollection.AddScoped<ViewModels.LoginViewModel>();
        serviceCollection.AddScoped<ViewModels.MainViewModel>();
        serviceCollection.AddScoped<ViewModels.DashboardViewModel>();
        serviceCollection.AddScoped<ViewModels.AnalyticsViewModel>();
        serviceCollection.AddScoped<ViewModels.ProductsViewModel>();
        serviceCollection.AddScoped<ViewModels.CustomersViewModel>();
        serviceCollection.AddScoped<ViewModels.SuppliersViewModel>();
        serviceCollection.AddScoped<ViewModels.VehiclesViewModel>();
        serviceCollection.AddScoped<ViewModels.WarehousesViewModel>();
        serviceCollection.AddScoped<ViewModels.RoutesViewModel>();
        serviceCollection.AddScoped<ViewModels.VehicleSalesViewModel>();
        serviceCollection.AddScoped<ViewModels.PosSalesViewModel>();
        serviceCollection.AddScoped<ViewModels.CollectionsViewModel>();
        serviceCollection.AddScoped<ViewModels.ProcurementViewModel>();
        serviceCollection.AddScoped<ViewModels.Phase3ProcurementViewModel>();
        serviceCollection.AddScoped<ViewModels.SupplierPaymentsViewModel>();
        serviceCollection.AddScoped<ViewModels.WarehouseViewModel>();
        serviceCollection.AddScoped<ViewModels.ProductionViewModel>();
        serviceCollection.AddScoped<ViewModels.ProductionCostingViewModel>();
        serviceCollection.AddScoped<ViewModels.MobileSyncViewModel>();
        serviceCollection.AddScoped<ViewModels.AccountingViewModel>();
        serviceCollection.AddScoped<ViewModels.AccountingSetupViewModel>();
        serviceCollection.AddScoped<ViewModels.LedgersViewModel>();
        serviceCollection.AddScoped<ViewModels.InventoryAccuracyViewModel>();
        serviceCollection.AddScoped<ViewModels.DatabaseExplorerViewModel>();
        serviceCollection.AddScoped<ViewModels.ReportsViewModel>();
        serviceCollection.AddScoped<ViewModels.ReportViewerViewModel>();
        serviceCollection.AddScoped<ViewModels.UserManagementViewModel>();
        serviceCollection.AddScoped<ViewModels.AuditLogsViewModel>();
    }

    private void ShowLoginWindow()
    {
        _loginWindowScope?.Dispose();
        _loginWindowScope = Services.CreateScope();

        var loginWindow = _loginWindowScope.ServiceProvider.GetRequiredService<Views.LoginWindow>();
        MainWindow = loginWindow;
        ShutdownMode = System.Windows.ShutdownMode.OnMainWindowClose;
        loginWindow.Show();
    }

    private void OpenMainWindowInternal()
    {
        _mainWindowScope?.Dispose();
        _mainWindowScope = Services.CreateScope();

        var mainWindow = _mainWindowScope.ServiceProvider.GetRequiredService<Views.MainWindow>();
        MainWindow = mainWindow;
        ShutdownMode = System.Windows.ShutdownMode.OnMainWindowClose;
        mainWindow.Show();

        _loginWindowScope?.Dispose();
        _loginWindowScope = null;
    }

    private static void EnsureValidSqliteDatabase(string databasePath)
    {
        if (!File.Exists(databasePath))
            return;

        try
        {
            using var connection = new SqliteConnection($"Data Source={databasePath}");
            connection.Open();

            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT COUNT(1)
                FROM sqlite_master
                WHERE type = 'table' AND name = 'Users';
                """;

            var hasUsersTable = Convert.ToInt32(command.ExecuteScalar()) > 0;
            if (hasUsersTable)
                return;
        }
        catch
        {
            // Rebuild corrupted or partially created first-run databases.
        }

        DeleteSqliteFiles(databasePath);
    }

    private static void DeleteSqliteFiles(string databasePath)
    {
        var relatedPaths = new[]
        {
            databasePath,
            $"{databasePath}-shm",
            $"{databasePath}-wal"
        };

        foreach (var path in relatedPaths)
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }
}
