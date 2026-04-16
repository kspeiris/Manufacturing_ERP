using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ManufacturingERP.Application.Services;
using ManufacturingERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace ManufacturingERP.Desktop.ViewModels;

public partial class LoginViewModel : ViewModelBase
{
    private readonly PasswordHasherService _passwordHasherService;
    private readonly CurrentUserService _currentUserService;

    [ObservableProperty] private string _username = "admin";
    [ObservableProperty] private string _password = "admin123";
    [ObservableProperty] private string _errorMessage = string.Empty;

    public LoginViewModel(PasswordHasherService passwordHasherService, CurrentUserService currentUserService)
    {
        _passwordHasherService = passwordHasherService;
        _currentUserService = currentUserService;
    }

    [RelayCommand]
    public async Task<bool> LoginAsync()
    {
        using var scope = App.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var user = await db.Users.FirstOrDefaultAsync(x => x.Username == Username);
        if (user is null || !_passwordHasherService.Verify(Password, user.PasswordHash))
        {
            ErrorMessage = "Invalid username or password.";
            return false;
        }

        if (!user.IsActive)
        {
            ErrorMessage = "This user account is disabled.";
            return false;
        }

        _currentUserService.Set(user);
        ErrorMessage = string.Empty;
        return true;
    }
}
