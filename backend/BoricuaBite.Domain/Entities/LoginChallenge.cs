using System.Security.Cryptography;
using System.Text;
using BoricuaBite.Domain.Common;

namespace BoricuaBite.Domain.Entities;

public sealed class LoginChallenge : BaseEntity
{
    private LoginChallenge() { }

    public LoginChallenge(Guid userId, string code, DateTime expiresAtUtc)
    {
        if (userId == Guid.Empty) throw new ArgumentException("User is required.", nameof(userId));
        if (string.IsNullOrWhiteSpace(code)) throw new ArgumentException("Code is required.", nameof(code));
        if (expiresAtUtc <= DateTime.UtcNow) throw new ArgumentException("Expiry must be in the future.", nameof(expiresAtUtc));
        UserId = userId;
        CodeHash = Hash(code);
        ExpiresAtUtc = expiresAtUtc;
    }

    public Guid UserId { get; private set; }
    public string CodeHash { get; private set; } = string.Empty;
    public DateTime ExpiresAtUtc { get; private set; }
    public int Attempts { get; private set; }
    public DateTime? ConsumedAtUtc { get; private set; }

    public bool IsUsable(DateTime utcNow) => ConsumedAtUtc is null && utcNow < ExpiresAtUtc && Attempts < 5;

    public bool TryVerify(string code, DateTime utcNow)
    {
        if (!IsUsable(utcNow)) return false;
        Attempts++;
        UpdatedAtUtc = utcNow;
        var expected = Convert.FromHexString(CodeHash);
        var actual = Convert.FromHexString(Hash(code));
        var valid = CryptographicOperations.FixedTimeEquals(expected, actual);
        if (valid) ConsumedAtUtc = utcNow;
        return valid;
    }

    public void Consume(DateTime utcNow)
    {
        ConsumedAtUtc ??= utcNow;
        UpdatedAtUtc = utcNow;
    }

    private static string Hash(string value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value.Trim())));
}
