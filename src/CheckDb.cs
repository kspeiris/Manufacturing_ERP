using System;
using Microsoft.Data.Sqlite;

class Program
{
    static void Main()
    {
        var dbPath = @"c:\Projects\ManufacturingERP\src\ManufacturingERP.Desktop\bin\Debug\net8.0-windows\manufacturing_erp.db";
        using var connection = new SqliteConnection($"Data Source={dbPath}");
        connection.Open();
        
        using var cmd1 = connection.CreateCommand();
        cmd1.CommandText = "SELECT COUNT(*) FROM SalesInvoices";
        var salesCount = cmd1.ExecuteScalar();
        
        using var cmd2 = connection.CreateCommand();
        cmd2.CommandText = "SELECT COUNT(*) FROM PurchaseOrders";
        var poCount = cmd2.ExecuteScalar();

        Console.WriteLine($"SalesInvoices Count: {salesCount}");
        Console.WriteLine($"PurchaseOrders Count: {poCount}");
    }
}
