using Microsoft.Extensions.Configuration;
using System.Security.Cryptography;

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

        _ = Task.Run(async () =>
        {
            await using var backgroundConn = await db.GetOpenConnectionAsync();

            var closingService = new AuctionClosingService(backgroundConn);
            var notificationSender = new NotificationSenderService(backgroundConn, config);

            using var timer = new PeriodicTimer(TimeSpan.FromMinutes(2));

            await RunBackgroundTasksAsync(closingService, notificationSender);

            while (await timer.WaitForNextTickAsync())
            {
                await RunBackgroundTasksAsync(closingService, notificationSender);
            }
        });

        // Bejelentkezés
        var userRepo = new UserRepository(conn);

        DbUser? dbUser = null;

        while (true)
        {
            Console.Write("Email cím: ");
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

        await RunRoleFlow(dbUser, conn);
    }

    static Role ChooseRole()
    {
        while (true)
        {
            Console.Clear();

            Console.WriteLine("================================");
            Console.WriteLine("        SZEAuction Login        ");
            Console.WriteLine("================================");
            Console.WriteLine();
            Console.WriteLine("Válassz szerepkört:");
            Console.WriteLine();
            Console.WriteLine("[1] Eladó");
            Console.WriteLine("[2] Vevő");
            Console.WriteLine();
            Console.Write("Választás: ");

            var input = Console.ReadLine();

            switch (input)
            {
                case "1":
                    Console.Clear();
                    return Role.elado;

                case "2":
                    Console.Clear();
                    return Role.vevo;

                default:
                    Console.WriteLine();
                    Console.WriteLine("Érvénytelen választás.");
                    Console.WriteLine("Nyomj meg egy gombot az újrapróbáláshoz...");
                    Console.ReadKey();
                    break;
            }
        }
    }

    static async Task RunRoleFlow(DbUser dbUser, Npgsql.NpgsqlConnection conn)
    {
        while (true)
        {
            var role = ChooseRole();
            var session = new Session(dbUser.Id, dbUser.Username, role);

            FlowResult result;

            if (session.Role == Role.elado)
                result = await RunSellerFlowAsync(session, conn);
            else
                result = await RunBuyerFlowAsync(session, conn);

            if (result == FlowResult.Exit)
                return;


        }
    }

    static async Task<FlowResult> RunSellerFlowAsync(Session session, Npgsql.NpgsqlConnection conn)
    {
        var auctionRepo = new AuctionRepository(conn);

        while (true)
        {
            Console.Clear();

            Console.WriteLine($"=== Eladói Menü ({session.Username}) ===");
            Console.WriteLine("1 - Új aukció indítása");
            Console.WriteLine("2 - Saját hirdetéseim");
            Console.WriteLine("3 - Vissza szerepkörválasztáshoz");
            Console.WriteLine("0 - Kilépés");
            Console.Write("Választás: ");

            var choice = Console.ReadLine()?.Trim();

            switch (choice)
            {
                case "1":
                    Console.Clear();
                    await CreateAuctionAction.ExecuteAsync(session, auctionRepo);
                    Pause();
                    break;

                case "2":
                    Console.Clear();
                    await ListMyAuctionsAction.ExecuteAsync(session, auctionRepo);
                    Pause();
                    break;

                case "3":
                    return FlowResult.BackToRoleSelection;

                case "0":
                    Console.Clear();
                    Console.WriteLine("Viszlát!");
                    return FlowResult.Exit;

                default:
                    Console.WriteLine();
                    Console.WriteLine("Érvénytelen választás, próbáld újra.");
                    Pause();
                    break;
            }
        }
    }

    static async Task<FlowResult> RunBuyerFlowAsync(Session session, Npgsql.NpgsqlConnection conn)
    {
        var auctionRepo = new AuctionRepository(conn);

        while (true)
        {
            Console.Clear();

            Console.WriteLine($"=== Vevői Menü ({session.Username}) ===");
            Console.WriteLine("1 - Aktív aukciók listázása");
            Console.WriteLine("2 - Licitálás indítása");
            Console.WriteLine("3 - Vissza szerepkörválasztáshoz");
            Console.WriteLine("0 - Kilépés");
            Console.Write("Választás: ");

            var choice = Console.ReadLine()?.Trim();

            switch (choice)
            {
                case "1":
                    Console.Clear();
                    await ListActiveAuctionsAction.ExecuteAsync(auctionRepo);
                    Pause();
                    break;

                case "2":
                    Console.Clear();
                    await StartBiddingAction.ExecuteAsync(session, auctionRepo);
                    Pause();
                    break;

                case "3":
                    return FlowResult.BackToRoleSelection;

                case "0":
                    Console.Clear();
                    Console.WriteLine("Viszlát!");
                    return FlowResult.Exit;

                default:
                    Console.WriteLine();
                    Console.WriteLine("Érvénytelen választás, próbáld újra.");
                    Pause();
                    break;
            }
        }
    }
    private static async Task RunBackgroundTasksAsync(
    AuctionClosingService closingService,
    NotificationSenderService notificationSender)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("Closing expired auctions...");
            await closingService.CloseAllExpiredAuctionsAsync();

            System.Diagnostics.Debug.WriteLine("Sending out Gmails...");
            await notificationSender.SendPendingNotificationsAsync();

            System.Diagnostics.Debug.WriteLine("Background tasks finished.");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Background task error: {ex.Message}");
        }
    }
    enum FlowResult
    {
        BackToRoleSelection,
        Exit
    }
    static void Pause()
    {
        Console.WriteLine();
        Console.WriteLine("Nyomj meg egy gombot a folytatáshoz...");
        Console.ReadKey(true);
    }
}