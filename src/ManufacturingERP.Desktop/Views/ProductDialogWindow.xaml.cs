using System.Windows;
using ManufacturingERP.Domain.Entities;

namespace ManufacturingERP.Desktop.Views;

public partial class ProductDialogWindow : Window
{
    public Product Product { get; }

    public ProductDialogWindow(Product product)
    {
        InitializeComponent();
        Product = product;
        DataContext = Product;
    }

    private void Save_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
