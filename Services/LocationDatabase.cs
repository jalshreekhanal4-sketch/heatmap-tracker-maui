using HeatMapTracker.Models;
using SQLite;

namespace HeatMapTracker.Services;

/// <summary>
/// Async SQLite store for recorded GPS fixes. The connection is opened lazily on first
/// use so that application start-up is not blocked by disk I/O.
/// </summary>
public class LocationDatabase
{
    private SQLiteAsyncConnection? _connection;

    /// <summary>Full path to the database file inside the platform's app-data directory.</summary>
    public static string DatabasePath =>
        Path.Combine(FileSystem.AppDataDirectory, "locations.db3");

    /// <summary>
    /// Opens the connection and creates the schema on first call. Subsequent calls
    /// return the cached connection.
    /// </summary>
    private async Task<SQLiteAsyncConnection> GetConnectionAsync()
    {
        if (_connection is not null)
        {
            return _connection;
        }

        _connection = new SQLiteAsyncConnection(
            DatabasePath,
            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);

        await _connection.CreateTableAsync<LocationPoint>();
        return _connection;
    }

    /// <summary>Persists a single fix and returns the number of rows written.</summary>
    public async Task<int> AddPointAsync(LocationPoint point)
    {
        ArgumentNullException.ThrowIfNull(point);

        SQLiteAsyncConnection connection = await GetConnectionAsync();
        return await connection.InsertAsync(point);
    }

    /// <summary>Returns every recorded fix, oldest first.</summary>
    public async Task<List<LocationPoint>> GetAllPointsAsync()
    {
        SQLiteAsyncConnection connection = await GetConnectionAsync();
        return await connection.Table<LocationPoint>()
                               .OrderBy(p => p.RecordedUtc)
                               .ToListAsync();
    }

    /// <summary>Returns the most recently recorded fix, or <c>null</c> if the table is empty.</summary>
    public async Task<LocationPoint?> GetLatestPointAsync()
    {
        SQLiteAsyncConnection connection = await GetConnectionAsync();
        return await connection.Table<LocationPoint>()
                               .OrderByDescending(p => p.RecordedUtc)
                               .FirstOrDefaultAsync();
    }

    /// <summary>Returns the total number of recorded fixes.</summary>
    public async Task<int> GetPointCountAsync()
    {
        SQLiteAsyncConnection connection = await GetConnectionAsync();
        return await connection.Table<LocationPoint>().CountAsync();
    }

    /// <summary>Deletes every recorded fix. Used by the Clear command.</summary>
    public async Task<int> ClearAllAsync()
    {
        SQLiteAsyncConnection connection = await GetConnectionAsync();
        return await connection.DeleteAllAsync<LocationPoint>();
    }
}
