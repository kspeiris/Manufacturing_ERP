using ManufacturingERP.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ManufacturingERP.Application.Abstractions;

public interface IAppDbContext
{
    DbSet<User> Users { get; }
    DbSet<ProductCategory> ProductCategories { get; }
    DbSet<Product> Products { get; }
    DbSet<Customer> Customers { get; }
    DbSet<Supplier> Suppliers { get; }
    DbSet<RoutePlan> RoutePlans { get; }
    DbSet<Vehicle> Vehicles { get; }
    DbSet<Warehouse> Warehouses { get; }
    DbSet<StockBalance> StockBalances { get; }
    DbSet<WarehouseTransaction> WarehouseTransactions { get; }
    DbSet<VehicleLoad> VehicleLoads { get; }
    DbSet<VehicleLoadItem> VehicleLoadItems { get; }
    DbSet<SalesInvoice> SalesInvoices { get; }
    DbSet<SalesInvoiceItem> SalesInvoiceItems { get; }
    DbSet<CollectionEntry> CollectionEntries { get; }
    DbSet<PurchaseOrder> PurchaseOrders { get; }
    DbSet<PurchaseOrderItem> PurchaseOrderItems { get; }
    DbSet<GoodsReceipt> GoodsReceipts { get; }
    DbSet<GoodsReceiptItem> GoodsReceiptItems { get; }
    DbSet<SupplierInvoice> SupplierInvoices { get; }
    DbSet<PurchaseReturn> PurchaseReturns { get; }
    DbSet<PurchaseReturnItem> PurchaseReturnItems { get; }
    DbSet<SupplierPayment> SupplierPayments { get; }
    DbSet<Account> Accounts { get; }
    DbSet<SyncLog> SyncLogs { get; }
    DbSet<BomHeader> BomHeaders { get; }
    DbSet<BomLine> BomLines { get; }
    DbSet<ProductionOrder> ProductionOrders { get; }
    DbSet<ProductionMaterialIssue> ProductionMaterialIssues { get; }
    DbSet<ExpenseEntry> ExpenseEntries { get; }
    DbSet<JournalEntry> JournalEntries { get; }
    DbSet<JournalLine> JournalLines { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<SystemReport> SystemReports { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
