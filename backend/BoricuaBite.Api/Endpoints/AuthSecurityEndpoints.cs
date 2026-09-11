using System.Security.Claims;
using System.Security.Cryptography;
using BoricuaBite.Application.Authentication;
using BoricuaBite.Application.Notifications;
using BoricuaBite.Domain.Entities;
using BoricuaBite.Infrastructure.Identity;
using BoricuaBite.Infrastructure.Persistence;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace BoricuaBite.Api.Endpoints;

public static class AuthSecurityEndpoints
{
    public static IEndpointRouteBuilder MapAuthSecurityEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/security").RequireRateLimiting("authentication");

        group.MapPost("/password/start", async (PasswordLoginStart request, UserManager<ApplicationUser> users,
            BoricuaBiteDbContext db, ITransactionalEmailSender email, IHostEnvironment environment, CancellationToken ct) =>
        {
            var user = await users.FindByEmailAsync(request.Email.Trim());
            if (user is null || !await users.CheckPasswordAsync(user, request.Password))
                return Results.Unauthorized();

            var now = DateTime.UtcNow;
            var existing = await db.LoginChallenges.Where(x => x.UserId == user.Id && x.ConsumedAtUtc == null && x.ExpiresAtUtc > now).ToListAsync(ct);
            foreach (var challenge in existing) challenge.Consume(now);

            var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
            var loginChallenge = new LoginChallenge(user.Id, code, now.AddMinutes(10));
            db.LoginChallenges.Add(loginChallenge);
            await db.SaveChangesAsync(ct);

            var mayExposeDevelopmentCode = environment.IsDevelopment() || environment.IsEnvironment("Testing");
            if (email.IsConfigured)
            {
                await email.SendAsync(user.Email!, "Your BoricuaBite sign-in code",
                    $"Your BoricuaBite verification code is {code}. It expires in 10 minutes.",
                    $"<h2>BoricuaBite sign-in</h2><p>Your verification code is:</p><p style=\"font-size:28px;font-weight:700;letter-spacing:6px\">{code}</p><p>This code expires in 10 minutes. If you did not try to sign in, you can ignore this email.</p>", ct);
            }
            else if (!mayExposeDevelopmentCode)
            {
                loginChallenge.Consume(DateTime.UtcNow);
                await db.SaveChangesAsync(ct);
                return Results.Problem("Email 2FA is not configured.", statusCode: StatusCodes.Status503ServiceUnavailable);
            }

            return Results.Ok(new
            {
                challengeId = loginChallenge.Id,
                expiresInSeconds = 600,
                destination = MaskEmail(user.Email!),
                developmentCode = !email.IsConfigured && mayExposeDevelopmentCode ? code : null
            });
        });

        group.MapPost("/password/verify", async (PasswordLoginVerify request, BoricuaBiteDbContext db,
            UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signInManager, CancellationToken ct) =>
        {
            var challenge = await db.LoginChallenges.SingleOrDefaultAsync(x => x.Id == request.ChallengeId, ct);
            if (challenge is null || !challenge.TryVerify(request.Code, DateTime.UtcNow))
            {
                if (challenge is not null) await db.SaveChangesAsync(ct);
                return Results.Unauthorized();
            }

            await db.SaveChangesAsync(ct);
            var user = await users.FindByIdAsync(challenge.UserId.ToString());
            if (user is null) return Results.Unauthorized();
            var principal = await signInManager.CreateUserPrincipalAsync(user);
            return Results.SignIn(principal, authenticationScheme: IdentityConstants.BearerScheme);
        });

        group.MapPost("/google", async (GoogleCredentialRequest request, IGoogleIdentityValidator google,
            UserManager<ApplicationUser> users, SignInManager<ApplicationUser> signInManager) =>
        {
            if (!google.IsConfigured) return Results.Problem("Google sign-in is not configured.", statusCode: StatusCodes.Status503ServiceUnavailable);

            GoogleIdentityProfile profile;
            try { profile = await google.ValidateAsync(request.Credential); }
            catch { return Results.Unauthorized(); }

            var login = new UserLoginInfo("Google", profile.Subject, "Google");
            var user = await users.FindByLoginAsync(login.LoginProvider, login.ProviderKey);
            if (user is null)
            {
                var sameEmail = await users.FindByEmailAsync(profile.Email);
                if (sameEmail is not null)
                    return Results.Conflict(new { error = "An account with this email already exists. Sign in with your password, then link Google from Security settings." });

                user = new ApplicationUser
                {
                    UserName = profile.Email,
                    Email = profile.Email,
                    EmailConfirmed = profile.EmailVerified
                };
                var created = await users.CreateAsync(user);
                if (!created.Succeeded) return Results.ValidationProblem(created.Errors.ToDictionary(x => x.Code, x => new[] { x.Description }));
                var linked = await users.AddLoginAsync(user, login);
                if (!linked.Succeeded) return Results.ValidationProblem(linked.Errors.ToDictionary(x => x.Code, x => new[] { x.Description }));
            }

            var principal = await signInManager.CreateUserPrincipalAsync(user);
            return Results.SignIn(principal, authenticationScheme: IdentityConstants.BearerScheme);
        });

        group.MapPost("/google/link", async (GoogleCredentialRequest request, ClaimsPrincipal principal,
            IGoogleIdentityValidator google, UserManager<ApplicationUser> users) =>
        {
            var user = await users.GetUserAsync(principal);
            if (user is null) return Results.Unauthorized();

            GoogleIdentityProfile profile;
            try { profile = await google.ValidateAsync(request.Credential); }
            catch { return Results.Unauthorized(); }
            if (!string.Equals(profile.Email, user.Email, StringComparison.OrdinalIgnoreCase))
                return Results.BadRequest(new { error = "The Google account email must match your BoricuaBite email." });

            var login = new UserLoginInfo("Google", profile.Subject, "Google");
            var owner = await users.FindByLoginAsync(login.LoginProvider, login.ProviderKey);
            if (owner is not null && owner.Id != user.Id) return Results.Conflict(new { error = "That Google account is already linked." });
            if (owner is null)
            {
                var result = await users.AddLoginAsync(user, login);
                if (!result.Succeeded) return Results.ValidationProblem(result.Errors.ToDictionary(x => x.Code, x => new[] { x.Description }));
            }
            return Results.Ok(new { googleLinked = true });
        }).RequireAuthorization("ApiBearer");

        group.MapGet("/status", async (ClaimsPrincipal principal, UserManager<ApplicationUser> users,
            ITransactionalEmailSender email, IGoogleIdentityValidator google) =>
        {
            var user = await users.GetUserAsync(principal);
            if (user is null) return Results.Unauthorized();
            var logins = await users.GetLoginsAsync(user);
            return Results.Ok(new
            {
                email2FaAvailable = email.IsConfigured,
                googleAvailable = google.IsConfigured,
                googleLinked = logins.Any(x => x.LoginProvider == "Google")
            });
        }).RequireAuthorization("ApiBearer");

        return endpoints;
    }

    private static string MaskEmail(string email)
    {
        var parts = email.Split('@');
        if (parts.Length != 2 || parts[0].Length < 2) return "your email";
        return $"{parts[0][0]}***{parts[0][^1]}@{parts[1]}";
    }

    public sealed record PasswordLoginStart(string Email, string Password);
    public sealed record PasswordLoginVerify(Guid ChallengeId, string Code);
    public sealed record GoogleCredentialRequest(string Credential);
}
