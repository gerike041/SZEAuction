namespace SZEAuction.App;

public sealed record AuctionItem(
    int AuctionItemId,
    int SellerUserId,
    string Title,
    string? Description,
    DateTimeOffset CloseTime,
    decimal StartPrice,
    decimal MinIncrement,
    decimal? CurrentHighestBid   // null = még senki sem licitált
);