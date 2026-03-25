using Npgsql;
using static System.Runtime.InteropServices.JavaScript.JSType;

namespace SZEAuction.App;

public sealed class AuctionRepository
{
    private readonly NpgsqlConnection _connection;

    public AuctionRepository(NpgsqlConnection connection)
    {
        _connection = connection;
    }

    // lekérdezés ami csak a nyílt státuszú és még le nem zárt aukciókat listázza, a jelenlegi legmagasabb licittel együtt
    public async Task<List<AuctionItem>> ListActiveAuctionsAsync(CancellationToken ct = default)
    {
        const string sql = """
            SELECT
                ai.auction_item_id,
                ai.seller_user_id,
                ai.title,
                ai.description,
                ai.close_time,
                ai.start_price,
                ai.min_increment,
                MAX(b.amount) AS current_highest_bid
            FROM public.auction_items ai
            JOIN public.auction_states ast
                ON ast.auction_state_id = ai.auction_state_id
            LEFT JOIN public.bids b
                ON b.auction_item_id = ai.auction_item_id
            WHERE ast.name = 'Open'
              AND ai.close_time > NOW()
            GROUP BY
                ai.auction_item_id,
                ai.seller_user_id,
                ai.title,
                ai.description,
                ai.close_time,
                ai.start_price,
                ai.min_increment
            ORDER BY ai.close_time ASC
            """;

        await using var cmd = new NpgsqlCommand(sql, _connection);
        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var list = new List<AuctionItem>();

        // midnen visszaadott rekordot beolvasunk és létrehozunk belőle egy AuctionItem objektumot, amit hozzáadunk a listához
        while (await reader.ReadAsync(ct))
        {
            list.Add(new AuctionItem(
                AuctionItemId: reader.GetInt32(0),
                SellerUserId: reader.GetInt32(1),
                Title: reader.GetString(2),
                Description: reader.IsDBNull(3) ? null : reader.GetString(3),
                CloseTime: reader.GetFieldValue<DateTimeOffset>(4),
                StartPrice: reader.GetDecimal(5),
                MinIncrement: reader.GetDecimal(6),
                CurrentHighestBid: reader.IsDBNull(7) ? null : reader.GetDecimal(7)
            ));
        }

        return list;
    }
    
    public async Task<int> PlaceBidAsync(
        int auctionItemId,
        int bidderUserId,
        decimal amount,
        CancellationToken ct = default)
    {
        // Validálá az aukció még nyitott és a licit összege elég magas
        const string checkSql = """
            SELECT
                ai.close_time,
                ai.start_price,
                ai.min_increment,
                ast.name AS state_name,
                (
                    SELECT b2.amount
                    FROM public.bids b2
                    WHERE b2.auction_item_id = ai.auction_item_id
                    ORDER BY b2.amount DESC, b2.created_at ASC, b2.bid_id ASC
                    LIMIT 1
                ) AS current_highest_bid,
                (
                    SELECT b3.bidder_user_id
                    FROM public.bids b3
                    WHERE b3.auction_item_id = ai.auction_item_id
                    ORDER BY b3.amount DESC, b3.created_at ASC, b3.bid_id ASC
                    LIMIT 1
                ) AS current_leader_user_id,
                ai.title
            FROM public.auction_items ai
            JOIN public.auction_states ast
                ON ast.auction_state_id = ai.auction_state_id
            WHERE ai.auction_item_id = @itemId
            """;

        await using var checkCmd = new NpgsqlCommand(checkSql, _connection);
        checkCmd.Parameters.AddWithValue("itemId", auctionItemId);
        await using var reader = await checkCmd.ExecuteReaderAsync(ct);

        if (!await reader.ReadAsync(ct))
            throw new InvalidOperationException("Az aukció nem található.");


        var closeTime = reader.GetFieldValue<DateTimeOffset>(0);
        var startPrice = reader.GetDecimal(1);
        var minIncrement = reader.GetDecimal(2);
        var stateName = reader.GetString(3);
        decimal? highestBid = reader.IsDBNull(4) ? null : reader.GetDecimal(4);
        int? previousLeaderUserId = reader.IsDBNull(5) ? null : reader.GetInt32(5);
        string itemTitle = reader.GetString(6);

        await reader.CloseAsync();

        if (stateName != "Open")
            throw new InvalidOperationException("Az aukció már nem nyitott.");

        if (closeTime <= DateTimeOffset.UtcNow)
            throw new InvalidOperationException("Az aukció lezárási ideje lejárt.");

        // Kiszámoljuk a minimum követelményt, ha van már licit, akkor az a legmagasabb + a minimum lépés, egyébként a kezdőár
        decimal minimumRequired = highestBid.HasValue
            ? highestBid.Value + minIncrement
            : startPrice;

        if (amount < minimumRequired)
            throw new InvalidOperationException(
                $"A licit összege legalább {minimumRequired:N2} Ft kell legyen " +
                $"(jelenlegi legmagasabb: {(highestBid.HasValue ? highestBid.Value.ToString("N2") : "nincs")}, " +
                $"min. lépés: {minIncrement:N2}).");

        // Mentés, ha minden validáción átmentünk, visszaadjuk az újonnan létrehozott licit azonosítóját
        const string insertSql = """
            INSERT INTO public.bids (auction_item_id, bidder_user_id, amount)
            VALUES (@itemId, @userId, @amount)
            RETURNING bid_id
            """;

        await using var insertCmd = new NpgsqlCommand(insertSql, _connection);
        insertCmd.Parameters.AddWithValue("itemId", auctionItemId);
        insertCmd.Parameters.AddWithValue("userId", bidderUserId);
        insertCmd.Parameters.AddWithValue("amount", amount);

        var result = await insertCmd.ExecuteScalarAsync(ct);
        int newBidId = Convert.ToInt32(result);

        if (previousLeaderUserId.HasValue && previousLeaderUserId.Value != bidderUserId)
        {
            const string insertNotificationSql = """
        INSERT INTO public.notifications
            (user_id, auction_item_id, status, created_at, type, subject, body, attempt_count)
        VALUES
            (@userId, @auctionItemId, 0, NOW(), @type, @subject, @body, 0)
        """;

            await using var notificationCmd = new NpgsqlCommand(insertNotificationSql, _connection);

            notificationCmd.Parameters.AddWithValue("userId", previousLeaderUserId.Value);
            notificationCmd.Parameters.AddWithValue("auctionItemId", auctionItemId);
            notificationCmd.Parameters.AddWithValue("type", "Outbid");

            notificationCmd.Parameters.AddWithValue(
                "subject",
                "You have been outbid"
            );

            notificationCmd.Parameters.AddWithValue(
                "body",
                $"""
                Hello!

                Another bidder has placed a higher bid than yours.

                Item:
                {itemTitle}

                Your previous highest bid:
                {highestBid:N0} Ft

                New highest bid:
                {amount:N0} Ft

                If you still want to win the item, you can place a higher bid.

                Best regards,
                SZEAuction Team
                """
            );

            await notificationCmd.ExecuteNonQueryAsync(ct);
        }

        return Convert.ToInt32(result);
    }

    public async Task<int> CreateAuctionAsync(
        int sellerUserId,
        string title,
        string? description,
        DateTimeOffset closeTime,
        decimal startPrice,
        decimal minIncrement,
        CancellationToken ct = default)
    {
        // Al-lekérdezéssel lekérjük az 'Open' státusz azonosítóját
        const string sql = """
            INSERT INTO public.auction_items 
            (seller_user_id, title, description, close_time, start_price, min_increment, auction_state_id)
            VALUES 
            (@sellerUserId, @title, @description, @closeTime, @startPrice, @minIncrement, 
             (SELECT auction_state_id FROM public.auction_states WHERE name = 'Open'))
            RETURNING auction_item_id
            """;

        await using var cmd = new NpgsqlCommand(sql, _connection);
        cmd.Parameters.AddWithValue("sellerUserId", sellerUserId);
        cmd.Parameters.AddWithValue("title", title);
        cmd.Parameters.AddWithValue("description", string.IsNullOrWhiteSpace(description) ? DBNull.Value : description);
        cmd.Parameters.AddWithValue("closeTime", closeTime);
        cmd.Parameters.AddWithValue("startPrice", startPrice);
        cmd.Parameters.AddWithValue("minIncrement", minIncrement);

        var result = await cmd.ExecuteScalarAsync(ct);
        return Convert.ToInt32(result);
    }

    public async Task<List<AuctionItem>> ListMyAuctionsAsync(
        int sellerUserId,
        int limit = 10,
        CancellationToken ct = default)
    {
        // Itt nem szűrünk rá a nyitott státuszra, és a lejártakat is engedjük
        const string sql = """
            SELECT
                ai.auction_item_id,
                ai.seller_user_id,
                ai.title,
                ai.description,
                ai.close_time,
                ai.start_price,
                ai.min_increment,
                MAX(b.amount) AS current_highest_bid
            FROM public.auction_items ai
            LEFT JOIN public.bids b
                ON b.auction_item_id = ai.auction_item_id
            WHERE ai.seller_user_id = @sellerUserId
            GROUP BY
                ai.auction_item_id,
                ai.seller_user_id,
                ai.title,
                ai.description,
                ai.close_time,
                ai.start_price,
                ai.min_increment
            ORDER BY ai.close_time DESC
            LIMIT @limit
            """;

        await using var cmd = new NpgsqlCommand(sql, _connection);
        cmd.Parameters.AddWithValue("sellerUserId", sellerUserId);
        cmd.Parameters.AddWithValue("limit", limit);

        await using var reader = await cmd.ExecuteReaderAsync(ct);

        var list = new List<AuctionItem>();

        while (await reader.ReadAsync(ct))
        {
            list.Add(new AuctionItem(
                AuctionItemId: reader.GetInt32(0),
                SellerUserId: reader.GetInt32(1),
                Title: reader.GetString(2),
                Description: reader.IsDBNull(3) ? null : reader.GetString(3),
                CloseTime: reader.GetFieldValue<DateTimeOffset>(4),
                StartPrice: reader.GetDecimal(5),
                MinIncrement: reader.GetDecimal(6),
                CurrentHighestBid: reader.IsDBNull(7) ? null : reader.GetDecimal(7)
            ));
        }

        return list;
    }
}