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
                FROM auction_items
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
                FROM bids
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
                UPDATE auction_items
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
                FROM notifications
                WHERE auction_item_id = @auctionItemId
                  AND user_id = @userId
                  AND type = @type
                LIMIT 1
                """;

            const string insertNotificationSql = """
                INSERT INTO notifications
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
                        "Congratulations! You won the auction"
                    );
                    winnerNotificationCmd.Parameters.AddWithValue(
                        "body",
                            $"""
                            Hello!

                            Congratulations, you have won the auction.

                            Item:
                            {itemTitle}

                            Auction ID:
                            {auctionItemId}

                            Our system has successfully closed the auction and recorded you as the winning bidder.

                            The seller may contact you soon with further details regarding the transaction.

                            Thank you for using SZEAuction.

                            Best regards,
                            SZEAuction Team
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
                    "Your auction has ended"
                );
                sellerNotificationCmd.Parameters.AddWithValue(
                    "body",
                    winnerUserId.HasValue
                    ? $"""
                    Hello!

                    Your auction has ended successfully.

                    Item:
                    {itemTitle}

                    Auction ID:
                    {auctionItemId}

                    A winning bidder has been selected for your item.

                    You may now proceed with contacting the buyer to complete the transaction.

                    Thank you for using SZEAuction.

                    Best regards,
                    SZEAuction Team
                    """
                    : $"""
                    Hello!

                    Your auction has ended.

                    Item:
                    {itemTitle}

                    Auction ID:
                    {auctionItemId}

                    Unfortunately, no bids were placed on this item.

                    You may relist the item if you wish.

                    Best regards,
                    SZEAuction Team
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
            FROM auction_items
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
        Console.WriteLine($"Expired open auctions count: {auctionIds.Count}");

        foreach (var auctionId in auctionIds)
        {
            Console.WriteLine($"Processing auction {auctionId}");
            await CloseExpiredAuctionAsync(auctionId);
        }
    }
}