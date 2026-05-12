using Npgsql;

namespace SZEAuction.App;

public sealed class AuctionClosingService
{
    private readonly NpgsqlConnection _connection;

    public AuctionClosingService(NpgsqlConnection connection)
    {
        _connection = connection;
    }

    public async Task CloseExpiredAuctionAsync(int auctionItemId)
    {
        await using var tx = await _connection.BeginTransactionAsync();

        try
        {
            const string lockAuctionSql = """
                SELECT auction_item_id, seller_user_id, title
                FROM public.auction_items
                WHERE auction_item_id = @auctionItemId
                  AND close_time <= NOW()
                  AND auction_state_id = 1
                FOR UPDATE
                """;

            int sellerUserId;
            string itemTitle;

            await using (var lockCmd = new NpgsqlCommand(lockAuctionSql, _connection, tx))
            {
                lockCmd.Parameters.AddWithValue("auctionItemId", auctionItemId);

                await using var reader = await lockCmd.ExecuteReaderAsync();
                if (!await reader.ReadAsync())
                {
                    return;
                }

                sellerUserId = reader.GetInt32(1);
                itemTitle = reader.GetString(2);
            }

            int? winningBidId = null;
            int? winnerUserId = null;

            const string winnerSql = """
                SELECT bid_id, bidder_user_id
                FROM public.bids
                WHERE auction_item_id = @auctionItemId
                ORDER BY amount DESC, created_at ASC, bid_id ASC
                LIMIT 1
                """;

            await using (var winnerCmd = new NpgsqlCommand(winnerSql, _connection, tx))
            {
                winnerCmd.Parameters.AddWithValue("auctionItemId", auctionItemId);

                await using var reader = await winnerCmd.ExecuteReaderAsync();
                if (await reader.ReadAsync())
                {
                    winningBidId = reader.GetInt32(0);
                    winnerUserId = reader.GetInt32(1);
                }
            }

            const string updateAuctionSql = """
                UPDATE public.auction_items
                SET auction_state_id = 2,
                    winning_bid_id = @winningBidId,
                    closed_at = NOW()
                WHERE auction_item_id = @auctionItemId
                """;

            await using (var updateCmd = new NpgsqlCommand(updateAuctionSql, _connection, tx))
            {
                updateCmd.Parameters.AddWithValue("auctionItemId", auctionItemId);
                updateCmd.Parameters.AddWithValue("winningBidId", (object?)winningBidId ?? DBNull.Value);

                await updateCmd.ExecuteNonQueryAsync();
            }

            const string notificationExistsSql = """
                SELECT 1
                FROM public.notifications
                WHERE auction_item_id = @auctionItemId
                  AND user_id = @userId
                  AND type = @type
                LIMIT 1
                """;

            const string insertNotificationSql = """
                INSERT INTO public.notifications
                    (user_id, auction_item_id, status, created_at, type, subject, body, attempt_count)
                VALUES
                    (@userId, @auctionItemId, 0, NOW(), @type, @subject, @body, 0)
                """;

            if (winnerUserId.HasValue)
            {
                bool winnerNotificationExists;

                await using (var existsCmd = new NpgsqlCommand(notificationExistsSql, _connection, tx))
                {
                    existsCmd.Parameters.AddWithValue("auctionItemId", auctionItemId);
                    existsCmd.Parameters.AddWithValue("userId", winnerUserId.Value);
                    existsCmd.Parameters.AddWithValue("type", "AuctionWon");

                    var existsResult = await existsCmd.ExecuteScalarAsync();
                    winnerNotificationExists = existsResult is not null;
                }

                if (!winnerNotificationExists)
                {
                    await using var winnerNotificationCmd = new NpgsqlCommand(insertNotificationSql, _connection, tx);

                    winnerNotificationCmd.Parameters.AddWithValue("auctionItemId", auctionItemId);
                    winnerNotificationCmd.Parameters.AddWithValue("userId", winnerUserId.Value);
                    winnerNotificationCmd.Parameters.AddWithValue("type", "AuctionWon");
                    winnerNotificationCmd.Parameters.AddWithValue(
                        "subject",
                        "Gratulálunk! Megnyerted az aukciót"
                    );
                    winnerNotificationCmd.Parameters.AddWithValue(
                        "body",
                        $"""
                        Kedves Felhasználó!

                        Gratulálunk, sikeresen megnyerted az aukciót.

                        Tétel neve:
                        {itemTitle}

                        Aukció azonosító:
                        {auctionItemId}

                        A rendszer lezárta az aukciót, és téged rögzített nyertes licitálóként.

                        Az eladó hamarosan felveheti veled a kapcsolatot a tranzakció további részleteivel kapcsolatban.

                        Köszönjük, hogy a SZEAuction rendszert használod.

                        Üdvözlettel:
                        SZEAuction csapat
                        """);

                    await winnerNotificationCmd.ExecuteNonQueryAsync();
                }
            }

            bool sellerNotificationExists;

            await using (var sellerExistsCmd = new NpgsqlCommand(notificationExistsSql, _connection, tx))
            {
                sellerExistsCmd.Parameters.AddWithValue("auctionItemId", auctionItemId);
                sellerExistsCmd.Parameters.AddWithValue("userId", sellerUserId);
                sellerExistsCmd.Parameters.AddWithValue("type", "AuctionSold");

                var sellerExistsResult = await sellerExistsCmd.ExecuteScalarAsync();
                sellerNotificationExists = sellerExistsResult is not null;
            }

            if (!sellerNotificationExists)
            {
                await using var sellerNotificationCmd = new NpgsqlCommand(insertNotificationSql, _connection, tx);

                sellerNotificationCmd.Parameters.AddWithValue("auctionItemId", auctionItemId);
                sellerNotificationCmd.Parameters.AddWithValue("userId", sellerUserId);
                sellerNotificationCmd.Parameters.AddWithValue("type", "AuctionSold");
                sellerNotificationCmd.Parameters.AddWithValue(
                    "subject",
                    "Lezárult az aukciód"
                );
                sellerNotificationCmd.Parameters.AddWithValue(
                    "body",
                    winnerUserId.HasValue
                    ? $"""
                    Kedves Felhasználó!

                    Az aukciód sikeresen lezárult.

                    Tétel neve:
                    {itemTitle}

                    Aukció azonosító:
                    {auctionItemId}

                    A rendszer kiválasztotta a nyertes licitálót.

                    Most már felveheted a kapcsolatot a vevővel az adásvétel további részleteinek egyeztetéséhez.

                    Köszönjük, hogy a SZEAuction rendszert használod.

                    Üdvözlettel:
                    SZEAuction csapat
                    """
                    : $"""
                    Kedves Felhasználó!

                    Az aukciód lezárult.

                    Tétel neve:
                    {itemTitle}

                    Aukció azonosító:
                    {auctionItemId}

                    Sajnos erre a tételre nem érkezett licit.

                    Ha szeretnéd, később újra meghirdetheted a terméket.

                    Üdvözlettel:
                    SZEAuction csapat
                    """);

                await sellerNotificationCmd.ExecuteNonQueryAsync();
            }

            await tx.CommitAsync();
        }
        catch
        {
            if (tx.Connection is not null)
            {
                await tx.RollbackAsync();
            }

            throw;
        }
    }

    public async Task<List<int>> GetExpiredOpenAuctionIdsAsync()
    {
        const string sql = """
            SELECT auction_item_id
            FROM public.auction_items
            WHERE close_time <= NOW()
              AND auction_state_id = 1
            ORDER BY close_time ASC;
            """;

        var results = new List<int>();

        await using var cmd = new NpgsqlCommand(sql, _connection);
        await using var reader = await cmd.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            results.Add(reader.GetInt32(0));
        }

        return results;
    }

    public async Task CloseAllExpiredAuctionsAsync()
    {
        var auctionIds = await GetExpiredOpenAuctionIdsAsync();

        System.Diagnostics.Debug.WriteLine($"Lejárt, nyitott aukciók száma: {auctionIds.Count}");

        foreach (var auctionId in auctionIds)
        {
            System.Diagnostics.Debug.WriteLine($"Aukció feldolgozása: {auctionId}");
            await CloseExpiredAuctionAsync(auctionId);
        }
    }
}