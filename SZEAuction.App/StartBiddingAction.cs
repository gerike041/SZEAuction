using System.Globalization;

namespace SZEAuction.App;

public static class StartBiddingAction
{
    public static async Task ExecuteAsync(Session session, AuctionRepository auctionRepo)
    {
        Console.WriteLine("\n--- Licitálás indítása ---");

        // Először listázzuk a tételeket
        var auctions = await auctionRepo.ListActiveAuctionsAsync();

        if (auctions.Count == 0)
        {
            Console.WriteLine("Jelenleg nincs aktív aukció.");
            return;
        }

        Console.WriteLine($"{"#",-4} {"Cím",-30} {"Kezdő ár",10} {"Leg. licit",10} {"Min. lépés",10}  {"Lezárás"}");
        Console.WriteLine(new string('-', 90));

        for (int i = 0; i < auctions.Count; i++)
        {
            var a = auctions[i];
            var highestStr = a.CurrentHighestBid.HasValue
                ? $"{a.CurrentHighestBid.Value,10:N2}"
                : $"{"nincs",10}";

            Console.WriteLine(
                $"{i + 1,-4} {Truncate(a.Title, 30),-30} {a.StartPrice,10:N2} {highestStr} {a.MinIncrement,10:N2}  {a.CloseTime.ToLocalTime():yyyy-MM-dd HH:mm}");
        }

        // Tétel kiválasztása
        Console.Write("\nAdd meg a tétel sorszámát (0 = mégse): ");
        var indexInput = Console.ReadLine()?.Trim();

        // ellenőrizzük, hogy a megadott index érvényes szám-e és nem 0, mert az a visszalépés jelzése
        if (!int.TryParse(indexInput, out int selectedIndex) || selectedIndex == 0)
        {
            Console.WriteLine("Licitálás megszakítva.");
            return;
        }

        if (selectedIndex < 1 || selectedIndex > auctions.Count)
        {
            Console.WriteLine("Érvénytelen sorszám.");
            return;
        }

        var selected = auctions[selectedIndex - 1];

        // Minimális licit összeg megjelenítése
        decimal minimum = selected.CurrentHighestBid.HasValue
            ? selected.CurrentHighestBid.Value + selected.MinIncrement
            : selected.StartPrice;

        Console.WriteLine($"\nKiválasztott tétel: {selected.Title}");
        Console.WriteLine($"Minimális licit: {minimum:N2} Ft");

        // Összeg bekérése, illetve egészeket formázzuk vesszőre, hogy a magyar formátumot is elfogadjuk
        Console.Write("Add meg a licit összegét (Ft): ");
        var amountInput = Console.ReadLine()?.Trim().Replace(',', '.');

        if (!decimal.TryParse(amountInput,
                System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture,
                out decimal amount))
        {
            Console.WriteLine("Érvénytelen összeg.");
            return;
        }

        // elősször validálunk, majd megpróbáljuk elmenteni a licitet, ha a validáció nem sikerül akkor a repository dob egy InvalidOperationExceptiont amiben benne van a hiba oka
        try
        {
            int bidId = await auctionRepo.PlaceBidAsync(
                auctionItemId: selected.AuctionItemId,
                bidderUserId: session.UserId,
                amount: amount);

            Console.WriteLine($"\n Licit sikeresen rögzítve! (bid_id = {bidId}, összeg = {amount:N2} Ft)");
        }
        catch (InvalidOperationException ex)
        {
            Console.WriteLine($"\n Sikertelen licit: {ex.Message}");
        }
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..(max - 1)] + "…";
}