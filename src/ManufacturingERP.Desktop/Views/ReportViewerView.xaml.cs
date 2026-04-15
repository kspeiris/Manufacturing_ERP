using System;
using System.IO;
using System.Windows.Controls;

namespace ManufacturingERP.Desktop.Views;

public partial class ReportViewerView : UserControl
{
    public ReportViewerView()
    {
        InitializeComponent();
    }

    private void ExportFilesList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (ExportFilesList.SelectedItem is not string path || !File.Exists(path))
            return;

        try
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();
            if (ext is ".html" or ".htm")
                PreviewBrowser.Navigate(new Uri(path));
            else
                PreviewBrowser.NavigateToString($"<html><body style='font-family:Segoe UI;padding:24px'><h2>Preview not embedded for this file type</h2><p>{System.Security.SecurityElement.Escape(path)}</p><p>Open the file externally or generate HTML output.</p></body></html>");
        }
        catch
        {
            PreviewBrowser.NavigateToString("<html><body style='font-family:Segoe UI;padding:24px'><h2>Preview failed</h2></body></html>");
        }
    }
}
