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

                // Megnézzük, hogy az idő alapján lejárt-e
                string status = a.CloseTime <= DateTimeOffset.UtcNow ? "[Lejárt]" : "[Aktív]";

                Console.WriteLine(
                    $"{i + 1,-4} {Truncate(a.Title, 25),-25} {a.StartPrice,10:N2} {highestStr}  {a.CloseTime.ToLocalTime():yyyy-MM-dd HH:mm} {status}");
            }
        }

        private static string Truncate(string s, int max) =>
            s.Length <= max ? s : s[..(max - 1)] + "…";
    }
}
