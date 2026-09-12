using BoricuaBite.Domain.Entities;

namespace BoricuaBite.Domain.Tests;

public sealed class LoginChallengeTests
{
    [Fact]
    public void CorrectCode_ConsumesChallenge()
    {
        var now = DateTime.UtcNow;
        var challenge = new LoginChallenge(Guid.NewGuid(), "123456", now.AddMinutes(10));

        Assert.True(challenge.TryVerify("123456", now.AddMinutes(1)));
        Assert.NotNull(challenge.ConsumedAtUtc);
        Assert.False(challenge.TryVerify("123456", now.AddMinutes(2)));
    }

    [Fact]
    public void ExpiredCode_IsRejected()
    {
        var now = DateTime.UtcNow;
        var challenge = new LoginChallenge(Guid.NewGuid(), "123456", now.AddMinutes(1));

        Assert.False(challenge.TryVerify("123456", now.AddMinutes(2)));
    }

    [Fact]
    public void FiveWrongAttempts_LockChallenge()
    {
        var now = DateTime.UtcNow;
        var challenge = new LoginChallenge(Guid.NewGuid(), "123456", now.AddMinutes(10));

        for (var i = 0; i < 5; i++) Assert.False(challenge.TryVerify("999999", now.AddSeconds(i + 1)));

        Assert.False(challenge.IsUsable(now.AddMinutes(1)));
        Assert.False(challenge.TryVerify("123456", now.AddMinutes(1)));
    }
}
