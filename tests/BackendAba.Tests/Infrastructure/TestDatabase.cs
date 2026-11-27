using BackendAba.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace BackendAba.Tests.Infrastructure;

/// <summary>
/// Base SQLite en memoria con las migraciones reales. Vive mientras la conexión esté abierta, así que
/// cada prueba tiene la suya y nunca toca aba.db.
/// </summary>
public sealed class TestDatabase : IDisposable
{
    private readonly SqliteConnection connection;

    public TestDatabase()
    {
        connection = new SqliteConnection("DataSource=:memory:");
        connection.Open();

        using var db = CreateContext();
        db.Database.Migrate();
    }

    public AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlite(connection)
            .Options;

        return new AppDbContext(options);
    }

    public void Dispose()
    {
        connection.Dispose();
    }
}
