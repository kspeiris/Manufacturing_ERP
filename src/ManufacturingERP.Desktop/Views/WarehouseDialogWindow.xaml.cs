using System.Windows;
using ManufacturingERP.Domain.Entities;

namespace ManufacturingERP.Desktop.Views;

public partial class WarehouseDialogWindow : Window
{
    public Warehouse Warehouse { get; }

    public WarehouseDialogWindow(Warehouse warehouse)
    {
        InitializeComponent();
        Warehouse = warehouse;
        DataContext = Warehouse;
    }

    private void Save_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
