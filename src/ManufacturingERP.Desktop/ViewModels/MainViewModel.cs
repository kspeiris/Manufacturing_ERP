using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ManufacturingERP.Application.Services;

namespace ManufacturingERP.Desktop.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    private readonly AuthorizationService _authorizationService;
    private readonly CurrentUserService _currentUserService;
    [ObservableProperty] private object? _currentView;
    [ObservableProperty] private string _statusMessage = string.Empty;

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

    public MainViewModel(CurrentUserService currentUserService, AuthorizationService authorizationService, DashboardViewModel dashboard, ProductsViewModel products, CustomersViewModel customers,
        SuppliersViewModel suppliers, VehiclesViewModel vehicles, WarehousesViewModel warehouses, RoutesViewModel routes,
        VehicleSalesViewModel vehicleSales, PosSalesViewModel posSales, CollectionsViewModel collections, ProcurementViewModel procurement,
        Phase3ProcurementViewModel supplierFinance, SupplierPaymentsViewModel supplierPayments, WarehouseViewModel warehouse,
        ProductionViewModel production, ProductionCostingViewModel productionCosting, MobileSyncViewModel mobileSync,
        AccountingViewModel accounting, LedgersViewModel ledgers, ReportsViewModel reports, ReportViewerViewModel reportViewer,
        UserManagementViewModel users, AuditLogsViewModel auditLogs)
    {
        _currentUserService = currentUserService;
        _authorizationService = authorizationService;
        Dashboard = dashboard; Products = products; Customers = customers; Suppliers = suppliers; Vehicles = vehicles;
        Warehouses = warehouses; Routes = routes; VehicleSales = vehicleSales; PosSales = posSales; Collections = collections;
        Procurement = procurement; SupplierFinance = supplierFinance; SupplierPayments = supplierPayments; Warehouse = warehouse;
        Production = production; ProductionCosting = productionCosting; MobileSync = mobileSync; Accounting = accounting;
        Ledgers = ledgers; Reports = reports; ReportViewer = reportViewer; Users = users; AuditLogs = auditLogs;
        CurrentView = Dashboard;
    }

    [RelayCommand]
    private void ShowDashboard()
    {
        StatusMessage = string.Empty;
        CurrentView = Dashboard;
    }
    [RelayCommand] private void ShowProducts() => NavigateTo(Products, _authorizationService.EnsureSalesAccess());
    [RelayCommand] private void ShowCustomers() => NavigateTo(Customers, _authorizationService.EnsureSalesAccess());
    [RelayCommand] private void ShowSuppliers() => NavigateTo(Suppliers, _authorizationService.EnsureAdminAccess());
    [RelayCommand] private void ShowVehicles() => NavigateTo(Vehicles, _authorizationService.EnsureAdminAccess());
    [RelayCommand] private void ShowWarehouses() => NavigateTo(Warehouses, _authorizationService.EnsureAdminAccess());
    [RelayCommand] private void ShowRoutes() => NavigateTo(Routes, _authorizationService.EnsureAdminAccess());
    [RelayCommand] private void ShowVehicleSales() => NavigateTo(VehicleSales, _authorizationService.EnsureSalesAccess());
    [RelayCommand] private void ShowPosSales() => NavigateTo(PosSales, _authorizationService.EnsureSalesAccess());
    [RelayCommand] private void ShowCollections() => NavigateTo(Collections, _authorizationService.EnsureSalesAccess());
    [RelayCommand] private void ShowProcurement() => NavigateTo(Procurement, _authorizationService.EnsureProcurementAccess());
    [RelayCommand] private void ShowSupplierFinance() => NavigateTo(SupplierFinance, _authorizationService.EnsureProcurementAccess());
    [RelayCommand] private void ShowSupplierPayments() => NavigateTo(SupplierPayments, _authorizationService.EnsureAccountingAccess());
    [RelayCommand] private void ShowWarehouse() => NavigateTo(Warehouse, _authorizationService.EnsureWarehouseAccess());
    [RelayCommand] private void ShowProduction() => NavigateTo(Production, _authorizationService.EnsureProductionAccess());
    [RelayCommand] private void ShowProductionCosting() => NavigateTo(ProductionCosting, _authorizationService.EnsureProductionAccess());
    [RelayCommand] private void ShowMobileSync() => NavigateTo(MobileSync, _authorizationService.EnsureSalesAccess());
    [RelayCommand] private void ShowAccounting() => NavigateTo(Accounting, _authorizationService.EnsureAccountingAccess());
    [RelayCommand] private void ShowLedgers() => NavigateTo(Ledgers, _authorizationService.EnsureLedgersAccess());
    [RelayCommand] private void ShowReports() => NavigateTo(Reports, _authorizationService.EnsureReportsAccess());
    [RelayCommand] private void ShowReportViewer() => NavigateTo(ReportViewer, _authorizationService.EnsureReportsAccess());
    [RelayCommand] private void ShowUsers() => NavigateTo(Users, _authorizationService.EnsureAdminAccess());
    [RelayCommand] private void ShowAuditLogs() => NavigateTo(AuditLogs, _authorizationService.EnsureAdminAccess());

    private void NavigateTo(object targetView, ManufacturingERP.Shared.Results.Result authResult)
    {
        if (!authResult.IsSuccess)
        {
            StatusMessage = authResult.Message;
            return;
        }

        StatusMessage = string.Empty;
        CurrentView = targetView;
    }
}
