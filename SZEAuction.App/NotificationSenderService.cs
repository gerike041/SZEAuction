using MailKit.Net.Smtp;
using MimeKit;
using Npgsql;
using Microsoft.Extensions.Configuration;

namespace SZEAuction.App;

public sealed class NotificationSenderService
{
    private readonly NpgsqlConnection _connection;
    private readonly string _senderEmail;
    private readonly string _senderPassword;

    public NotificationSenderService(NpgsqlConnection connection, IConfiguration configuration)
    {
        _connection = connection;
        _senderEmail = configuration["Email:Address"]
            ?? throw new Exception("Email:Address missing from configuration.");
        _senderPassword = configuration["Email:Password"]
            ?? throw new Exception("Email:Password missing from configuration.");
    }

    public async Task SendPendingNotificationsAsync()
    {
        const string sql = """
            SELECT n.notification_id,
                   n.user_id,
                   n.subject,
                   n.body,
                   u.username
            FROM notifications n
            JOIN users u ON u.user_id = n.user_id
            WHERE n.status = 0
            ORDER BY n.created_at ASC
            """;

        var pending = new List<(int NotificationId, int UserId, string Subject, string Body, string Email)>();

        await using (var cmd = new NpgsqlCommand(sql, _connection))
        await using (var reader = await cmd.ExecuteReaderAsync())
        {
            while (await reader.ReadAsync())
            {
                pending.Add((
                    reader.GetInt32(0),
                    reader.GetInt32(1),
                    reader.GetString(2),
                    reader.GetString(3),
                    reader.GetString(4)
                ));
            }
        }

        foreach (var item in pending)
        {
            try
            {
                var message = new MimeMessage();
                message.From.Add(new MailboxAddress("SZEAuction", _senderEmail));
                message.To.Add(MailboxAddress.Parse(item.Email));
                message.Subject = item.Subject;
                message.Body = new TextPart("plain")
                {
                    Text = item.Body
                };

                using var smtp = new SmtpClient();
                await smtp.ConnectAsync("smtp.gmail.com", 587, MailKit.Security.SecureSocketOptions.StartTls);
                await smtp.AuthenticateAsync(_senderEmail, _senderPassword);
                await smtp.SendAsync(message);
                await smtp.DisconnectAsync(true);

                await MarkNotificationAsSentAsync(item.NotificationId);
            }
            catch (Exception ex)
            {
                await MarkNotificationAsFailedAsync(item.NotificationId, ex.Message);
            }
        }
    }

    private async Task MarkNotificationAsSentAsync(int notificationId)
    {
        const string sql = """
            UPDATE notifications
            SET status = 1,
                sent_at = NOW()
            WHERE notification_id = @notificationId
            """;

        await using var cmd = new NpgsqlCommand(sql, _connection);
        cmd.Parameters.AddWithValue("notificationId", notificationId);
        await cmd.ExecuteNonQueryAsync();
    }

    private async Task MarkNotificationAsFailedAsync(int notificationId, string error)
    {
        const string sql = """
            UPDATE notifications
            SET status = 2,
                last_error = @error,
                attempt_count = attempt_count + 1
            WHERE notification_id = @notificationId
            """;

        await using var cmd = new NpgsqlCommand(sql, _connection);
        cmd.Parameters.AddWithValue("notificationId", notificationId);
        cmd.Parameters.AddWithValue("error", error);
        await cmd.ExecuteNonQueryAsync();
    }
}