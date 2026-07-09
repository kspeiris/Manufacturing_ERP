using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ManufacturingERP.Application.DTOs;
using ManufacturingERP.Application.Services;
using ManufacturingERP.Domain.Entities;
using ManufacturingERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace ManufacturingERP.Desktop.ViewModels;

public partial class InventoryAccuracyViewModel : ViewModelBase
{
    private readonly InventoryService _inventoryService;
    private readonly CurrentUserService _currentUserService;

    public ObservableCollection<Product> Products { get; } = new();
    public ObservableCollection<Warehouse> Warehouses { get; } = new();
    public ObservableCollection<WarehouseBin> WarehouseBins { get; } = new();
    public ObservableCollection<BatchLot> BatchLots { get; } = new();
    public ObservableCollection<StockTransfer> StockTransfers { get; } = new();
    public ObservableCollection<StockCount> StockCounts { get; } = new();
    public ObservableCollection<ReorderAlertDto> ReorderAlerts { get; } = new();

    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private Product? _selectedProduct;
    [ObservableProperty] private Warehouse? _selectedWarehouse;
    [ObservableProperty] private WarehouseBin? _selectedWarehouseBin;
    [ObservableProperty] private BatchLot? _selectedBatchLot;
    [ObservableProperty] private StockTransfer? _selectedStockTransfer;
    [ObservableProperty] private StockCount? _selectedStockCount;

    [ObservableProperty] private string _lotNumber = string.Empty;
    [ObservableProperty] private DateTime _manufacturingDate = DateTime.Today;
    [ObservableProperty] private DateTime? _expiryDate = DateTime.Today.AddMonths(6);
    [ObservableProperty] private decimal _lotQuantity;

    [ObservableProperty] private decimal _selectedProductReorderLevel;
    [ObservableProperty] private bool _selectedProductTrackBatch;

    [ObservableProperty] private Warehouse? _transferFromWarehouse;
    [ObservableProperty] private Warehouse? _transferToWarehouse;
    [ObservableProperty] private Product? _transferProduct;
    [ObservableProperty] private decimal _transferQuantity;
    [ObservableProperty] private string _transferNotes = string.Empty;

    [ObservableProperty] private Product? _countProduct;
    [ObservableProperty] private Warehouse? _countWarehouse;
    [ObservableProperty] private BatchLot? _countBatchLot;
    [ObservableProperty] private decimal _bookQuantity;
    [ObservableProperty] private decimal _countedQuantity;
    [ObservableProperty] private string _countNotes = string.Empty;

    public InventoryAccuracyViewModel(InventoryService inventoryService, CurrentUserService currentUserService)
    {
        _inventoryService = inventoryService;
        _currentUserService = currentUserService;
        _ = LoadAsync();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            Products.Clear();
            foreach (var item in await db.Products.Where(x => x.IsActive).OrderBy(x => x.Code).ToListAsync())
                Products.Add(item);

            Warehouses.Clear();
            foreach (var item in await db.Warehouses.Where(x => x.IsActive).OrderBy(x => x.Name).ToListAsync())
                Warehouses.Add(item);

            WarehouseBins.Clear();
            foreach (var item in await db.WarehouseBins.Include(x => x.Warehouse).OrderBy(x => x.Warehouse!.Name).ThenBy(x => x.BinCode).ToListAsync())
                WarehouseBins.Add(item);

            BatchLots.Clear();
            foreach (var item in await db.BatchLots.Include(x => x.Product).Include(x => x.Warehouse).Include(x => x.WarehouseBin).OrderBy(x => x.Product!.Code).ThenBy(x => x.ExpiryDate).ToListAsync())
                BatchLots.Add(item);

            StockTransfers.Clear();
            foreach (var item in await db.StockTransfers.Include(x => x.FromWarehouse).Include(x => x.ToWarehouse).OrderByDescending(x => x.TransferDate).Take(100).ToListAsync())
                StockTransfers.Add(item);

            StockCounts.Clear();
            foreach (var item in await db.StockCounts.Include(x => x.Warehouse).OrderByDescending(x => x.CountDate).Take(100).ToListAsync())
                StockCounts.Add(item);

            ReorderAlerts.Clear();
            foreach (var item in await _inventoryService.GetReorderAlertsAsync())
                ReorderAlerts.Add(item);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load inventory data: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task SaveBatchLotAsync()
    {
        if (SelectedProduct is null || SelectedWarehouse is null || LotQuantity <= 0 || string.IsNullOrWhiteSpace(LotNumber))
        {
            StatusMessage = "Select product and warehouse, then enter lot number and quantity.";
            return;
        }

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var lot = SelectedBatchLot is null ? new BatchLot() : await db.BatchLots.FirstAsync(x => x.Id == SelectedBatchLot.Id);
        var previousWarehouseId = lot.WarehouseId;
        var previousQuantity = SelectedBatchLot is null ? 0 : lot.QuantityOnHand;
        lot.LotNumber = LotNumber.Trim();
        lot.ProductId = SelectedProduct.Id;
        lot.WarehouseId = SelectedWarehouse.Id;
        lot.WarehouseBinId = SelectedWarehouseBin?.Id;
        lot.ManufacturingDate = ManufacturingDate.Date;
        lot.ExpiryDate = ExpiryDate?.Date;
        lot.QuantityReceived = SelectedBatchLot is null ? LotQuantity : lot.QuantityReceived;
        lot.QuantityOnHand = LotQuantity;
        lot.IsActive = true;
        if (SelectedBatchLot is null) db.BatchLots.Add(lot);
        try
        {
            if (SelectedBatchLot is not null && previousWarehouseId != SelectedWarehouse.Id)
                await AdjustStockBalanceAsync(db, SelectedProduct.Id, previousWarehouseId, -previousQuantity);

            var stockDelta = SelectedBatchLot is null || previousWarehouseId != SelectedWarehouse.Id
                ? LotQuantity
                : LotQuantity - previousQuantity;
            await AdjustStockBalanceAsync(db, SelectedProduct.Id, SelectedWarehouse.Id, stockDelta);

            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            StatusMessage = "Batch lot saved.";
            await LoadAsync();
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = ex.Message;
        }
    }

    [RelayCommand]
    private void NewBatchLot()
    {
        SelectedBatchLot = null;
        LotNumber = string.Empty;
        LotQuantity = 0;
        ManufacturingDate = DateTime.Today;
        ExpiryDate = DateTime.Today.AddMonths(6);
    }

    [RelayCommand]
    private async Task SaveReorderLevelAsync()
    {
        if (SelectedProduct is null)
        {
            StatusMessage = "Select a product.";
            return;
        }

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var product = await db.Products.FirstAsync(x => x.Id == SelectedProduct.Id);
        product.ReorderLevel = SelectedProductReorderLevel;
        product.TrackBatch = SelectedProductTrackBatch;
        await db.SaveChangesAsync();
        StatusMessage = "Product inventory controls saved.";
        await LoadAsync();
    }

    [RelayCommand]
    private async Task CreateStockTransferAsync()
    {
        if (TransferFromWarehouse is null || TransferToWarehouse is null || TransferProduct is null || TransferQuantity <= 0)
        {
            StatusMessage = "Select transfer warehouses, product, and quantity.";
            return;
        }
        if (TransferFromWarehouse.Id == TransferToWarehouse.Id)
        {
            StatusMessage = "Transfer source and destination must be different.";
            return;
        }
        if (_currentUserService.CurrentUserId is null)
        {
            StatusMessage = "A signed-in user is required for stock transfers.";
            return;
        }

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var fromStock = await db.StockBalances.FirstOrDefaultAsync(x => x.ProductId == TransferProduct.Id && x.WarehouseId == TransferFromWarehouse.Id);
        if (fromStock is null || fromStock.QuantityAvailable < TransferQuantity)
        {
            StatusMessage = $"Insufficient available stock. Available: {fromStock?.QuantityAvailable ?? 0:N2}.";
            return;
        }

        var transfer = new StockTransfer
        {
            TransferNo = $"ST-{DateTime.Now:yyyyMMdd-HHmmssfff}",
            TransferDate = DateTime.Today,
            FromWarehouseId = TransferFromWarehouse.Id,
            ToWarehouseId = TransferToWarehouse.Id,
            RequestedByUserId = _currentUserService.CurrentUserId.Value,
            Notes = TransferNotes,
            Lines =
            [
                new StockTransferLine
                {
                    ProductId = TransferProduct.Id,
                    QuantityRequested = TransferQuantity,
                    QuantityDispatched = TransferQuantity
                }
            ]
        };

        db.StockTransfers.Add(transfer);
        await db.SaveChangesAsync();
        StatusMessage = "Stock transfer created.";
        TransferQuantity = 0;
        TransferNotes = string.Empty;
        await LoadAsync();
    }

    [RelayCommand] private async Task SubmitTransferAsync() => await RunTransferActionAsync(x => x.Submit(), "Transfer submitted.");
    [RelayCommand]
    private async Task ApproveTransferAsync()
    {
        if (_currentUserService.CurrentUserId is null)
        {
            StatusMessage = "A signed-in user is required to approve transfers.";
            return;
        }

        await RunTransferActionAsync(x => x.Approve(_currentUserService.CurrentUserId.Value), "Transfer approved.");
    }
    [RelayCommand] private async Task DispatchTransferAsync() => await RunTransferActionAsync(x => x.Dispatch(), "Transfer dispatched.");
    [RelayCommand] private async Task ReceiveTransferAsync() => await ReceiveTransferInternalAsync();
    [RelayCommand] private async Task CancelTransferAsync() => await RunTransferActionAsync(x => x.Cancel(), "Transfer cancelled.");

    [RelayCommand]
    private async Task CreateStockCountAsync()
    {
        if (CountProduct is null || CountWarehouse is null)
        {
            StatusMessage = "Select count product and warehouse.";
            return;
        }
        if (_currentUserService.CurrentUserId is null)
        {
            StatusMessage = "A signed-in user is required for stock counts.";
            return;
        }

        var result = await _inventoryService.CreateStockCountAsync(new CreateStockCountRequest
        {
            WarehouseId = CountWarehouse.Id,
            InitiatedByUserId = _currentUserService.CurrentUserId.Value,
            Notes = CountNotes,
            Lines =
            [
                new()
                {
                    ProductId = CountProduct.Id,
                    BatchLotId = CountBatchLot?.Id,
                    BookQuantity = BookQuantity,
                    CountedQuantity = CountedQuantity,
                    UnitCost = CountProduct.CostPrice,
                    Notes = CountNotes
                }
            ]
        });

        StatusMessage = result.IsSuccess ? $"Stock count created: {result.Message}" : result.Message;
        if (result.IsSuccess)
        {
            BookQuantity = 0;
            CountedQuantity = 0;
            CountNotes = string.Empty;
            await LoadAsync();
        }
    }

    [RelayCommand] private async Task StartCountAsync() => await RunCountActionAsync(x => _inventoryService.StartStockCountAsync(x.Id));
    [RelayCommand] private async Task SubmitCountAsync() => await RunCountActionAsync(x => _inventoryService.SubmitStockCountAsync(x.Id));
    [RelayCommand]
    private async Task ApproveCountAsync()
    {
        if (_currentUserService.CurrentUserId is null)
        {
            StatusMessage = "A signed-in user is required to approve stock counts.";
            return;
        }

        await RunCountActionAsync(x => _inventoryService.ApproveStockCountAsync(x.Id, _currentUserService.CurrentUserId.Value));
    }

    partial void OnSelectedBatchLotChanged(BatchLot? value)
    {
        if (value is null) return;
        SelectedProduct = Products.FirstOrDefault(x => x.Id == value.ProductId);
        SelectedWarehouse = Warehouses.FirstOrDefault(x => x.Id == value.WarehouseId);
        SelectedWarehouseBin = WarehouseBins.FirstOrDefault(x => x.Id == value.WarehouseBinId);
        LotNumber = value.LotNumber;
        ManufacturingDate = value.ManufacturingDate;
        ExpiryDate = value.ExpiryDate;
        LotQuantity = value.QuantityOnHand;
    }

    partial void OnSelectedProductChanged(Product? value)
    {
        if (value is null) return;
        SelectedProductReorderLevel = value.ReorderLevel;
        SelectedProductTrackBatch = value.TrackBatch;
    }

    private async Task RunTransferActionAsync(Action<StockTransfer> action, string successMessage)
    {
        if (SelectedStockTransfer is null)
        {
            StatusMessage = "Select a stock transfer.";
            return;
        }

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var transfer = await db.StockTransfers.Include(x => x.Lines).FirstAsync(x => x.Id == SelectedStockTransfer.Id);
        try
        {
            action(transfer);
            await db.SaveChangesAsync();
            StatusMessage = successMessage;
            await LoadAsync();
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task ReceiveTransferInternalAsync()
    {
        if (SelectedStockTransfer is null)
        {
            StatusMessage = "Select a stock transfer.";
            return;
        }

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await using var transaction = await db.Database.BeginTransactionAsync();
        var transfer = await db.StockTransfers.Include(x => x.Lines).FirstAsync(x => x.Id == SelectedStockTransfer.Id);
        try
        {
            if (_currentUserService.CurrentUserId is null)
            {
                StatusMessage = "A signed-in user is required to receive transfers.";
                return;
            }

            transfer.Receive(_currentUserService.CurrentUserId.Value);
            foreach (var line in transfer.Lines)
            {
                line.QuantityReceived = line.QuantityDispatched;
                var from = await db.StockBalances.FirstOrDefaultAsync(x => x.ProductId == line.ProductId && x.WarehouseId == transfer.FromWarehouseId);
                var to = await db.StockBalances.FirstOrDefaultAsync(x => x.ProductId == line.ProductId && x.WarehouseId == transfer.ToWarehouseId);
                if (from is null || from.QuantityAvailable < line.QuantityDispatched)
                {
                    StatusMessage = $"Insufficient source stock for product ID {line.ProductId}.";
                    return;
                }

                from.QuantityOnHand -= line.QuantityDispatched;
                if (to is null)
                {
                    to = new StockBalance { ProductId = line.ProductId, WarehouseId = transfer.ToWarehouseId };
                    db.StockBalances.Add(to);
                }
                to.QuantityOnHand += line.QuantityReceived;

                db.WarehouseTransactions.Add(new WarehouseTransaction
                {
                    ProductId = line.ProductId,
                    WarehouseId = transfer.FromWarehouseId,
                    TransactionDate = DateTime.Now,
                    TransactionType = "TRANSFER-OUT",
                    QuantityOut = line.QuantityDispatched,
                    ReferenceNo = transfer.TransferNo,
                    Remarks = $"Transfer to warehouse {transfer.ToWarehouseId}"
                });

                db.WarehouseTransactions.Add(new WarehouseTransaction
                {
                    ProductId = line.ProductId,
                    WarehouseId = transfer.ToWarehouseId,
                    TransactionDate = DateTime.Now,
                    TransactionType = "TRANSFER-IN",
                    QuantityIn = line.QuantityReceived,
                    ReferenceNo = transfer.TransferNo,
                    Remarks = $"Transfer from warehouse {transfer.FromWarehouseId}"
                });
            }

            await db.SaveChangesAsync();
            await transaction.CommitAsync();
            StatusMessage = "Transfer received and stock moved.";
            await LoadAsync();
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task RunCountActionAsync(Func<StockCount, Task<ManufacturingERP.Shared.Results.Result>> action)
    {
        if (SelectedStockCount is null)
        {
            StatusMessage = "Select a stock count.";
            return;
        }

        var result = await action(SelectedStockCount);
        StatusMessage = result.Message;
        await LoadAsync();
    }

    private static async Task AdjustStockBalanceAsync(AppDbContext db, int productId, int warehouseId, decimal delta)
    {
        if (delta == 0) return;

        var stock = await db.StockBalances.FirstOrDefaultAsync(x => x.ProductId == productId && x.WarehouseId == warehouseId);
        if (stock is null)
        {
            stock = new StockBalance { ProductId = productId, WarehouseId = warehouseId };
            db.StockBalances.Add(stock);
        }

        if (stock.QuantityOnHand + delta < 0)
            throw new InvalidOperationException("Batch quantity change would make warehouse stock negative.");

        stock.QuantityOnHand += delta;
    }
}
