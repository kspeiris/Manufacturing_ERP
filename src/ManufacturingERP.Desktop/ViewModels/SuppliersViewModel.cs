using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ManufacturingERP.Application.Services;
using ManufacturingERP.Desktop.Services;
using ManufacturingERP.Domain.Entities;
using ManufacturingERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;

namespace ManufacturingERP.Desktop.ViewModels;

public partial class SuppliersViewModel : ViewModelBase
{
    private readonly AuthorizationService _authorizationService;
    private readonly MasterDataValidationService _validationService;
    private readonly List<Supplier> _allSuppliers = [];
    public ObservableCollection<Supplier> Suppliers { get; } = new();

    [ObservableProperty] private Supplier? _selectedSupplier;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _searchText = string.Empty;

    public SuppliersViewModel(MasterDataValidationService validationService, AuthorizationService authorizationService)
    {
        _validationService = validationService;
        _authorizationService = authorizationService;
        _ = LoadAsync();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        _allSuppliers.Clear();
        _allSuppliers.AddRange(await db.Suppliers.OrderBy(x => x.Name).ToListAsync());
        ApplyFilter();
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        var auth = _authorizationService.EnsureAdminAccess();
        if (!auth.IsSuccess) { StatusMessage = auth.Message; return; }
        var dialog = new Views.SupplierDialogWindow(new Supplier { SupplierCode = $"S{DateTime.Now:HHmmss}", Name = "New Supplier" });
        if (dialog.ShowDialog() == true)
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var validationMessage = ValidateSupplier(dialog.Supplier, db);
            if (validationMessage is not null) { StatusMessage = validationMessage; return; }

            try
            {
                db.Suppliers.Add(dialog.Supplier);
                await db.SaveChangesAsync();
                StatusMessage = "Supplier created.";
                await LoadAsync();
            }
            catch (DbUpdateException ex)
            {
                StatusMessage = MasterDataUiHelper.TryGetFriendlySaveError(ex, "Supplier", "code") ??
                    "Unable to save supplier.";
            }
        }
    }

    [RelayCommand]
    private async Task EditAsync(Supplier? supplier = null)
    {
        if (supplier is not null)
            SelectedSupplier = supplier;
        var auth = _authorizationService.EnsureAdminAccess();
        if (!auth.IsSuccess) { StatusMessage = auth.Message; return; }
        if (SelectedSupplier is null) { StatusMessage = "Select a supplier."; return; }
        var clone = new Supplier
        {
            Id = SelectedSupplier.Id,
            SupplierCode = SelectedSupplier.SupplierCode,
            Name = SelectedSupplier.Name,
            ContactNumber = SelectedSupplier.ContactNumber,
            Address = SelectedSupplier.Address
        };
        var dialog = new Views.SupplierDialogWindow(clone);
        if (dialog.ShowDialog() == true)
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var validationMessage = ValidateSupplier(dialog.Supplier, db, clone.Id);
            if (validationMessage is not null) { StatusMessage = validationMessage; return; }

            try
            {
                var entity = await db.Suppliers.FirstAsync(x => x.Id == clone.Id);
                entity.SupplierCode = dialog.Supplier.SupplierCode.Trim();
                entity.Name = dialog.Supplier.Name.Trim();
                entity.ContactNumber = dialog.Supplier.ContactNumber.Trim();
                entity.Address = dialog.Supplier.Address.Trim();
                await db.SaveChangesAsync();
                StatusMessage = "Supplier updated.";
                await LoadAsync();
            }
            catch (DbUpdateException ex)
            {
                StatusMessage = MasterDataUiHelper.TryGetFriendlySaveError(ex, "Supplier", "code") ??
                    "Unable to update supplier.";
            }
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(Supplier? supplier = null)
    {
        if (supplier is not null)
            SelectedSupplier = supplier;
        var auth = _authorizationService.EnsureAdminAccess();
        if (!auth.IsSuccess) { StatusMessage = auth.Message; return; }
        if (SelectedSupplier is null) { StatusMessage = "Select a supplier."; return; }
        if (!MasterDataUiHelper.ConfirmDelete("supplier", SelectedSupplier.Name))
            return;

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        try
        {
            var entity = await db.Suppliers.FirstAsync(x => x.Id == SelectedSupplier.Id);
            db.Suppliers.Remove(entity);
            await db.SaveChangesAsync();
            StatusMessage = "Supplier deleted.";
            await LoadAsync();
        }
        catch (DbUpdateException ex)
        {
            StatusMessage = MasterDataUiHelper.TryGetFriendlySaveError(ex, "Supplier", "code") ??
                "Unable to delete supplier.";
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    private void SelectSupplier(Supplier? supplier)
    {
        if (supplier is not null)
            SelectedSupplier = supplier;
    }

    private void ApplyFilter()
    {
        var term = SearchText.Trim();
        var filtered = string.IsNullOrWhiteSpace(term)
            ? _allSuppliers
            : _allSuppliers.Where(x =>
                x.SupplierCode.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.ContactNumber.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.Address.Contains(term, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Suppliers.Clear();
        foreach (var item in filtered)
            Suppliers.Add(item);
    }

    private string? ValidateSupplier(Supplier supplier, AppDbContext db, int? currentId = null)
    {
        var codeCheck = _validationService.ValidateRequired("Supplier code", supplier.SupplierCode);
        if (!codeCheck.ok) return codeCheck.message;

        var nameCheck = _validationService.ValidateRequired("Supplier name", supplier.Name);
        if (!nameCheck.ok) return nameCheck.message;

        var code = supplier.SupplierCode.Trim();
        var exists = db.Suppliers.Any(x => x.SupplierCode == code && x.Id != currentId);
        if (exists)
            return MasterDataUiHelper.GetDuplicateMessage("Supplier", "code");

        supplier.SupplierCode = code;
        supplier.Name = supplier.Name.Trim();
        supplier.ContactNumber = supplier.ContactNumber.Trim();
        supplier.Address = supplier.Address.Trim();
        return null;
    }
}
