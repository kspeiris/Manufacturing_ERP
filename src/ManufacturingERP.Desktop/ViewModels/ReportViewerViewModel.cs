using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ManufacturingERP.Desktop.ViewModels;

public partial class ReportViewerViewModel : ViewModelBase
{
    public ObservableCollection<string> ExportFiles { get; } = new();
    public ObservableCollection<string> TemplateFiles { get; } = new();

    [ObservableProperty] private string _selectedFilePath = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;

    public ReportViewerViewModel()
    {
        _ = LoadAsync();
    }

    [RelayCommand]
    public Task LoadAsync()
    {
        ExportFiles.Clear();
        TemplateFiles.Clear();

        var exportDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Exports");
        if (Directory.Exists(exportDir))
        {
            foreach (var file in new DirectoryInfo(exportDir).GetFiles().OrderByDescending(x => x.LastWriteTimeUtc).Select(x => x.FullName))
                ExportFiles.Add(file);
        }

        var templateDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports", "Templates");
        if (Directory.Exists(templateDir))
        {
            foreach (var file in Directory.GetFiles(templateDir).OrderBy(x => x))
                TemplateFiles.Add(file);
        }

        SelectedFilePath = ExportFiles.FirstOrDefault() ?? TemplateFiles.FirstOrDefault() ?? string.Empty;
        StatusMessage = $"Found {ExportFiles.Count} exported files and {TemplateFiles.Count} templates.";
        return Task.CompletedTask;
    }

    [RelayCommand]
    private void OpenSelectedFile()
    {
        if (string.IsNullOrWhiteSpace(SelectedFilePath) || !File.Exists(SelectedFilePath))
        {
            StatusMessage = "Select a valid report file.";
            return;
        }

        StatusMessage = $"Selected: {SelectedFilePath}";
    }
}
