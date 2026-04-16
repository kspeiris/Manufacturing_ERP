using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;

namespace ManufacturingERP.Desktop.ViewModels;

public partial class ReportViewerViewModel : ViewModelBase
{
    private readonly List<string> _allExportFiles = [];
    private readonly List<string> _allTemplateFiles = [];

    public ObservableCollection<string> ExportFiles { get; } = new();
    public ObservableCollection<string> TemplateFiles { get; } = new();

    [ObservableProperty] private string _selectedFilePath = string.Empty;
    [ObservableProperty] private string _statusMessage = string.Empty;
    [ObservableProperty] private string _searchText = string.Empty;

    public ReportViewerViewModel()
    {
        _ = LoadAsync();
    }

    [RelayCommand]
    public Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            _allExportFiles.Clear();
            _allTemplateFiles.Clear();

            var exportDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Exports");
            if (Directory.Exists(exportDir))
            {
                _allExportFiles.AddRange(new DirectoryInfo(exportDir).GetFiles()
                    .OrderByDescending(x => x.LastWriteTimeUtc)
                    .Select(x => x.FullName));
            }

            var templateDir = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Reports", "Templates");
            if (Directory.Exists(templateDir))
            {
                _allTemplateFiles.AddRange(Directory.GetFiles(templateDir).OrderBy(x => x));
            }

            ApplyFilter();
            SelectedFilePath = ExportFiles.FirstOrDefault() ?? TemplateFiles.FirstOrDefault() ?? string.Empty;
            StatusMessage = $"Found {ExportFiles.Count} exported files and {TemplateFiles.Count} templates.";
        }
        finally
        {
            IsBusy = false;
        }

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

        StatusMessage = $"Previewing {Path.GetFileName(SelectedFilePath)}";
    }

    partial void OnSearchTextChanged(string value) => ApplyFilter();

    private void ApplyFilter()
    {
        var term = SearchText.Trim();
        var exportFiles = string.IsNullOrWhiteSpace(term)
            ? _allExportFiles
            : _allExportFiles.Where(x => Path.GetFileName(x).Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();
        var templateFiles = string.IsNullOrWhiteSpace(term)
            ? _allTemplateFiles
            : _allTemplateFiles.Where(x => Path.GetFileName(x).Contains(term, StringComparison.OrdinalIgnoreCase)).ToList();

        ExportFiles.Clear();
        foreach (var file in exportFiles)
            ExportFiles.Add(file);

        TemplateFiles.Clear();
        foreach (var file in templateFiles)
            TemplateFiles.Add(file);
    }
}
