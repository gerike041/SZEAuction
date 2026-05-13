using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SZEAuction.App
{

    public static class ListMyAuctionsAction
    {
        public static async Task ExecuteAsync(Session session, AuctionRepository auctionRepo)
        {
            Console.WriteLine("\n--- Saját hirdetéseim ---");

            // Alapértelmezetten az utolsó 10-et kéri le a repoból
            var myAuctions = await auctionRepo.ListMyAuctionsAsync(session.UserId);

            if (myAuctions.Count == 0)
            {
                Console.WriteLine("Még nem adtál fel hirdetést.");
                return;
            }

            Console.WriteLine($"{"#",-4} {"Cím",-25} {"Kezdő ár",10} {"Leg. licit",10}  {"Lezárás",-16} {"Státusz"}");
            Console.WriteLine(new string('-', 85));

            for (int i = 0; i < myAuctions.Count; i++)
            {
                var a = myAuctions[i];
                var highestStr = a.CurrentHighestBid.HasValue
                    ? $"{a.CurrentHighestBid.Value,10:N2}"
                    : $"{"nincs",10}";

                // Státusz és szín meghatározása
                string status;
                ConsoleColor statusColor;

                if (a.CloseTime <= DateTimeOffset.UtcNow)
                {
                    // Ha lejárt, megnézzük, hogy érkezett-e rá licit
                    if (a.CurrentHighestBid.HasValue)
                    {
                        status = "[Elkelt]";
                        statusColor = ConsoleColor.Yellow;
                    }
                    else
                    {
                        status = "[Licit nélkül]";
                        statusColor = ConsoleColor.Red;
                    }
                }
                else
                {
                    // Ha még nem járt le
                    status = "[Aktív]";
                    statusColor = ConsoleColor.Green;
                }

                // Kiírjuk a sor elejét normál színnel (Console.Write, hogy ne rakjon új sort)
                Console.Write(
                    $"{i + 1,-4} {Truncate(a.Title, 25),-25} {a.StartPrice,10:N2} {highestStr}  {a.CloseTime.ToLocalTime():yyyy-MM-dd HH:mm} ");

                // Színt váltunk, kiírjuk a státuszt, majd visszaállítjuk az eredeti színt
                Console.ForegroundColor = statusColor;
                Console.WriteLine(status);
                Console.ResetColor();
            }
        }

        private static string Truncate(string s, int max) =>
            s.Length <= max ? s : s[..(max - 1)] + "…";
    }
}
