using System.Windows;
using ManufacturingERP.Domain.Entities;

namespace ManufacturingERP.Desktop.Views;

public partial class SupplierDialogWindow : Window
{
    public Supplier Supplier { get; }

    public SupplierDialogWindow(Supplier supplier)
    {
        InitializeComponent();
        Supplier = supplier;
        DataContext = Supplier;
    }

    private void Save_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
