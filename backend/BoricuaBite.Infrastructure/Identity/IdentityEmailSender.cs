using System.Net;
using BoricuaBite.Application.Notifications;
using Microsoft.AspNetCore.Identity;

namespace BoricuaBite.Infrastructure.Identity;

public sealed class IdentityEmailSender(ITransactionalEmailSender emailSender) : IEmailSender<ApplicationUser>
{
    public Task SendConfirmationLinkAsync(ApplicationUser user, string email, string confirmationLink) =>
        SendIfConfigured(email, "Confirm your BoricuaBite email",
            $"Confirm your BoricuaBite email by opening this link: {confirmationLink}",
            $"<h2>Confirm your BoricuaBite email</h2><p><a href=\"{WebUtility.HtmlEncode(confirmationLink)}\">Confirm email</a></p>");

    public Task SendPasswordResetLinkAsync(ApplicationUser user, string email, string resetLink) =>
        SendIfConfigured(email, "Reset your BoricuaBite password",
            $"Reset your BoricuaBite password by opening this link: {resetLink}",
            $"<h2>Reset your BoricuaBite password</h2><p><a href=\"{WebUtility.HtmlEncode(resetLink)}\">Reset password</a></p>");

    public Task SendPasswordResetCodeAsync(ApplicationUser user, string email, string resetCode) =>
        SendIfConfigured(email, "Your BoricuaBite password reset code",
            $"Your BoricuaBite password reset code is {resetCode}.",
            $"<h2>BoricuaBite password reset</h2><p>Your reset code is:</p><p style=\"font-size:28px;font-weight:700;letter-spacing:4px\">{WebUtility.HtmlEncode(resetCode)}</p>");

    private Task SendIfConfigured(string recipient, string subject, string plainText, string html) =>
        emailSender.IsConfigured
            ? emailSender.SendAsync(recipient, subject, plainText, html)
            : Task.CompletedTask;
}
