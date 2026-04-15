using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ManufacturingERP.Application.DTOs;
using ManufacturingERP.Application.Services;
using System.Collections.ObjectModel;

namespace ManufacturingERP.Desktop.ViewModels;

public partial class AccountingViewModel : ViewModelBase
{
    private readonly AccountingService _accountingService;

    public ObservableCollection<JournalLineEditor> Lines { get; } = new();
    public ObservableCollection<TrialBalanceRowDto> TrialBalanceRows { get; } = new();

    [ObservableProperty] private string _description = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public AccountingViewModel(AccountingService accountingService)
    {
        _accountingService = accountingService;
        Lines.Add(new JournalLineEditor());
        Lines.Add(new JournalLineEditor());
        _ = LoadTrialBalanceAsync();
    }

    [RelayCommand] private void AddLine() => Lines.Add(new JournalLineEditor());

    [RelayCommand]
    private async Task SaveJournalEntryAsync()
    {
        var validLines = Lines.Where(x => !string.IsNullOrWhiteSpace(x.AccountCode) && !string.IsNullOrWhiteSpace(x.AccountName))
            .Select(x => new CreateJournalLineRequest
            {
                AccountCode = x.AccountCode,
                AccountName = x.AccountName,
                Debit = x.Debit,
                Credit = x.Credit
            }).ToList();

        var result = await _accountingService.CreateJournalEntryAsync(new CreateJournalEntryRequest
        {
            Description = Description,
            Lines = validLines
        });

        StatusMessage = result.IsSuccess ? $"Journal saved: {result.Message}" : result.Message;
        if (result.IsSuccess)
        {
            Description = string.Empty;
            Lines.Clear();
            Lines.Add(new JournalLineEditor());
            Lines.Add(new JournalLineEditor());
            await LoadTrialBalanceAsync();
        }
    }

    [RelayCommand]
    public async Task LoadTrialBalanceAsync()
    {
        TrialBalanceRows.Clear();
        foreach (var row in await _accountingService.GetTrialBalanceAsync()) TrialBalanceRows.Add(row);
    }
}

public partial class JournalLineEditor : ObservableObject
{
    [ObservableProperty] private string _accountCode = string.Empty;
    [ObservableProperty] private string _accountName = string.Empty;
    [ObservableProperty] private decimal _debit;
    [ObservableProperty] private decimal _credit;
}
