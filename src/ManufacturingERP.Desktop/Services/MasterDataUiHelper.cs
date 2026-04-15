using Microsoft.EntityFrameworkCore;

namespace ManufacturingERP.Desktop.Services;

internal static class MasterDataUiHelper
{
    public static bool ConfirmDelete(string entityName, string displayValue)
    {
        var message = $"Delete {entityName} '{displayValue}'?";
        var result = System.Windows.MessageBox.Show(
            message,
            $"Delete {entityName}",
            System.Windows.MessageBoxButton.YesNo,
            System.Windows.MessageBoxImage.Warning);

        return result == System.Windows.MessageBoxResult.Yes;
    }

    public static string GetDuplicateMessage(string entityName, string fieldLabel)
        => $"{entityName} with the same {fieldLabel} already exists.";

    public static string GetDeleteBlockedMessage(string entityName)
        => $"{entityName} cannot be deleted because it is referenced by other records.";

    public static string? TryGetFriendlySaveError(DbUpdateException exception, string entityName, string duplicateFieldLabel)
    {
        var message = exception.InnerException?.Message ?? exception.Message;
        if (message.Contains("UNIQUE", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("duplicate", StringComparison.OrdinalIgnoreCase))
        {
            return GetDuplicateMessage(entityName, duplicateFieldLabel);
        }

        if (message.Contains("FOREIGN KEY", StringComparison.OrdinalIgnoreCase) ||
            message.Contains("constraint", StringComparison.OrdinalIgnoreCase))
        {
            return GetDeleteBlockedMessage(entityName);
        }

        return null;
    }
}
