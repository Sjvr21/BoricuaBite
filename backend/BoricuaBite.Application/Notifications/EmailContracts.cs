namespace BoricuaBite.Application.Notifications;

public interface ITransactionalEmailSender
{
    bool IsConfigured { get; }
    Task SendAsync(string toEmail, string subject, string plainText, string html, CancellationToken cancellationToken = default);
}

public sealed class EmailDeliveryOptions
{
    public const string SectionName = "Email";
    public string? ApiKey { get; set; }
    public string? FromEmail { get; set; }
    public string FromName { get; set; } = "BoricuaBite";
}
