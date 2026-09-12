using System.Net.Http.Headers;
using System.Net.Http.Json;
using BoricuaBite.Application.Notifications;
using Microsoft.Extensions.Options;

namespace BoricuaBite.Infrastructure.Notifications;

public sealed class SendGridEmailSender(HttpClient httpClient, IOptions<EmailDeliveryOptions> optionsAccessor) : ITransactionalEmailSender
{
    private readonly EmailDeliveryOptions options = optionsAccessor.Value;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.ApiKey) && !string.IsNullOrWhiteSpace(options.FromEmail);

    public async Task SendAsync(string toEmail, string subject, string plainText, string html, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured) throw new InvalidOperationException("Transactional email is not configured.");
        if (string.IsNullOrWhiteSpace(toEmail)) throw new ArgumentException("Recipient email is required.", nameof(toEmail));

        using var request = new HttpRequestMessage(HttpMethod.Post, "v3/mail/send");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        request.Content = JsonContent.Create(new
        {
            personalizations = new[] { new { to = new[] { new { email = toEmail } } } },
            from = new { email = options.FromEmail, name = options.FromName },
            subject,
            content = new[]
            {
                new { type = "text/plain", value = plainText },
                new { type = "text/html", value = html }
            }
        });

        var response = await httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            throw new InvalidOperationException($"SendGrid rejected the message ({(int)response.StatusCode}): {body}");
        }
    }
}
