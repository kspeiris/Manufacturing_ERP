using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ManufacturingERP.Application.DTOs;
using ManufacturingERP.Application.Services;
using ManufacturingERP.Domain.Entities;
using ManufacturingERP.Domain.Enums;
using ManufacturingERP.Infrastructure.Persistence;
using ManufacturingERP.Shared.Results;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Collections.ObjectModel;

namespace ManufacturingERP.Desktop.ViewModels;

public partial class AccountingSetupViewModel : ViewModelBase
{
    private readonly AccountingService _accountingService;
    private readonly CurrentUserService _currentUserService;

    public ObservableCollection<FiscalPeriod> FiscalPeriods { get; } = new();
    public ObservableCollection<Account> Accounts { get; } = new();
    public ObservableCollection<Tax> Taxes { get; } = new();
    public ObservableCollection<Voucher> Vouchers { get; } = new();
    public ObservableCollection<Account> ActiveAccounts { get; } = new();

    public Array FiscalPeriodStatuses => Enum.GetValues(typeof(FiscalPeriodStatus));
    public Array TaxTypes => Enum.GetValues(typeof(TaxType));
    public Array VoucherTypes => Enum.GetValues(typeof(VoucherType));

    [ObservableProperty] private FiscalPeriod? _selectedFiscalPeriod;
    [ObservableProperty] private Account? _selectedAccount;
    [ObservableProperty] private Tax? _selectedTax;
    [ObservableProperty] private Voucher? _selectedVoucher;
    [ObservableProperty] private string _statusMessage = string.Empty;

    [ObservableProperty] private int _fiscalYear = DateTime.Today.Year;
    [ObservableProperty] private int _periodNumber = DateTime.Today.Month;
    [ObservableProperty] private string _periodName = DateTime.Today.ToString("MMM-yyyy");
    [ObservableProperty] private DateTime _periodStartDate = new(DateTime.Today.Year, DateTime.Today.Month, 1);
    [ObservableProperty] private DateTime _periodEndDate = new(DateTime.Today.Year, DateTime.Today.Month, 1);

    [ObservableProperty] private string _accountCode = string.Empty;
    [ObservableProperty] private string _accountName = string.Empty;
    [ObservableProperty] private string _accountType = "Asset";
    [ObservableProperty] private bool _accountIsActive = true;

    [ObservableProperty] private string _taxCode = string.Empty;
    [ObservableProperty] private string _taxName = string.Empty;
    [ObservableProperty] private TaxType _taxType = TaxType.Percentage;
    [ObservableProperty] private decimal _taxRate;
    [ObservableProperty] private Account? _inputTaxAccount;
    [ObservableProperty] private Account? _outputTaxAccount;
    [ObservableProperty] private bool _taxIsActive = true;
    [ObservableProperty] private bool _taxIsDefault;

    [ObservableProperty] private VoucherType _voucherType = VoucherType.JournalVoucher;
    [ObservableProperty] private DateTime _voucherDate = DateTime.Today;
    [ObservableProperty] private string _voucherDescription = string.Empty;
    [ObservableProperty] private string _voucherReference = string.Empty;
    [ObservableProperty] private Account? _voucherDebitAccount;
    [ObservableProperty] private Account? _voucherCreditAccount;
    [ObservableProperty] private decimal _voucherAmount;

    public AccountingSetupViewModel(AccountingService accountingService, CurrentUserService currentUserService)
    {
        _accountingService = accountingService;
        _currentUserService = currentUserService;
        PeriodEndDate = PeriodStartDate.AddMonths(1).AddDays(-1);
        _ = LoadAsync();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        try
        {
            using var scope = App.Services.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

            FiscalPeriods.Clear();
            foreach (var item in await db.FiscalPeriods.OrderByDescending(x => x.FiscalYear).ThenBy(x => x.PeriodNumber).ToListAsync())
                FiscalPeriods.Add(item);

            Accounts.Clear();
            ActiveAccounts.Clear();
            foreach (var item in await db.Accounts.OrderBy(x => x.AccountCode).ToListAsync())
            {
                Accounts.Add(item);
                if (item.IsActive) ActiveAccounts.Add(item);
            }

            Taxes.Clear();
            foreach (var item in await db.Taxes.Include(x => x.InputTaxAccount).Include(x => x.OutputTaxAccount).OrderBy(x => x.TaxCode).ToListAsync())
                Taxes.Add(item);

            Vouchers.Clear();
            foreach (var item in await db.Vouchers.Include(x => x.FiscalPeriod).OrderByDescending(x => x.VoucherDate).ThenByDescending(x => x.Id).Take(100).ToListAsync())
                Vouchers.Add(item);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load accounting setup: {ex.Message}";
        }
    }

    [RelayCommand]
    private void NewFiscalPeriod()
    {
        SelectedFiscalPeriod = null;
        FiscalYear = DateTime.Today.Year;
        PeriodNumber = DateTime.Today.Month;
        PeriodStartDate = new DateTime(FiscalYear, PeriodNumber, 1);
        PeriodEndDate = PeriodStartDate.AddMonths(1).AddDays(-1);
        PeriodName = PeriodStartDate.ToString("MMM-yyyy");
    }

    [RelayCommand]
    private async Task SaveFiscalPeriodAsync()
    {
        if (PeriodNumber is < 1 or > 12)
        {
            StatusMessage = "Period number must be between 1 and 12.";
            return;
        }
        if (PeriodEndDate.Date < PeriodStartDate.Date)
        {
            StatusMessage = "Period end date cannot be before start date.";
            return;
        }
        if (string.IsNullOrWhiteSpace(PeriodName))
        {
            StatusMessage = "Period name is required.";
            return;
        }

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = SelectedFiscalPeriod is null ? new FiscalPeriod() : await db.FiscalPeriods.FirstAsync(x => x.Id == SelectedFiscalPeriod.Id);
        entity.FiscalYear = FiscalYear;
        entity.PeriodNumber = PeriodNumber;
        entity.PeriodName = PeriodName.Trim();
        entity.StartDate = PeriodStartDate.Date;
        entity.EndDate = PeriodEndDate.Date;
        if (SelectedFiscalPeriod is null) db.FiscalPeriods.Add(entity);
        try
        {
            await db.SaveChangesAsync();
            StatusMessage = "Fiscal period saved.";
            await LoadAsync();
        }
        catch (DbUpdateException)
        {
            StatusMessage = "Unable to save fiscal period. Check for duplicate year and period number.";
        }
    }

    [RelayCommand]
    private async Task CloseFiscalPeriodAsync()
    {
        if (_currentUserService.CurrentUserId is null)
        {
            StatusMessage = "A signed-in user is required to close periods.";
            return;
        }

        await ChangeFiscalPeriodAsync(x => x.Close(_currentUserService.CurrentUserId ?? 0), "Fiscal period closed.");
    }

    [RelayCommand]
    private async Task LockFiscalPeriodAsync()
    {
        if (_currentUserService.CurrentUserId is null)
        {
            StatusMessage = "A signed-in user is required to lock periods.";
            return;
        }

        await ChangeFiscalPeriodAsync(x => x.Lock(_currentUserService.CurrentUserId ?? 0), "Fiscal period locked.");
    }

    [RelayCommand]
    private async Task ReopenFiscalPeriodAsync()
    {
        await ChangeFiscalPeriodAsync(x => x.Reopen(), "Fiscal period reopened.");
    }

    [RelayCommand]
    private void NewAccount()
    {
        SelectedAccount = null;
        AccountCode = string.Empty;
        AccountName = string.Empty;
        AccountType = "Asset";
        AccountIsActive = true;
    }

    [RelayCommand]
    private async Task SaveAccountAsync()
    {
        if (string.IsNullOrWhiteSpace(AccountCode) || string.IsNullOrWhiteSpace(AccountName) || string.IsNullOrWhiteSpace(AccountType))
        {
            StatusMessage = "Account code, name, and type are required.";
            return;
        }

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = SelectedAccount is null ? new Account() : await db.Accounts.FirstAsync(x => x.Id == SelectedAccount.Id);
        entity.AccountCode = AccountCode.Trim();
        entity.AccountName = AccountName.Trim();
        entity.AccountType = AccountType.Trim();
        entity.IsActive = AccountIsActive;
        if (SelectedAccount is null) db.Accounts.Add(entity);
        try
        {
            await db.SaveChangesAsync();
            StatusMessage = "Account saved.";
            await LoadAsync();
        }
        catch (DbUpdateException)
        {
            StatusMessage = "Unable to save account. Account code must be unique.";
        }
    }

    [RelayCommand]
    private void NewTax()
    {
        SelectedTax = null;
        TaxCode = string.Empty;
        TaxName = string.Empty;
        TaxType = TaxType.Percentage;
        TaxRate = 0;
        InputTaxAccount = null;
        OutputTaxAccount = null;
        TaxIsActive = true;
        TaxIsDefault = false;
    }

    [RelayCommand]
    private async Task SaveTaxAsync()
    {
        if (string.IsNullOrWhiteSpace(TaxCode) || string.IsNullOrWhiteSpace(TaxName))
        {
            StatusMessage = "Tax code and name are required.";
            return;
        }
        if (TaxRate < 0)
        {
            StatusMessage = "Tax rate cannot be negative.";
            return;
        }

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = SelectedTax is null ? new Tax() : await db.Taxes.FirstAsync(x => x.Id == SelectedTax.Id);
        entity.TaxCode = TaxCode.Trim();
        entity.TaxName = TaxName.Trim();
        entity.TaxType = TaxType;
        entity.Rate = TaxRate;
        entity.InputTaxAccountId = InputTaxAccount?.Id;
        entity.OutputTaxAccountId = OutputTaxAccount?.Id;
        entity.IsActive = TaxIsActive;
        entity.IsDefault = TaxIsDefault;
        if (SelectedTax is null) db.Taxes.Add(entity);
        try
        {
            await db.SaveChangesAsync();
            StatusMessage = "Tax saved.";
            await LoadAsync();
        }
        catch (DbUpdateException)
        {
            StatusMessage = "Unable to save tax. Tax code must be unique.";
        }
    }

    [RelayCommand]
    private async Task CreateVoucherAsync()
    {
        if (VoucherDebitAccount is null || VoucherCreditAccount is null || VoucherAmount <= 0)
        {
            StatusMessage = "Select debit and credit accounts and enter an amount.";
            return;
        }
        if (VoucherDebitAccount.Id == VoucherCreditAccount.Id)
        {
            StatusMessage = "Debit and credit accounts must be different.";
            return;
        }
        if (string.IsNullOrWhiteSpace(VoucherDescription))
        {
            StatusMessage = "Voucher description is required.";
            return;
        }
        if (_currentUserService.CurrentUserId is null)
        {
            StatusMessage = "A signed-in user is required to create vouchers.";
            return;
        }

        var result = await _accountingService.CreateVoucherAsync(new CreateVoucherRequest
        {
            VoucherType = VoucherType,
            VoucherDate = VoucherDate,
            Description = VoucherDescription,
            Reference = VoucherReference,
            Lines =
            [
                new() { AccountCode = VoucherDebitAccount.AccountCode, Description = VoucherDescription, Debit = VoucherAmount },
                new() { AccountCode = VoucherCreditAccount.AccountCode, Description = VoucherDescription, Credit = VoucherAmount }
            ]
        });

        StatusMessage = result.IsSuccess ? $"Voucher created: {result.Message}" : result.Message;
        if (result.IsSuccess)
        {
            VoucherDescription = string.Empty;
            VoucherReference = string.Empty;
            VoucherAmount = 0;
            await LoadAsync();
        }
    }

    [RelayCommand] private async Task SubmitVoucherAsync() => await RunVoucherActionAsync(x => _accountingService.SubmitVoucherAsync(x.Id));
    [RelayCommand] private async Task ApproveVoucherAsync() => await RunVoucherActionAsync(x => _accountingService.ApproveVoucherAsync(x.Id));
    [RelayCommand] private async Task PostVoucherAsync() => await RunVoucherActionAsync(async x => await _accountingService.PostVoucherAsync(x.Id));
    [RelayCommand] private async Task ReverseVoucherAsync() => await RunVoucherActionAsync(async x => await _accountingService.ReverseVoucherAsync(x.Id));

    partial void OnSelectedFiscalPeriodChanged(FiscalPeriod? value)
    {
        if (value is null) return;
        FiscalYear = value.FiscalYear;
        PeriodNumber = value.PeriodNumber;
        PeriodName = value.PeriodName;
        PeriodStartDate = value.StartDate;
        PeriodEndDate = value.EndDate;
    }

    partial void OnSelectedAccountChanged(Account? value)
    {
        if (value is null) return;
        AccountCode = value.AccountCode;
        AccountName = value.AccountName;
        AccountType = value.AccountType;
        AccountIsActive = value.IsActive;
    }

    partial void OnSelectedTaxChanged(Tax? value)
    {
        if (value is null) return;
        TaxCode = value.TaxCode;
        TaxName = value.TaxName;
        TaxType = value.TaxType;
        TaxRate = value.Rate;
        InputTaxAccount = ActiveAccounts.FirstOrDefault(x => x.Id == value.InputTaxAccountId);
        OutputTaxAccount = ActiveAccounts.FirstOrDefault(x => x.Id == value.OutputTaxAccountId);
        TaxIsActive = value.IsActive;
        TaxIsDefault = value.IsDefault;
    }

    private async Task ChangeFiscalPeriodAsync(Action<FiscalPeriod> action, string successMessage)
    {
        if (SelectedFiscalPeriod is null)
        {
            StatusMessage = "Select a fiscal period.";
            return;
        }

        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var entity = await db.FiscalPeriods.FirstAsync(x => x.Id == SelectedFiscalPeriod.Id);
        try
        {
            action(entity);
            await db.SaveChangesAsync();
            StatusMessage = successMessage;
            await LoadAsync();
        }
        catch (InvalidOperationException ex)
        {
            StatusMessage = ex.Message;
        }
    }

    private async Task RunVoucherActionAsync(Func<Voucher, Task<Result>> action)
    {
        if (SelectedVoucher is null)
        {
            StatusMessage = "Select a voucher.";
            return;
        }

        var result = await action(SelectedVoucher);
        StatusMessage = result.Message;
        await LoadAsync();
    }
}
