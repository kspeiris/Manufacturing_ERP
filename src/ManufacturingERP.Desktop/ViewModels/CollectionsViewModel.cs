using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ManufacturingERP.Application.Services;
using ManufacturingERP.Domain.Entities;
using ManufacturingERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using System.Collections.ObjectModel;

namespace ManufacturingERP.Desktop.ViewModels;

public partial class CollectionsViewModel : ViewModelBase
{
    private readonly SalesService _salesService;
    private readonly MasterDataValidationService _validationService;

    public ObservableCollection<Customer> Customers { get; } = new();
    public ObservableCollection<CollectionEntry> Collections { get; } = new();

    [ObservableProperty] private Customer? _selectedCustomer;
    [ObservableProperty] private decimal _amount;
    [ObservableProperty] private string _referenceNo = string.Empty;
    [ObservableProperty] private string _notes = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public decimal SelectedCustomerOutstanding => SelectedCustomer?.OutstandingBalance ?? 0;

    public CollectionsViewModel(SalesService salesService, MasterDataValidationService validationService)
    {
        _salesService = salesService;
        _validationService = validationService;
        _ = LoadAsync();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        Customers.Clear();
        foreach (var item in await db.Customers.OrderBy(x => x.ShopName).ToListAsync()) Customers.Add(item);

        Collections.Clear();
        foreach (var item in await _salesService.GetCollectionsAsync()) Collections.Add(item);
        OnPropertyChanged(nameof(SelectedCustomerOutstanding));
    }

    [RelayCommand]
    private async Task SaveCollectionAsync()
    {
        if (SelectedCustomer is null)
        {
            StatusMessage = "Select a customer.";
            return;
        }

        var amountCheck = _validationService.ValidateNonNegative("Amount", Amount);
        if (!amountCheck.ok || Amount <= 0)
        {
            StatusMessage = "Enter a valid collection amount.";
            return;
        }

        if (Amount > SelectedCustomer.OutstandingBalance)
        {
            StatusMessage = "Collection amount cannot exceed customer outstanding balance.";
            return;
        }

        var refCheck = _validationService.ValidateRequired("Reference No", ReferenceNo);
        if (!refCheck.ok)
        {
            StatusMessage = refCheck.message;
            return;
        }

        var result = await _salesService.RegisterCollectionAsync(SelectedCustomer.Id, Amount, ReferenceNo, Notes);
        StatusMessage = result.Message;

        if (result.IsSuccess)
        {
            Amount = 0;
            ReferenceNo = string.Empty;
            Notes = string.Empty;
            await LoadAsync();
        }
    }

    partial void OnSelectedCustomerChanged(Customer? value)
    {
        OnPropertyChanged(nameof(SelectedCustomerOutstanding));
    }
}
