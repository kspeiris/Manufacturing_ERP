using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ManufacturingERP.Application.DTOs;
using ManufacturingERP.Application.Services;
using ManufacturingERP.Desktop.Services;
using ManufacturingERP.Domain.Entities;
using ManufacturingERP.Domain.Enums;
using System.Collections.ObjectModel;

namespace ManufacturingERP.Desktop.ViewModels;

public partial class UserManagementViewModel : ViewModelBase
{
    private readonly AuthorizationService _authorizationService;
    private readonly UserManagementService _userManagementService;
    private readonly List<User> _allUsers = [];

    public ObservableCollection<User> Users { get; } = new();
    public Array Roles => Enum.GetValues(typeof(UserRole));

    [ObservableProperty] private User? _selectedUser;
    [ObservableProperty] private string _username = string.Empty;
    [ObservableProperty] private string _password = string.Empty;
    [ObservableProperty] private string _fullName = string.Empty;
    [ObservableProperty] private UserRole _selectedRole = UserRole.Sales;
    [ObservableProperty] private bool _isActive = true;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _searchText = string.Empty;

    public UserManagementViewModel(UserManagementService userManagementService, AuthorizationService authorizationService)
    {
        _userManagementService = userManagementService;
        _authorizationService = authorizationService;
        _ = LoadAsync();
    }

    [RelayCommand]
    public async Task LoadAsync()
    {
        _allUsers.Clear();
        _allUsers.AddRange(await _userManagementService.GetUsersAsync());
        ApplyFilter();
    }

    partial void OnSelectedUserChanged(User? value)
    {
        if (value is null) return;
        Username = value.Username;
        Password = string.Empty;
        FullName = value.FullName;
        SelectedRole = value.Role;
        IsActive = value.IsActive;
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        var auth = _authorizationService.EnsureAdminAccess();
        if (!auth.IsSuccess) { StatusMessage = auth.Message; return; }

        var validationMessage = ValidateUser();
        if (validationMessage is not null)
        {
            StatusMessage = validationMessage;
            return;
        }

        var result = await _userManagementService.SaveUserAsync(new UserCrudRequest
        {
            Id = SelectedUser?.Id,
            Username = Username.Trim(),
            Password = Password,
            FullName = FullName.Trim(),
            Role = SelectedRole,
            IsActive = IsActive
        });

        StatusMessage = result.Message;
        if (result.IsSuccess)
        {
            ClearForm();
            await LoadAsync();
        }
    }

    [RelayCommand]
    private async Task DeleteAsync()
    {
        var auth = _authorizationService.EnsureAdminAccess();
        if (!auth.IsSuccess) { StatusMessage = auth.Message; return; }
        if (SelectedUser is null)
        {
            StatusMessage = "Select a user to delete.";
            return;
        }

        if (!MasterDataUiHelper.ConfirmDelete("user", SelectedUser.Username))
            return;

        var result = await _userManagementService.DeleteUserAsync(SelectedUser.Id);
        StatusMessage = result.Message;
        if (result.IsSuccess)
        {
            ClearForm();
            await LoadAsync();
        }
    }

    [RelayCommand]
    private void NewUser() => ClearForm();

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var term = SearchText.Trim();
        var filtered = string.IsNullOrWhiteSpace(term)
            ? _allUsers
            : _allUsers.Where(x =>
                x.Username.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.FullName.Contains(term, StringComparison.OrdinalIgnoreCase) ||
                x.Role.ToString().Contains(term, StringComparison.OrdinalIgnoreCase))
            .ToList();

        Users.Clear();
        foreach (var item in filtered)
            Users.Add(item);
    }

    private string? ValidateUser()
    {
        if (string.IsNullOrWhiteSpace(Username))
            return "Username is required.";
        if (string.IsNullOrWhiteSpace(FullName))
            return "Full name is required.";
        if (SelectedUser is null && string.IsNullOrWhiteSpace(Password))
            return "Password is required for new users.";

        return null;
    }

    private void ClearForm()
    {
        SelectedUser = null;
        Username = string.Empty;
        Password = string.Empty;
        FullName = string.Empty;
        SelectedRole = UserRole.Sales;
        IsActive = true;
    }
}
