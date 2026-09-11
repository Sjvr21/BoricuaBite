namespace BoricuaBite.Application.Authentication;

public sealed record GoogleIdentityProfile(string Subject, string Email, bool EmailVerified);

public interface IGoogleIdentityValidator
{
    bool IsConfigured { get; }
    Task<GoogleIdentityProfile> ValidateAsync(string credential, CancellationToken cancellationToken = default);
}

public sealed class GoogleIdentityOptions
{
    public const string SectionName = "GoogleAuth";
    public string? ClientId { get; set; }
}
