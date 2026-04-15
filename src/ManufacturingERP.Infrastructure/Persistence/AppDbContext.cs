using ManufacturingERP.Application.Abstractions;
using ManufacturingERP.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ManufacturingERP.Infrastructure.Persistence;

public class AppDbContext : DbContext, IAppDbContext
{
    public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }

    public DbSet<User> Users => Set<User>();
    public DbSet<ProductCategory> ProductCategories => Set<ProductCategory>();
    public DbSet<Product> Products => Set<Product>();
    public DbSet<Customer> Customers => Set<Customer>();
    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<RoutePlan> RoutePlans => Set<RoutePlan>();
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Warehouse> Warehouses => Set<Warehouse>();
    public DbSet<StockBalance> StockBalances => Set<StockBalance>();
    public DbSet<WarehouseTransaction> WarehouseTransactions => Set<WarehouseTransaction>();
    public DbSet<VehicleLoad> VehicleLoads => Set<VehicleLoad>();
    public DbSet<VehicleLoadItem> VehicleLoadItems => Set<VehicleLoadItem>();
    public DbSet<SalesInvoice> SalesInvoices => Set<SalesInvoice>();
    public DbSet<SalesInvoiceItem> SalesInvoiceItems => Set<SalesInvoiceItem>();
    public DbSet<CollectionEntry> CollectionEntries => Set<CollectionEntry>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
    public DbSet<GoodsReceipt> GoodsReceipts => Set<GoodsReceipt>();
    public DbSet<GoodsReceiptItem> GoodsReceiptItems => Set<GoodsReceiptItem>();
    public DbSet<SupplierInvoice> SupplierInvoices => Set<SupplierInvoice>();
    public DbSet<PurchaseReturn> PurchaseReturns => Set<PurchaseReturn>();
    public DbSet<PurchaseReturnItem> PurchaseReturnItems => Set<PurchaseReturnItem>();
    public DbSet<SupplierPayment> SupplierPayments => Set<SupplierPayment>();
    public DbSet<Account> Accounts => Set<Account>();
    public DbSet<SyncLog> SyncLogs => Set<SyncLog>();
    public DbSet<BomHeader> BomHeaders => Set<BomHeader>();
    public DbSet<BomLine> BomLines => Set<BomLine>();
    public DbSet<ProductionOrder> ProductionOrders => Set<ProductionOrder>();
    public DbSet<ProductionMaterialIssue> ProductionMaterialIssues => Set<ProductionMaterialIssue>();
    public DbSet<ExpenseEntry> ExpenseEntries => Set<ExpenseEntry>();
    public DbSet<JournalEntry> JournalEntries => Set<JournalEntry>();
    public DbSet<JournalLine> JournalLines => Set<JournalLine>();
    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<SystemReport> SystemReports => Set<SystemReport>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.Entity<Product>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<Customer>().HasIndex(x => x.CustomerCode).IsUnique();
        modelBuilder.Entity<Supplier>().HasIndex(x => x.SupplierCode).IsUnique();
        modelBuilder.Entity<RoutePlan>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<Vehicle>().HasIndex(x => x.VehicleNumber).IsUnique();
        modelBuilder.Entity<SalesInvoice>().HasIndex(x => x.InvoiceNo).IsUnique();
        modelBuilder.Entity<PurchaseOrder>().HasIndex(x => x.OrderNo).IsUnique();
        modelBuilder.Entity<GoodsReceipt>().HasIndex(x => x.ReceiptNo).IsUnique();
        modelBuilder.Entity<SupplierInvoice>().HasIndex(x => x.InvoiceNo).IsUnique();
        modelBuilder.Entity<PurchaseReturn>().HasIndex(x => x.ReturnNo).IsUnique();
        modelBuilder.Entity<SupplierPayment>().HasIndex(x => x.PaymentNo).IsUnique();
        modelBuilder.Entity<Account>().HasIndex(x => x.AccountCode).IsUnique();
        modelBuilder.Entity<JournalEntry>().HasIndex(x => x.EntryNo).IsUnique();
        modelBuilder.Entity<SystemReport>().HasIndex(x => x.ReportCode).IsUnique();

        modelBuilder.Entity<SalesInvoiceItem>().Ignore(x => x.LineTotal);
        modelBuilder.Entity<PurchaseOrderItem>().Ignore(x => x.LineTotal);
        modelBuilder.Entity<GoodsReceiptItem>().Ignore(x => x.LineTotal);
        modelBuilder.Entity<PurchaseReturnItem>().Ignore(x => x.LineTotal);
    }
}
