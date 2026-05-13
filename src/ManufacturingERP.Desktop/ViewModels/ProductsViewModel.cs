using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ManufacturingERP.Desktop.Services;
using ManufacturingERP.Domain.Entities;
using ManufacturingERP.Application.Services;
using ManufacturingERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;

namespace ManufacturingERP.Desktop.ViewModels;

public partial class ProductsViewModel : ViewModelBase
{
    private readonly AuthorizationService _authorizationService;
    private readonly List<Product> _allProducts = [];
    public ObservableCollection<Product> Products { get; } = new();

    [ObservableProperty] private Product? _selectedProduct;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _searchText = string.Empty;
    [ObservableProperty] private string _activeTab = "Overview";

    public ProductsViewModel(AuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
        _ = LoadAsync();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var items = await db.Products.Include(x => x.ProductCategory).OrderBy(x => x.Name).ToListAsync();
        _allProducts.Clear();
        _allProducts.AddRange(items);
        ApplyFilter();
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        var auth = _authorizationService.EnsureSalesAccess();
        if (!auth.IsSuccess) { StatusMessage = auth.Message; return; }
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var categoryId = await db.ProductCategories.Select(x => x.Id).FirstAsync();
        var dialog = new Views.ProductDialogWindow(new Product
        {
            Code = $"NEW-{DateTime.Now:HHmmss}",
            Name = "New Product",
            ProductCategoryId = categoryId,
            Unit = "PCS",
            CostPrice = 0,
            SellingPrice = 0
        });

        if (dialog.ShowDialog() == true)
        {
            var validationMessage = ValidateProduct(dialog.Product, db);
            if (validationMessage is not null)
            {
                StatusMessage = validationMessage;
                return;
            }

            try
            {
                db.Products.Add(dialog.Product);
                await db.SaveChangesAsync();
                StatusMessage = "Product created.";
                await LoadAsync();
            }
            catch (DbUpdateException ex)
            {
                StatusMessage = MasterDataUiHelper.TryGetFriendlySaveError(ex, "Product", "code") ??
                    "Unable to save product.";
            }
        }
    }

    [RelayCommand]
    private async Task EditAsync(Product? product = null)
    {
        if (product is not null)
            SelectedProduct = product;
        var auth = _authorizationService.EnsureSalesAccess();
        if (!auth.IsSuccess) { StatusMessage = auth.Message; return; }
        if (SelectedProduct is null)
        {
            StatusMessage = "Select a product to edit.";
            return;
        }

        var clone = new Product
        {
            Id = SelectedProduct.Id,
            Code = SelectedProduct.Code,
            Name = SelectedProduct.Name,
            ProductCategoryId = SelectedProduct.ProductCategoryId,
            Unit = SelectedProduct.Unit,
            CostPrice = SelectedProduct.CostPrice,
            SellingPrice = SelectedProduct.SellingPrice,
            TrackBatch = SelectedProduct.TrackBatch,
            IsActive = SelectedProduct.IsActive
        };

        var dialog = new Views.ProductDialogWindow(clone);
        if (dialog.ShowDialog() == true)
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var validationMessage = ValidateProduct(dialog.Product, db, clone.Id);
            if (validationMessage is not null)
            {
                StatusMessage = validationMessage;
                return;
            }

            var entity = await db.Products.FirstAsync(x => x.Id == clone.Id);
            try
            {
                entity.Code = dialog.Product.Code.Trim();
                entity.Name = dialog.Product.Name.Trim();
                entity.Unit = dialog.Product.Unit.Trim();
                entity.CostPrice = dialog.Product.CostPrice;
                entity.SellingPrice = dialog.Product.SellingPrice;
                entity.TrackBatch = dialog.Product.TrackBatch;
                entity.IsActive = dialog.Product.IsActive;
                await db.SaveChangesAsync();
                StatusMessage = "Product updated.";
                await LoadAsync();
            }
            catch (DbUpdateException ex)
            {
                StatusMessage = MasterDataUiHelper.TryGetFriendlySaveError(ex, "Product", "code") ??
                    "Unable to update product.";
            }
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(Product? product = null)
    {
        if (product is not null)
            SelectedProduct = product;
        var auth = _authorizationService.EnsureSalesAccess();
        if (!auth.IsSuccess) { StatusMessage = auth.Message; return; }
        if (SelectedProduct is null)
        {
            StatusMessage = "Select a product to delete.";
            return;
        }

        if (!MasterDataUiHelper.ConfirmDelete("product", SelectedProduct.Name))
            return;

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        try
        {
            var entity = await db.Products.FirstAsync(x => x.Id == SelectedProduct.Id);
            db.Products.Remove(entity);
            await db.SaveChangesAsync();
            StatusMessage = "Product deleted.";
            await LoadAsync();
        }
        catch (DbUpdateException ex)
        {
            StatusMessage = MasterDataUiHelper.TryGetFriendlySaveError(ex, "Product", "code") ??
                "Unable to delete product.";
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    private void SelectProduct(Product? product)
    {
        if (product is not null)
            SelectedProduct = product;
    }

    [RelayCommand]
    private void SetActiveTab(string? tab)
    {
        if (!string.IsNullOrWhiteSpace(tab))
            ActiveTab = tab;
    }

    private void ApplyFilter()
    {
        var term = SearchText.Trim();
        var filtered = string.IsNullOrWhiteSpace(term)
            ? _allProducts
            : _allProducts.Where(x =>
                x.Code.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.Unit.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                (x.ProductCategory?.Name?.Contains(term, StringComparison.OrdinalIgnoreCase) ?? false))
            .ToList();

        Products.Clear();
        foreach (var item in filtered)
            Products.Add(item);

        OnPropertyChanged(nameof(ActiveProductsCount));
        OnPropertyChanged(nameof(InactiveProductsCount));
        OnPropertyChanged(nameof(CategoryCount));
        OnPropertyChanged(nameof(BatchTrackedCount));
        OnPropertyChanged(nameof(AverageSellingPrice));
    }

    public int ActiveProductsCount => Products.Count(x => x.IsActive);

    public int InactiveProductsCount => Products.Count(x => !x.IsActive);

    public int CategoryCount => Products
        .Where(x => x.ProductCategory is not null)
        .Select(x => x.ProductCategoryId)
        .Distinct()
        .Count();

    public int BatchTrackedCount => Products.Count(x => x.TrackBatch);

    public decimal AverageSellingPrice => Products.Count == 0
        ? 0
        : Products.Average(x => x.SellingPrice);

    private static string? ValidateProduct(Product product, AppDbContext db, int? currentId = null)
    {
        if (string.IsNullOrWhiteSpace(product.Code))
            return "Product code is required.";
        if (string.IsNullOrWhiteSpace(product.Name))
            return "Product name is required.";
        if (string.IsNullOrWhiteSpace(product.Unit))
            return "Unit is required.";
        if (product.CostPrice < 0)
            return "Cost price cannot be negative.";
        if (product.SellingPrice < 0)
            return "Selling price cannot be negative.";

        var code = product.Code.Trim();
        var exists = db.Products.Any(x => x.Code == code && x.Id != currentId);
        if (exists)
            return MasterDataUiHelper.GetDuplicateMessage("Product", "code");

        product.Code = code;
        product.Name = product.Name.Trim();
        product.Unit = product.Unit.Trim();
        return null;
    }
}
