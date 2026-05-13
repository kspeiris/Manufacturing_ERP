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
    public DbSet<WarehouseBin> WarehouseBins => Set<WarehouseBin>();
    public DbSet<StockBalance> StockBalances => Set<StockBalance>();
    public DbSet<WarehouseTransaction> WarehouseTransactions => Set<WarehouseTransaction>();
    public DbSet<BatchLot> BatchLots => Set<BatchLot>();
    public DbSet<StockTransfer> StockTransfers => Set<StockTransfer>();
    public DbSet<StockTransferLine> StockTransferLines => Set<StockTransferLine>();
    public DbSet<StockCount> StockCounts => Set<StockCount>();
    public DbSet<StockCountLine> StockCountLines => Set<StockCountLine>();
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
    public DbSet<FiscalPeriod> FiscalPeriods => Set<FiscalPeriod>();
    public DbSet<Tax> Taxes => Set<Tax>();
    public DbSet<Voucher> Vouchers => Set<Voucher>();
    public DbSet<VoucherLine> VoucherLines => Set<VoucherLine>();
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
        modelBuilder.Entity<StockBalance>().HasIndex(x => new { x.ProductId, x.WarehouseId }).IsUnique();
        modelBuilder.Entity<Customer>().HasIndex(x => x.CustomerCode).IsUnique();
        modelBuilder.Entity<Supplier>().HasIndex(x => x.SupplierCode).IsUnique();
        modelBuilder.Entity<RoutePlan>().HasIndex(x => x.Code).IsUnique();
        modelBuilder.Entity<Vehicle>().HasIndex(x => x.VehicleNumber).IsUnique();
        modelBuilder.Entity<WarehouseBin>().HasIndex(x => new { x.WarehouseId, x.BinCode }).IsUnique();
        modelBuilder.Entity<BatchLot>().HasIndex(x => new { x.ProductId, x.WarehouseId, x.LotNumber }).IsUnique();
        modelBuilder.Entity<StockTransfer>().HasIndex(x => x.TransferNo).IsUnique();
        modelBuilder.Entity<StockCount>().HasIndex(x => x.CountNo).IsUnique();
        modelBuilder.Entity<SalesInvoice>().HasIndex(x => x.InvoiceNo).IsUnique();
        modelBuilder.Entity<PurchaseOrder>().HasIndex(x => x.OrderNo).IsUnique();
        modelBuilder.Entity<GoodsReceipt>().HasIndex(x => x.ReceiptNo).IsUnique();
        modelBuilder.Entity<SupplierInvoice>().HasIndex(x => x.InvoiceNo).IsUnique();
        modelBuilder.Entity<PurchaseReturn>().HasIndex(x => x.ReturnNo).IsUnique();
        modelBuilder.Entity<SupplierPayment>().HasIndex(x => x.PaymentNo).IsUnique();
        modelBuilder.Entity<Account>().HasIndex(x => x.AccountCode).IsUnique();
        modelBuilder.Entity<FiscalPeriod>().HasIndex(x => new { x.FiscalYear, x.PeriodNumber }).IsUnique();
        modelBuilder.Entity<Tax>().HasIndex(x => x.TaxCode).IsUnique();
        modelBuilder.Entity<Voucher>().HasIndex(x => x.VoucherNo).IsUnique();
        modelBuilder.Entity<JournalEntry>().HasIndex(x => x.EntryNo).IsUnique();
        modelBuilder.Entity<SystemReport>().HasIndex(x => x.ReportCode).IsUnique();

        modelBuilder.Entity<StockBalance>().Ignore(x => x.QuantityAvailable);

        modelBuilder.Entity<FiscalPeriod>()
            .HasOne(x => x.ClosedByUser)
            .WithMany()
            .HasForeignKey(x => x.ClosedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Tax>()
            .HasOne(x => x.InputTaxAccount)
            .WithMany()
            .HasForeignKey(x => x.InputTaxAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Tax>()
            .HasOne(x => x.OutputTaxAccount)
            .WithMany()
            .HasForeignKey(x => x.OutputTaxAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Voucher>()
            .HasOne(x => x.CreatedByUser)
            .WithMany()
            .HasForeignKey(x => x.CreatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Voucher>()
            .HasOne(x => x.ApprovedByUser)
            .WithMany()
            .HasForeignKey(x => x.ApprovedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<Voucher>()
            .HasOne(x => x.ReversalOfVoucher)
            .WithMany()
            .HasForeignKey(x => x.ReversalOfVoucherId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StockTransfer>()
            .HasOne(x => x.FromWarehouse)
            .WithMany()
            .HasForeignKey(x => x.FromWarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StockTransfer>()
            .HasOne(x => x.ToWarehouse)
            .WithMany()
            .HasForeignKey(x => x.ToWarehouseId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StockTransfer>()
            .HasOne(x => x.FromBin)
            .WithMany()
            .HasForeignKey(x => x.FromBinId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StockTransfer>()
            .HasOne(x => x.ToBin)
            .WithMany()
            .HasForeignKey(x => x.ToBinId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StockTransfer>()
            .HasOne(x => x.RequestedByUser)
            .WithMany()
            .HasForeignKey(x => x.RequestedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StockTransfer>()
            .HasOne(x => x.ApprovedByUser)
            .WithMany()
            .HasForeignKey(x => x.ApprovedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StockTransfer>()
            .HasOne(x => x.ReceivedByUser)
            .WithMany()
            .HasForeignKey(x => x.ReceivedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StockCount>()
            .HasOne(x => x.InitiatedByUser)
            .WithMany()
            .HasForeignKey(x => x.InitiatedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StockCount>()
            .HasOne(x => x.ApprovedByUser)
            .WithMany()
            .HasForeignKey(x => x.ApprovedByUserId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StockCount>()
            .HasOne(x => x.VarianceVoucher)
            .WithMany()
            .HasForeignKey(x => x.VarianceVoucherId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<StockBalance>()
            .HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<StockBalance>()
            .HasOne(x => x.Warehouse)
            .WithMany()
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BatchLot>()
            .HasOne(x => x.Product)
            .WithMany()
            .HasForeignKey(x => x.ProductId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BatchLot>()
            .HasOne(x => x.Warehouse)
            .WithMany()
            .HasForeignKey(x => x.WarehouseId)
            .OnDelete(DeleteBehavior.Cascade);

        modelBuilder.Entity<BatchLot>()
            .HasOne(x => x.WarehouseBin)
            .WithMany()
            .HasForeignKey(x => x.WarehouseBinId)
            .OnDelete(DeleteBehavior.Restrict);

        modelBuilder.Entity<SalesInvoiceItem>().Ignore(x => x.LineTotal);
        modelBuilder.Entity<PurchaseOrderItem>().Ignore(x => x.LineTotal);
        modelBuilder.Entity<GoodsReceiptItem>().Ignore(x => x.LineTotal);
        modelBuilder.Entity<PurchaseReturnItem>().Ignore(x => x.LineTotal);
    }
}
