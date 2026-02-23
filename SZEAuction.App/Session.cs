namespace SZEAuction.App;

public enum Role { elado, vevo }

public sealed record Session(int UserId, string Username, Role Role);