using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using YaalTrail.Api.Configuration;
using YaalTrail.Api.Controllers;
using YaalTrail.Api.Data;
using YaalTrail.Api.Dtos;
using YaalTrail.Api.Models;
using YaalTrail.Api.Services;

namespace YaalTrail.Tests;

// SRS 4.7: FR-50 to FR-56, using the in-memory database.
public class AdminAuthServiceTests
{
    private const string Email = "admin@yaaltrail.lk";
    private const string Password = "Correct-Password-1";

    // Hashing is deliberately slow (PBKDF2, 100,000 iterations), so hash once for all tests.
    private static readonly PasswordHasher Hasher = new();
    private static readonly string StoredHash = Hasher.Hash(Password);

    private readonly AppDbContext _db = TestData.NewDatabase();
    private readonly FakeClock _clock = FakeClock.MondayMorning();
    private readonly AdminSessionStore _sessions;
    private readonly AdminAuthService _service;
    private readonly AdminUser _admin;

    public AdminAuthServiceTests()
    {
        _sessions = new AdminSessionStore(_clock);
        var jwt = Options.Create(new JwtOptions { Key = "test-signing-key-that-is-at-least-32-bytes-long!" });
        _service = new AdminAuthService(_db, Hasher, _sessions, _clock, jwt, NullLogger<AdminAuthService>.Instance);

        _admin = new AdminUser { Email = Email, DisplayName = "Test Admin", PasswordHash = StoredHash };
        _db.AdminUsers.Add(_admin);
        _db.SaveChanges();
    }

    private Task<AdminLoginResponseDto?> Login(string password, string email = Email) =>
        _service.LoginAsync(new AdminLoginRequestDto(email, password));

    // ---------- success ----------

    [Fact]
    public async Task LoginAsync_CorrectPassword_ReturnsBearerToken()
    {
        AdminLoginResponseDto? result = await Login(Password);

        Assert.NotNull(result);
        Assert.Equal("Bearer", result.TokenType);
        Assert.Equal("Test Admin", result.DisplayName);
        Assert.False(string.IsNullOrWhiteSpace(result.AccessToken));
    }

    // FR-51: signed token valid for at most 60 minutes, with a jti and the admin's id.
    [Fact]
    public async Task LoginAsync_CorrectPassword_TokenExpiresWithin60MinutesAndHasJti()
    {
        AdminLoginResponseDto result = (await Login(Password))!;
        JsonWebToken token = new JsonWebTokenHandler().ReadJsonWebToken(result.AccessToken);

        Assert.Equal("HS256", token.Alg);
        Assert.False(string.IsNullOrEmpty(token.Id));
        Assert.Equal(_admin.Id.ToString(), token.Subject);
        Assert.Equal("Admin", token.GetClaim("role").Value);
        Assert.Equal(_clock.UtcNow.AddMinutes(60), result.ExpiresAt);
        Assert.True(token.ValidTo <= _clock.UtcNow.AddMinutes(60));
    }

    [Fact]
    public async Task LoginAsync_CorrectPassword_ResetsFailedAttemptsAndRecordsLastLogin()
    {
        _admin.FailedAttempts = 3;
        _db.SaveChanges();

        await Login(Password);

        Assert.Equal(0, _admin.FailedAttempts);
        Assert.Null(_admin.LockedUntil);
        Assert.Equal(_clock.UtcNow, _admin.LastLoginAt);
        Assert.Equal(DateTimeKind.Utc, _admin.LastLoginAt!.Value.Kind);
    }

    [Fact]
    public async Task LoginAsync_EmailInDifferentCaseWithSpaces_Succeeds()
    {
        AdminLoginResponseDto? result = await Login(Password, "  ADMIN@YaalTrail.LK ");

        Assert.NotNull(result);
    }

    // FR-55, FR-56: the token's session is active after sign-in and ends at sign-out.
    [Fact]
    public async Task Logout_AfterLogin_EndsTheSession()
    {
        AdminLoginResponseDto result = (await Login(Password))!;
        string jti = new JsonWebTokenHandler().ReadJsonWebToken(result.AccessToken).Id;
        Assert.True(_sessions.TryTouch(jti));

        _service.Logout(jti);

        Assert.False(_sessions.TryTouch(jti));
    }

    // ---------- failures and FR-54 lockout ----------

    [Fact]
    public async Task LoginAsync_WrongPassword_ReturnsNullAndCountsAttempt()
    {
        AdminLoginResponseDto? result = await Login("wrong");

        Assert.Null(result);
        Assert.Equal(1, _admin.FailedAttempts);
        Assert.Null(_admin.LockedUntil);
    }

    [Fact]
    public async Task LoginAsync_UnknownEmail_ReturnsNull()
    {
        AdminLoginResponseDto? result = await Login(Password, "nobody@example.com");

        Assert.Null(result);
        Assert.Equal(0, _admin.FailedAttempts);
    }

    [Fact]
    public async Task LoginAsync_FourWrongPasswords_DoesNotLock()
    {
        for (int i = 0; i < 4; i++) await Login("wrong");

        Assert.Equal(4, _admin.FailedAttempts);
        Assert.Null(_admin.LockedUntil);
        Assert.NotNull(await Login(Password));
    }

    [Fact]
    public async Task LoginAsync_FiveWrongPasswords_LocksAccountFor15Minutes()
    {
        for (int i = 0; i < 5; i++) await Login("wrong");

        Assert.Equal(_clock.UtcNow.AddMinutes(15), _admin.LockedUntil);
    }

    [Fact]
    public async Task LoginAsync_LockedAccountWithCorrectPassword_ReturnsNull()
    {
        for (int i = 0; i < 5; i++) await Login("wrong");
        _clock.Advance(TimeSpan.FromMinutes(14));

        AdminLoginResponseDto? result = await Login(Password);

        Assert.Null(result);
        Assert.NotNull(_admin.LockedUntil);
    }

    [Fact]
    public async Task LoginAsync_After15MinuteLockout_CorrectPasswordSucceedsAndClearsLock()
    {
        for (int i = 0; i < 5; i++) await Login("wrong");
        _clock.Advance(TimeSpan.FromMinutes(15).Add(TimeSpan.FromSeconds(1)));

        AdminLoginResponseDto? result = await Login(Password);

        Assert.NotNull(result);
        Assert.Null(_admin.LockedUntil);
        Assert.Equal(0, _admin.FailedAttempts);
    }

    // "Consecutive": a success in between starts the count again.
    [Fact]
    public async Task LoginAsync_SuccessBetweenFailures_ResetsTheCount()
    {
        for (int i = 0; i < 4; i++) await Login("wrong");
        await Login(Password);
        for (int i = 0; i < 4; i++) await Login("wrong");

        Assert.Equal(4, _admin.FailedAttempts);
        Assert.Null(_admin.LockedUntil);
    }

    // ---------- FR-53 generic message (controller) ----------

    [Fact]
    public async Task Login_UnknownEmailWrongPasswordAndLocked_AllGiveSameGenericMessage()
    {
        var controller = new AdminAuthController(_service);

        string? unknown = await FailureDetail(controller, "nobody@example.com", Password);
        string? wrong = await FailureDetail(controller, Email, "wrong");
        for (int i = 0; i < 4; i++) await Login("wrong"); // now 5 failures: locked
        string? locked = await FailureDetail(controller, Email, Password);

        Assert.Equal(AdminAuthController.LoginFailedMessage, unknown);
        Assert.Equal(unknown, wrong);
        Assert.Equal(unknown, locked);
        Assert.DoesNotContain("password is", unknown, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task<string?> FailureDetail(AdminAuthController controller, string email, string password)
    {
        ActionResult<AdminLoginResponseDto> response = await controller.Login(new AdminLoginRequestDto(email, password));
        var unauthorized = Assert.IsType<UnauthorizedObjectResult>(response.Result);
        return Assert.IsType<ProblemDetails>(unauthorized.Value).Detail;
    }
}
