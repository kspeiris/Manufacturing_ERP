using System.Windows;
using ManufacturingERP.Domain.Entities;

namespace ManufacturingERP.Desktop.Views;

public partial class VehicleDialogWindow : Window
{
    public Vehicle Vehicle { get; }

    public VehicleDialogWindow(Vehicle vehicle)
    {
        InitializeComponent();
        Vehicle = vehicle;
        DataContext = Vehicle;
    }

    private void Save_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
