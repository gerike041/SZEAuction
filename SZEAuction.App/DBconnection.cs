using Npgsql;

namespace SZEAuction.App;

public sealed class DBconnection
{
    private readonly string _connectionString;

    public DBconnection(string connectionString)
    {
        _connectionString = connectionString
            ?? throw new ArgumentNullException(nameof(connectionString));
    }

    public async Task<NpgsqlConnection> GetOpenConnectionAsync()
    {
        var connection = new NpgsqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }
}