using Npgsql;

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
                ast.name              AS state_name,
                MAX(b.amount)         AS current_highest_bid
            FROM public.auction_items ai
            JOIN public.auction_states ast
                ON ast.auction_state_id = ai.auction_state_id
            LEFT JOIN public.bids b
                ON b.auction_item_id = ai.auction_item_id
            WHERE ai.auction_item_id = @itemId
            GROUP BY ai.close_time, ai.start_price, ai.min_increment, ast.name
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
        return Convert.ToInt32(result);
    }
}