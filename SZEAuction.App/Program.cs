using Microsoft.Extensions.Configuration;

namespace SZEAuction.App;

public class Program
{
    public static async Task Main()
    {
        var config = new ConfigurationBuilder()
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: false)
            .Build();

        var connectionString = config.GetConnectionString("DefaultConnection");
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new Exception("DB configstring is null");

        var db = new DBconnection(connectionString);

        Console.WriteLine("Connecting ...");
        await using var conn = await db.GetOpenConnectionAsync();
        Console.WriteLine("Connected successfully!");

        // Bejelentkezés
        var userRepo = new UserRepository(conn);

        DbUser? dbUser = null;

        while (true)
        {
            Console.Write("Felhasználónév: ");
            var usernameInput = Console.ReadLine()?.Trim();

            Console.Write("Jelszó: ");
            var passwordInput = Console.ReadLine(); // ne Trim

            if (string.IsNullOrWhiteSpace(usernameInput) || string.IsNullOrEmpty(passwordInput))
            {
                Console.WriteLine("Hiányzó adatok. Próbáld újra.\n");
                continue;
            }

            dbUser = await userRepo.FindByUsernameAsync(usernameInput);

            if (dbUser is null)
            {
                Console.WriteLine("Hibás felhasználónév vagy jelszó. Próbáld újra.\n");
                continue;
            }

            if (passwordInput != dbUser.Password)
            {
                Console.WriteLine("Hibás felhasználónév vagy jelszó. Próbáld újra.\n");
                continue;
            }

     
            break;
        }



        Console.WriteLine("Sikeres bejelentkezés!");

        var role = ChooseRole();
        var session = new Session(dbUser.Id, dbUser.Username, role);

        await RunRoleFlow(session, conn);
    }

    static Role ChooseRole()
    {
        while (true)
        {
            Console.WriteLine("\nVálassz szerepkört:");
            Console.WriteLine("1 - Eladó");
            Console.WriteLine("2 - Vevő");
            Console.Write("Választás: ");

            var input = Console.ReadLine();

            if (input == "1") return Role.elado;
            if (input == "2") return Role.vevo;

            Console.WriteLine("Érvénytelen választás.");
        }
    }

    static async Task RunRoleFlow(Session session, Npgsql.NpgsqlConnection conn)
    {
        if (session.Role == Role.elado)
            RunSellerFlow(session);
        else
            await RunBuyerFlowAsync(session, conn);
    }

    static void RunSellerFlow(Session session)
    {
        Console.WriteLine($"\n--- Eladói Menü ({session.Username}) ---");
    }

    static async Task RunBuyerFlowAsync(Session session, Npgsql.NpgsqlConnection conn)
    {
        // Repository létrehozása ami addig fut amíg a user ki nem lép 0-val
        var auctionRepo = new AuctionRepository(conn);

        while (true)
        {
            Console.WriteLine($"\n=== Vevői Menü ({session.Username}) ===");
            Console.WriteLine("1 - Aktív aukciók listázása");
            Console.WriteLine("2 - Licitálás indítása");
            Console.WriteLine("0 - Kilépés");
            Console.Write("Választás: ");

            var choice = Console.ReadLine()?.Trim();

            switch (choice)
            {
                case "1":
                    await ListActiveAuctionsAsync(auctionRepo);
                    break;

                case "2":
                    await StartBiddingAsync(session, auctionRepo);
                    break;

                case "0":
                    Console.WriteLine("Viszlát!");
                    return;

                default:
                    Console.WriteLine("Érvénytelen választás, próbáld újra.");
                    break;
            }
        }
    }
    static async Task ListActiveAuctionsAsync(AuctionRepository auctionRepo)
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

    static async Task StartBiddingAsync(Session session, AuctionRepository auctionRepo)
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

    static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..(max - 1)] + "…";
}