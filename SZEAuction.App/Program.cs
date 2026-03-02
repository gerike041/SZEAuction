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
                    await ListActiveAuctionsAction.ExecuteAsync(auctionRepo);
                    break;

                case "2":
                    await StartBiddingAction.ExecuteAsync(session, auctionRepo);
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
}