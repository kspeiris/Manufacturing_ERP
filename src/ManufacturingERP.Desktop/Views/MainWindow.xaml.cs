using ManufacturingERP.Desktop.ViewModels;

namespace ManufacturingERP.Desktop.Views;

public partial class MainWindow : System.Windows.Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }
}
