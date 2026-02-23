using Npgsql;
namespace SZEAuction.App;

public sealed class UserRepository
{
    private readonly NpgsqlConnection _connection;

    public UserRepository(NpgsqlConnection connection)
    {
        _connection = connection;
    }

    public async Task<DbUser?> FindByUsernameAsync(string username, CancellationToken ct = default)
    {
        const string sql = """
            SELECT user_id, username, password_hash
            FROM public.users
            WHERE username = @username
            """;

        await using var cmd = new NpgsqlCommand(sql, _connection);
        cmd.Parameters.AddWithValue("username", username);

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        if (!await reader.ReadAsync(ct))
            return null;

        return new DbUser(
            reader.GetInt32(0),
            reader.GetString(1),
            reader.GetString(2)
        );
    }
}
