using System.Net;
using Microsoft.EntityFrameworkCore;
using SmartOrderSystem.Data;

namespace SmartOrderSystem.Services
{
    public record PromoSendResult(bool Success, string Message);

    public class PromotionMailer
    {
        private const int DailyLimit = 300;

        private readonly ApplicationDbContext _context;
        private readonly IPromoEmailSender _sender;
        private readonly IConfiguration _config;
        private readonly ILogger<PromotionMailer> _logger;

        public PromotionMailer(ApplicationDbContext context, IPromoEmailSender sender,
            IConfiguration config, ILogger<PromotionMailer> logger)
        {
            _context = context;
            _sender = sender;
            _config = config;
            _logger = logger;
        }

        // Dapat kapareho ng filter sa SendAsync. Kapag binago mo ang isa, baguhin din ang isa.
        public Task<int> CountRecipientsAsync(CancellationToken ct = default) =>
            _context.Customers
                .AsNoTracking()
                .CountAsync(c => c.receive_promotions && c.email != "", ct);

        public async Task<PromoSendResult> SendAsync(int promotionId, CancellationToken ct = default)
        {
            var promo = await _context.ProductPromotions
                .FirstOrDefaultAsync(p => p.promotion_id == promotionId, ct);

            if (promo == null)
                return new PromoSendResult(false, "Promotion not found.");

            var today = DateTime.Today;

            if (promo.LastSentAt.HasValue && promo.LastSentAt.Value.Date == today)
                return new PromoSendResult(false, "This promotion email was already sent today.");

            if (promo.status != "Active" || today < promo.start_date.Date || today > promo.end_date.Date)
                return new PromoSendResult(false, "This promotion is not active (status or dates), so no emails were sent.");

            var recipients = await _context.Customers
                .AsNoTracking()
                .Where(c => c.receive_promotions && c.email != "")
                .Select(c => new { c.full_name, c.email })
                .ToListAsync(ct);

            if (recipients.Count == 0)
                return new PromoSendResult(false, "No customers have opted in to receive promotions.");

            if (recipients.Count > DailyLimit)
                return new PromoSendResult(false,
                    $"{recipients.Count} customers opted in, but the free Brevo plan allows {DailyLimit} emails per day. Nothing was sent.");

            var shoe = await _context.ShoeCatalogs
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.shoe_id == promo.shoe_id, ct);
            var productName = WebUtility.HtmlEncode(
                shoe == null ? "selected shoes" : $"{shoe.brand} {shoe.model_name}");

            var configuredBaseUrl = _config["App:BaseUrl"];
            if (!Uri.TryCreate(configuredBaseUrl, UriKind.Absolute, out var baseUri) ||
                !string.Equals(baseUri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
            {
                return new PromoSendResult(false, "The HTTPS application base URL is not configured.");
            }

            var baseUrl = baseUri.ToString().TrimEnd('/');
            var manageUrl = $"{baseUrl}/Account/EditProfile";
            var campaign = WebUtility.HtmlEncode(promo.campaign_name);
            var discount = promo.discount_percentage.ToString("0.##");
            var until = promo.end_date.ToString("MMMM d, yyyy");
            var subject = $"{promo.campaign_name}: {discount}% off at CJ Shoes";

            var emails = recipients
                .Select(r => new PromoEmail(
                    r.email,
                    r.full_name,
                    subject,
                    BuildBody(WebUtility.HtmlEncode(r.full_name), campaign, productName, discount, until, manageUrl)))
                .ToList();

            int sent;
            int failed;
            try
            {
                (sent, failed) = await _sender.SendBulkAsync(emails, ct);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Promotion {PromotionId} email send failed", promotionId);
                return new PromoSendResult(false, "Could not connect to the email service. Check the Brevo settings.");
            }

            if (sent > 0)
            {
                promo.LastSentAt = DateTime.Now;
                await _context.SaveChangesAsync(ct);
            }

            return new PromoSendResult(sent > 0, $"Promotion emails sent: {sent}. Failed: {failed}.");
        }

        private static string BuildBody(string name, string campaign, string product, string discount, string until, string manageUrl) =>
$@"<div style=""font-family:Arial,sans-serif;max-width:520px;margin:auto;color:#1e293b"">
  <h2 style=""margin-bottom:4px"">{campaign}</h2>
  <p>Hi {name},</p>
  <p>CJ Shoes is running a promotion with <strong>{discount}% off</strong> on <strong>{product}</strong>, valid until {until}. Visit our shop to get the discounted price.</p>
  <hr style=""border:none;border-top:1px solid #e2e8f0;margin:24px 0"">
  <p style=""font-size:12px;color:#64748b"">You are receiving this because you agreed to receive promotions from CJ Shoes. You can turn this off anytime in <a href=""{manageUrl}"">your account settings</a>.</p>
</div>";
    }
}
