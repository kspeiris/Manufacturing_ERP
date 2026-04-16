using System;
using System.IO;
using System.Net;
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
            {
                PreviewBrowser.Navigate(new Uri(path));
                return;
            }

            if (ext is ".txt" or ".log" or ".csv" or ".json")
            {
                var content = File.ReadAllText(path);
                PreviewBrowser.NavigateToString($"""
                    <html>
                    <body style='font-family:Segoe UI;padding:24px;background:#f8fafc;color:#0f172a'>
                        <h2 style='margin-top:0'>Embedded Preview</h2>
                        <p>{WebUtility.HtmlEncode(path)}</p>
                        <pre style='white-space:pre-wrap;background:white;border:1px solid #cbd5e1;border-radius:12px;padding:16px'>{WebUtility.HtmlEncode(content)}</pre>
                    </body>
                    </html>
                    """);
                return;
            }

            PreviewBrowser.NavigateToString($"<html><body style='font-family:Segoe UI;padding:24px'><h2>Preview not embedded for this file type</h2><p>{System.Security.SecurityElement.Escape(path)}</p><p>Open the file externally or generate HTML output.</p></body></html>");
        }
        catch
        {
            PreviewBrowser.NavigateToString("<html><body style='font-family:Segoe UI;padding:24px'><h2>Preview failed</h2></body></html>");
        }
    }
}
