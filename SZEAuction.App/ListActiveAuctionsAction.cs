using System.Globalization;

namespace SZEAuction.App;

public static class ListActiveAuctionsAction
{
    public static async Task ExecuteAsync(AuctionRepository auctionRepo)
    {
        Console.WriteLine("\n--- Aktív aukciók ---");

        var auctions = await auctionRepo.ListActiveAuctionsAsync();

        if (auctions.Count == 0)
        {
            Console.WriteLine("Jelenleg nincs aktív aukció.");
            return;
        }

        Console.WriteLine($"{"#",-4} {"Cím",-30} {"Kezdő ár",10} {"Leg. licit",10} {"Min. lépés",10}  {"Lezárás"}");
        Console.WriteLine(new string('-', 90));

        // Végigiterálunk a listán és kiírjuk őket egy táblázatos formában
        for (int i = 0; i < auctions.Count; i++)
        {
            var a = auctions[i];
            var highestStr = a.CurrentHighestBid.HasValue
                ? $"{a.CurrentHighestBid.Value,10:N2}"
                : $"{"nincs",10}";

            Console.WriteLine(
                $"{i + 1,-4} {Truncate(a.Title, 30),-30} {a.StartPrice,10:N2} {highestStr} {a.MinIncrement,10:N2}  {a.CloseTime.ToLocalTime():yyyy-MM-dd HH:mm}");
        }
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..(max - 1)] + "…";
}