using System.Collections.ObjectModel;
using System.Data;
using System.Data.Common;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ManufacturingERP.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.Extensions.DependencyInjection;

namespace ManufacturingERP.Desktop.ViewModels;

public partial class DatabaseExplorerViewModel : ViewModelBase
{
    private readonly IServiceScopeFactory _scopeFactory;

    public ObservableCollection<DatabaseTableInfo> Tables { get; } = new();

    [ObservableProperty] private DatabaseTableInfo? _selectedTable;
    [ObservableProperty] private DataView? _tableData;
    [ObservableProperty] private string _statusText = "Select a table to inspect the live database contents.";
    [ObservableProperty] private bool _isLoading;
    [ObservableProperty] private int _loadedRowCount;
    [ObservableProperty] private string _tableCaption = "No table selected";

    public DatabaseExplorerViewModel(IServiceScopeFactory scopeFactory)
    {
        _scopeFactory = scopeFactory;
        _ = LoadTablesAsync();
    }

    public async Task LoadTablesAsync()
    {
        IsLoading = true;
        try
        {
            Tables.Clear();

            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var entityTypes = db.Model.GetEntityTypes()
                .Where(x => x.GetTableName() is not null)
                .OrderBy(x => x.GetTableName())
                .ToList();

            foreach (var entityType in entityTypes)
            {
                var tableName = entityType.GetTableName() ?? entityType.DisplayName();
                var clrType = entityType.ClrType;
                var count = await GetTableRowCountAsync(db, tableName);

                Tables.Add(new DatabaseTableInfo(tableName, entityType.DisplayName(), clrType, count, entityType));
            }

            SelectedTable = Tables.FirstOrDefault();
            if (SelectedTable is null)
            {
                StatusText = "No mapped database tables were found.";
                TableData = null;
                TableCaption = "No table selected";
            }
        }
        finally
        {
            IsLoading = false;
        }
    }

    partial void OnSelectedTableChanged(DatabaseTableInfo? value)
    {
        _ = value is null ? Task.CompletedTask : LoadSelectedTableAsync(value);
    }

    [RelayCommand]
    private async Task RefreshAsync()
    {
        if (SelectedTable is null)
            await LoadTablesAsync();
        else
            await LoadSelectedTableAsync(SelectedTable);
    }

    private async Task LoadSelectedTableAsync(DatabaseTableInfo table)
    {
        IsLoading = true;
        StatusText = $"Loading {table.TableName}...";

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var rows = await LoadRowsAsync(db, table.TableName, 250);

            TableData = BuildTable(rows, table.EntityType);
            LoadedRowCount = rows.Count;
            TableCaption = $"{table.DisplayName} ({table.RowCount:N0} rows total)";
            StatusText = rows.Count == 0
                ? $"No rows found in {table.TableName}."
                : $"Showing the first {rows.Count} rows from {table.TableName}.";
        }
        catch (Exception ex)
        {
            TableData = null;
            LoadedRowCount = 0;
            TableCaption = table.DisplayName;
            StatusText = $"Failed to load {table.TableName}: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    private static async Task<int> GetTableRowCountAsync(AppDbContext db, string tableName)
    {
        await using var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(1) FROM \"{tableName}\";";
        var scalar = await command.ExecuteScalarAsync();
        return Convert.ToInt32(scalar);
    }

    private static async Task<List<Dictionary<string, object?>>> LoadRowsAsync(AppDbContext db, string tableName, int limit)
    {
        await using var connection = db.Database.GetDbConnection();
        if (connection.State != ConnectionState.Open)
            await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = $"SELECT * FROM \"{tableName}\" LIMIT {limit};";

        await using var reader = await command.ExecuteReaderAsync();
        var rows = new List<Dictionary<string, object?>>();

        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            for (var i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = await reader.IsDBNullAsync(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }

        return rows;
    }

    private static DataView BuildTable(IEnumerable<Dictionary<string, object?>> rows, IEntityType entityType)
    {
        var dataTable = new DataTable(entityType.DisplayName());
        var properties = entityType.GetProperties().ToList();

        foreach (var property in properties)
        {
            var columnType = Nullable.GetUnderlyingType(property.ClrType) ?? property.ClrType;
            if (columnType.IsEnum)
                columnType = Enum.GetUnderlyingType(columnType);
            dataTable.Columns.Add(property.Name, columnType);
        }

        foreach (var row in rows)
        {
            var values = new object?[properties.Count];

            for (var i = 0; i < properties.Count; i++)
            {
                var value = row.TryGetValue(properties[i].Name, out var rawValue) ? rawValue : null;
                values[i] = value ?? DBNull.Value;
            }

            dataTable.Rows.Add(values);
        }

        return dataTable.DefaultView;
    }
}

public sealed record DatabaseTableInfo(string TableName, string DisplayName, Type ClrType, int RowCount, IEntityType EntityType);
