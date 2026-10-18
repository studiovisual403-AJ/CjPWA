using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Options;
using MimeKit;

namespace SmartOrderSystem.Services
{
    public class BrevoSettings
    {
        public string Host { get; set; } = "smtp-relay.brevo.com";
        public int Port { get; set; } = 587;
        public string Login { get; set; } = string.Empty;
        public string SmtpKey { get; set; } = string.Empty;
        public string SenderEmail { get; set; } = string.Empty;
        public string SenderName { get; set; } = "CJ Shoes";
    }

    public record PromoEmail(string ToEmail, string ToName, string Subject, string HtmlBody);

    public interface IPromoEmailSender
    {
        Task<(int Sent, int Failed)> SendBulkAsync(IEnumerable<PromoEmail> emails, CancellationToken ct = default);
    }

    public class BrevoEmailService : IPromoEmailSender
    {
        private readonly BrevoSettings _settings;
        private readonly ILogger<BrevoEmailService> _logger;

        public BrevoEmailService(IOptions<BrevoSettings> settings, ILogger<BrevoEmailService> logger)
        {
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<(int Sent, int Failed)> SendBulkAsync(IEnumerable<PromoEmail> emails, CancellationToken ct = default)
        {
            int sent = 0;
            int failed = 0;

            using var client = new SmtpClient();
            await client.ConnectAsync(_settings.Host, _settings.Port, SecureSocketOptions.StartTls, ct);
            await client.AuthenticateAsync(_settings.Login, _settings.SmtpKey, ct);

            foreach (var mail in emails)
            {
                try
                {
                    var message = new MimeMessage();
                    message.From.Add(new MailboxAddress(_settings.SenderName, _settings.SenderEmail));
                    message.To.Add(new MailboxAddress(mail.ToName, mail.ToEmail));
                    message.Subject = mail.Subject;
                    message.Body = new BodyBuilder { HtmlBody = mail.HtmlBody }.ToMessageBody();

                    await client.SendAsync(message, ct);
                    sent++;
                }
                catch (Exception ex)
                {
                    failed++;
                    _logger.LogWarning(ex, "Promo email to {Email} failed", mail.ToEmail);
                }
            }

            await client.DisconnectAsync(true, ct);
            return (sent, failed);
        }
    }
}
