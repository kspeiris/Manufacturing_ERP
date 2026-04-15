using System.Windows;
using ManufacturingERP.Domain.Entities;

namespace ManufacturingERP.Desktop.Views;

public partial class RouteDialogWindow : Window
{
    public RoutePlan RoutePlan { get; }

    public RouteDialogWindow(RoutePlan routePlan)
    {
        InitializeComponent();
        RoutePlan = routePlan;
        DataContext = RoutePlan;
    }

    private void Save_Click(object sender, RoutedEventArgs e) => DialogResult = true;
    private void Cancel_Click(object sender, RoutedEventArgs e) => DialogResult = false;
}
