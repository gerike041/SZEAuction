using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SZEAuction.App
{

    public static class CreateAuctionAction
    {
        public static async Task ExecuteAsync(Session session, AuctionRepository repo)
        {
            Console.WriteLine("\n--- Új aukció indítása ---");

            Console.Write("Tétel címe: ");
            var title = Console.ReadLine()?.Trim();
            if (string.IsNullOrWhiteSpace(title))
            {
                Console.WriteLine("A cím nem lehet üres!");
                return;
            }

            Console.Write("Leírás (opcionális, enterrel átugorható): ");
            var description = Console.ReadLine()?.Trim();

            Console.Write("Kezdő ár (Ft): ");
            if (!decimal.TryParse(Console.ReadLine()?.Replace(',', '.'), out decimal startPrice) || startPrice < 0)
            {
                Console.WriteLine("Érvénytelen kezdő ár.");
                return;
            }

            Console.Write("Minimum licitlépcső (Ft): ");
            if (!decimal.TryParse(Console.ReadLine()?.Replace(',', '.'), out decimal minIncrement) || minIncrement <= 0)
            {
                Console.WriteLine("Érvénytelen licitlépcső.");
                return;
            }

            Console.Write("Lejárat ideje (perc múlva záruljon le, pl. 60): ");
            if (!int.TryParse(Console.ReadLine(), out int minutes) || minutes <= 0)
            {
                Console.WriteLine("Érvénytelen időtartam.");
                return;
            }

            var closeTime = DateTimeOffset.UtcNow.AddMinutes(minutes);

            try
            {
                int newId = await repo.CreateAuctionAsync(session.UserId, title, description, closeTime, startPrice, minIncrement);
                Console.WriteLine($"\n> Aukció sikeresen létrehozva! (Azonosító: {newId}, Lejárat: {closeTime.ToLocalTime():yyyy-MM-dd HH:mm})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n> Hiba az aukció létrehozásakor: {ex.Message}");
            }
        }
    }
}
