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

        RunRoleFlow(session);
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

    static void RunRoleFlow(Session session)
    {
        if (session.Role == Role.elado)
            RunSellerFlow(session);
        else
            RunBuyerFlow(session);
    }

    static void RunSellerFlow(Session session)
    {
        Console.WriteLine($"\n--- Eladói Menü ({session.Username}) ---");
    }

    static void RunBuyerFlow(Session session)
    {
        Console.WriteLine($"\n--- Vevői Menü ({session.Username}) ---");
    }
}