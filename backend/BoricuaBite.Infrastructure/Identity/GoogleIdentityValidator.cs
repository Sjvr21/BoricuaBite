using BoricuaBite.Application.Authentication;
using Google.Apis.Auth;
using Microsoft.Extensions.Options;

namespace BoricuaBite.Infrastructure.Identity;

public sealed class GoogleIdentityValidator(IOptions<GoogleIdentityOptions> optionsAccessor) : IGoogleIdentityValidator
{
    private readonly GoogleIdentityOptions options = optionsAccessor.Value;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(options.ClientId);

    public async Task<GoogleIdentityProfile> ValidateAsync(string credential, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured) throw new InvalidOperationException("Google sign-in is not configured.");
        if (string.IsNullOrWhiteSpace(credential)) throw new ArgumentException("Google credential is required.", nameof(credential));

        var settings = new GoogleJsonWebSignature.ValidationSettings
        {
            Audience = [options.ClientId!]
        };

        var payload = await GoogleJsonWebSignature.ValidateAsync(credential, settings);
        if (string.IsNullOrWhiteSpace(payload.Subject) || string.IsNullOrWhiteSpace(payload.Email) || payload.EmailVerified != true)
            throw new InvalidOperationException("Google account email is not verified.");

        return new GoogleIdentityProfile(payload.Subject, payload.Email, true);
    }
}
