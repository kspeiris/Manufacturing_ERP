using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ManufacturingERP.Application.Services;

namespace ManufacturingERP.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly CurrentUserService _currentUserService;
    [ObservableProperty] private object? _currentView;

    public DashboardViewModel Dashboard { get; }
    public ProductsViewModel Products { get; }
    public CustomersViewModel Customers { get; }
    public SuppliersViewModel Suppliers { get; }
    public VehiclesViewModel Vehicles { get; }
    public WarehousesViewModel Warehouses { get; }
    public RoutesViewModel Routes { get; }
    public VehicleSalesViewModel VehicleSales { get; }
    public PosSalesViewModel PosSales { get; }
    public CollectionsViewModel Collections { get; }
    public ProcurementViewModel Procurement { get; }
    public Phase3ProcurementViewModel SupplierFinance { get; }
    public SupplierPaymentsViewModel SupplierPayments { get; }
    public WarehouseViewModel Warehouse { get; }
    public ProductionViewModel Production { get; }
    public ProductionCostingViewModel ProductionCosting { get; }
    public MobileSyncViewModel MobileSync { get; }
    public AccountingViewModel Accounting { get; }
    public LedgersViewModel Ledgers { get; }
    public ReportsViewModel Reports { get; }
    public ReportViewerViewModel ReportViewer { get; }
    public UserManagementViewModel Users { get; }
    public AuditLogsViewModel AuditLogs { get; }

    public string CurrentUserDisplay => _currentUserService.CurrentUser is null ? "Guest" : $"{_currentUserService.CurrentUser.FullName} ({_currentUserService.CurrentUser.Role})";
    public bool CanManageAdmin => _currentUserService.IsInRole("Admin", "Manager");
    public bool CanManageAccounting => _currentUserService.IsInRole("Admin", "Manager", "Accounts");
    public bool CanManageWarehouse => _currentUserService.IsInRole("Admin", "Manager", "Warehouse");
    public bool CanManageSales => _currentUserService.IsInRole("Admin", "Manager", "Sales", "Accounts");
    public bool CanManageProduction => _currentUserService.IsInRole("Admin", "Manager", "Production");
    public bool CanViewReports => _currentUserService.CurrentUser is not null;

    public MainViewModel(CurrentUserService currentUserService, DashboardViewModel dashboard, ProductsViewModel products, CustomersViewModel customers,
        SuppliersViewModel suppliers, VehiclesViewModel vehicles, WarehousesViewModel warehouses, RoutesViewModel routes,
        VehicleSalesViewModel vehicleSales, PosSalesViewModel posSales, CollectionsViewModel collections, ProcurementViewModel procurement,
        Phase3ProcurementViewModel supplierFinance, SupplierPaymentsViewModel supplierPayments, WarehouseViewModel warehouse,
        ProductionViewModel production, ProductionCostingViewModel productionCosting, MobileSyncViewModel mobileSync,
        AccountingViewModel accounting, LedgersViewModel ledgers, ReportsViewModel reports, ReportViewerViewModel reportViewer,
        UserManagementViewModel users, AuditLogsViewModel auditLogs)
    {
        _currentUserService = currentUserService;
        Dashboard = dashboard; Products = products; Customers = customers; Suppliers = suppliers; Vehicles = vehicles;
        Warehouses = warehouses; Routes = routes; VehicleSales = vehicleSales; PosSales = posSales; Collections = collections;
        Procurement = procurement; SupplierFinance = supplierFinance; SupplierPayments = supplierPayments; Warehouse = warehouse;
        Production = production; ProductionCosting = productionCosting; MobileSync = mobileSync; Accounting = accounting;
        Ledgers = ledgers; Reports = reports; ReportViewer = reportViewer; Users = users; AuditLogs = auditLogs;
        CurrentView = Dashboard;
    }

    [RelayCommand] private void ShowDashboard() => CurrentView = Dashboard;
    [RelayCommand] private void ShowProducts() => CurrentView = Products;
    [RelayCommand] private void ShowCustomers() => CurrentView = Customers;
    [RelayCommand] private void ShowSuppliers() => CurrentView = Suppliers;
    [RelayCommand] private void ShowVehicles() => CurrentView = Vehicles;
    [RelayCommand] private void ShowWarehouses() => CurrentView = Warehouses;
    [RelayCommand] private void ShowRoutes() => CurrentView = Routes;
    [RelayCommand] private void ShowVehicleSales() => CurrentView = VehicleSales;
    [RelayCommand] private void ShowPosSales() => CurrentView = PosSales;
    [RelayCommand] private void ShowCollections() => CurrentView = Collections;
    [RelayCommand] private void ShowProcurement() => CurrentView = Procurement;
    [RelayCommand] private void ShowSupplierFinance() => CurrentView = SupplierFinance;
    [RelayCommand] private void ShowSupplierPayments() => CurrentView = SupplierPayments;
    [RelayCommand] private void ShowWarehouse() => CurrentView = Warehouse;
    [RelayCommand] private void ShowProduction() => CurrentView = Production;
    [RelayCommand] private void ShowProductionCosting() => CurrentView = ProductionCosting;
    [RelayCommand] private void ShowMobileSync() => CurrentView = MobileSync;
    [RelayCommand] private void ShowAccounting() => CurrentView = Accounting;
    [RelayCommand] private void ShowLedgers() => CurrentView = Ledgers;
    [RelayCommand] private void ShowReports() => CurrentView = Reports;
    [RelayCommand] private void ShowReportViewer() => CurrentView = ReportViewer;
    [RelayCommand] private void ShowUsers() => CurrentView = Users;
    [RelayCommand] private void ShowAuditLogs() => CurrentView = AuditLogs;
}
