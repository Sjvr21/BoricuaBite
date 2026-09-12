using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace BoricuaBite.Api.Tests;

public sealed class AuthSecurityTests
{
    private const string Password = "SecureLogin!12345";

    [Fact]
    public async Task PasswordLogin_RequiresSecondFactorBeforeBearerToken()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        const string email = "twofactor@example.com";

        Assert.Equal(HttpStatusCode.OK,
            (await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password })).StatusCode);

        var start = await client.PostAsJsonAsync("/api/security/password/start", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, start.StatusCode);
        var challenge = await start.Content.ReadFromJsonAsync<JsonElement>();
        var challengeId = challenge.GetProperty("challengeId").GetGuid();
        var developmentCode = challenge.GetProperty("developmentCode").GetString();
        Assert.False(string.IsNullOrWhiteSpace(developmentCode));

        var verify = await client.PostAsJsonAsync("/api/security/password/verify", new { challengeId, code = developmentCode });
        Assert.Equal(HttpStatusCode.OK, verify.StatusCode);
        var tokens = await verify.Content.ReadFromJsonAsync<JsonElement>();
        var accessToken = tokens.GetProperty("accessToken").GetString();
        Assert.False(string.IsNullOrWhiteSpace(accessToken));

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/account")).StatusCode);
    }

    [Fact]
    public async Task PasswordLogin_RejectsInvalidSecondFactor()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        const string email = "wrongcode@example.com";

        await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password });
        var start = await client.PostAsJsonAsync("/api/security/password/start", new { email, password = Password });
        var challenge = await start.Content.ReadFromJsonAsync<JsonElement>();

        var verify = await client.PostAsJsonAsync("/api/security/password/verify", new
        {
            challengeId = challenge.GetProperty("challengeId").GetGuid(),
            code = "999999"
        });

        Assert.Equal(HttpStatusCode.Unauthorized, verify.StatusCode);
    }

    [Fact]
    public async Task NewChallenge_ConsumesPreviousChallenge()
    {
        using var factory = new ApiFactory();
        using var client = factory.CreateApiClient();
        const string email = "replacecode@example.com";

        await client.PostAsJsonAsync("/api/auth/register", new { email, password = Password });
        var first = await client.PostAsJsonAsync("/api/security/password/start", new { email, password = Password });
        var firstChallenge = await first.Content.ReadFromJsonAsync<JsonElement>();

        var second = await client.PostAsJsonAsync("/api/security/password/start", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);

        var oldVerify = await client.PostAsJsonAsync("/api/security/password/verify", new
        {
            challengeId = firstChallenge.GetProperty("challengeId").GetGuid(),
            code = firstChallenge.GetProperty("developmentCode").GetString()
        });

        Assert.Equal(HttpStatusCode.Unauthorized, oldVerify.StatusCode);
    }
}
