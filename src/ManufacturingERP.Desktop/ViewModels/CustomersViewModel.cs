using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ManufacturingERP.Application.Services;
using ManufacturingERP.Desktop.Services;
using ManufacturingERP.Domain.Entities;
using ManufacturingERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;

namespace ManufacturingERP.Desktop.ViewModels;

public partial class CustomersViewModel : ViewModelBase
{
    private readonly AuthorizationService _authorizationService;
    private readonly List<Customer> _allCustomers = [];
    public ObservableCollection<Customer> Customers { get; } = new();

    [ObservableProperty] private Customer? _selectedCustomer;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _searchText = string.Empty;

    public CustomersViewModel(AuthorizationService authorizationService)
    {
        _authorizationService = authorizationService;
        _ = LoadAsync();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var items = await db.Customers.OrderBy(x => x.ShopName).ToListAsync();
        _allCustomers.Clear();
        _allCustomers.AddRange(items);
        ApplyFilter();
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        var auth = _authorizationService.EnsureSalesAccess();
        if (!auth.IsSuccess) { StatusMessage = auth.Message; return; }
        var dialog = new Views.CustomerDialogWindow(new Customer
        {
            CustomerCode = $"C{DateTime.Now:HHmmss}",
            ShopName = "New Shop",
            OwnerName = "Owner",
            Route = "Route",
            Address = "",
            ContactNumber = "",
            CreditLimit = 0,
            IsActive = true
        });

        if (dialog.ShowDialog() == true)
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var validationMessage = ValidateCustomer(dialog.Customer, db);
            if (validationMessage is not null)
            {
                StatusMessage = validationMessage;
                return;
            }

            try
            {
                db.Customers.Add(dialog.Customer);
                await db.SaveChangesAsync();
                StatusMessage = "Customer created.";
                await LoadAsync();
            }
            catch (DbUpdateException ex)
            {
                StatusMessage = MasterDataUiHelper.TryGetFriendlySaveError(ex, "Customer", "code") ??
                    "Unable to save customer.";
            }
        }
    }

    [RelayCommand]
    private async Task EditAsync(Customer? customer = null)
    {
        if (customer is not null)
            SelectedCustomer = customer;
        var auth = _authorizationService.EnsureSalesAccess();
        if (!auth.IsSuccess) { StatusMessage = auth.Message; return; }
        if (SelectedCustomer is null)
        {
            StatusMessage = "Select a customer to edit.";
            return;
        }

        var clone = new Customer
        {
            Id = SelectedCustomer.Id,
            CustomerCode = SelectedCustomer.CustomerCode,
            ShopName = SelectedCustomer.ShopName,
            OwnerName = SelectedCustomer.OwnerName,
            Route = SelectedCustomer.Route,
            Address = SelectedCustomer.Address,
            ContactNumber = SelectedCustomer.ContactNumber,
            CreditLimit = SelectedCustomer.CreditLimit,
            OutstandingBalance = SelectedCustomer.OutstandingBalance,
            IsActive = SelectedCustomer.IsActive
        };

        var dialog = new Views.CustomerDialogWindow(clone);
        if (dialog.ShowDialog() == true)
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var validationMessage = ValidateCustomer(dialog.Customer, db, clone.Id);
            if (validationMessage is not null)
            {
                StatusMessage = validationMessage;
                return;
            }

            try
            {
                var entity = await db.Customers.FirstAsync(x => x.Id == clone.Id);
                entity.CustomerCode = dialog.Customer.CustomerCode.Trim();
                entity.ShopName = dialog.Customer.ShopName.Trim();
                entity.OwnerName = dialog.Customer.OwnerName.Trim();
                entity.Route = dialog.Customer.Route.Trim();
                entity.Address = dialog.Customer.Address.Trim();
                entity.ContactNumber = dialog.Customer.ContactNumber.Trim();
                entity.CreditLimit = dialog.Customer.CreditLimit;
                entity.IsActive = dialog.Customer.IsActive;
                await db.SaveChangesAsync();
                StatusMessage = "Customer updated.";
                await LoadAsync();
            }
            catch (DbUpdateException ex)
            {
                StatusMessage = MasterDataUiHelper.TryGetFriendlySaveError(ex, "Customer", "code") ??
                    "Unable to update customer.";
            }
        }
    }

    [RelayCommand]
    private async Task DeleteAsync(Customer? customer = null)
    {
        if (customer is not null)
            SelectedCustomer = customer;
        var auth = _authorizationService.EnsureSalesAccess();
        if (!auth.IsSuccess) { StatusMessage = auth.Message; return; }
        if (SelectedCustomer is null)
        {
            StatusMessage = "Select a customer to delete.";
            return;
        }

        if (!MasterDataUiHelper.ConfirmDelete("customer", SelectedCustomer.ShopName))
            return;

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        try
        {
            var entity = await db.Customers.FirstAsync(x => x.Id == SelectedCustomer.Id);
            db.Customers.Remove(entity);
            await db.SaveChangesAsync();
            StatusMessage = "Customer deleted.";
            await LoadAsync();
        }
        catch (DbUpdateException ex)
        {
            StatusMessage = MasterDataUiHelper.TryGetFriendlySaveError(ex, "Customer", "code") ??
                "Unable to delete customer.";
        }
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    [RelayCommand]
    private void SelectCustomer(Customer? customer)
    {
        if (customer is not null)
            SelectedCustomer = customer;
    }

    private void ApplyFilter()
    {
        var term = SearchText.Trim();
        var filtered = string.IsNullOrWhiteSpace(term)
            ? _allCustomers
            : _allCustomers.Where(x =>
                x.CustomerCode.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.ShopName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.OwnerName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.Route.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.ContactNumber.Contains(term, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Customers.Clear();
        foreach (var item in filtered)
            Customers.Add(item);
    }

    private static string? ValidateCustomer(Customer customer, AppDbContext db, int? currentId = null)
    {
        if (string.IsNullOrWhiteSpace(customer.CustomerCode))
            return "Customer code is required.";
        if (string.IsNullOrWhiteSpace(customer.ShopName))
            return "Shop name is required.";
        if (string.IsNullOrWhiteSpace(customer.OwnerName))
            return "Owner name is required.";
        if (customer.CreditLimit < 0)
            return "Credit limit cannot be negative.";

        var code = customer.CustomerCode.Trim();
        var exists = db.Customers.Any(x => x.CustomerCode == code && x.Id != currentId);
        if (exists)
            return MasterDataUiHelper.GetDuplicateMessage("Customer", "code");

        customer.CustomerCode = code;
        customer.ShopName = customer.ShopName.Trim();
        customer.OwnerName = customer.OwnerName.Trim();
        customer.Route = customer.Route.Trim();
        customer.Address = customer.Address.Trim();
        customer.ContactNumber = customer.ContactNumber.Trim();
        return null;
    }
}
