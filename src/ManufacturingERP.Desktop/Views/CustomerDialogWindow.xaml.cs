using System.Windows;
using ManufacturingERP.Domain.Entities;

namespace ManufacturingERP.Desktop.Views;

public partial class CustomerDialogWindow : Window
{
    public Customer Customer { get; }

    public CustomerDialogWindow(Customer customer)
    {
        InitializeComponent();
        Customer = customer;
        DataContext = Customer;
    }

    private void Save_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
