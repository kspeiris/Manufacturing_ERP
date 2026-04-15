using ManufacturingERP.Desktop.ViewModels;

namespace ManufacturingERP.Desktop.Views;

public partial class LoginWindow : System.Windows.Window
{
    private readonly LoginViewModel _viewModel;

    public LoginWindow(LoginViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = _viewModel;
    }

    private void PasswordBox_OnPasswordChanged(object sender, System.Windows.RoutedEventArgs e)
    {
        _viewModel.Password = PasswordBox.Password;
    }

    private async void LoginButton_OnClick(object sender, System.Windows.RoutedEventArgs e)
    {
        if (await _viewModel.LoginAsync())
        {
            App.OpenMainWindow();
            Close();
        }
    }
}
