using CommunityToolkit.Mvvm.ComponentModel;

namespace ManufacturingERP.Desktop.ViewModels;

public partial class ViewModelBase : ObservableObject
{
    [ObservableProperty] private bool _isBusy;
}
