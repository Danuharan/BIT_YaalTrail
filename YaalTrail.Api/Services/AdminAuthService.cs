using System.Security.Claims;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using YaalTrail.Api.Configuration;
using YaalTrail.Api.Data;
using YaalTrail.Api.Dtos;
using YaalTrail.Api.Models;

namespace YaalTrail.Api.Services;

public interface IAdminAuthService
{
    // Returns null on any failure (FR-53: the caller shows one generic message).
    Task<AdminLoginResponseDto?> LoginAsync(AdminLoginRequestDto request);

    // FR-56
    void Logout(string jti);
}

public class AdminAuthService : IAdminAuthService
{
    public const string AdminRole = "Admin";

    // FR-51: at most 60 minutes.
    public static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(60);

    // FR-54
    public const int MaxFailedAttempts = 5;
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    // Used when the email is unknown so the response takes as long as a wrong
    // password; otherwise the timing would reveal which emails exist (FR-53).
    private static string? _dummyHash;

    private readonly AppDbContext _db;
    private readonly IPasswordHasher _hasher;
    private readonly IAdminSessionStore _sessions;
    private readonly TimeProvider _clock;
    private readonly JwtOptions _jwt;
    private readonly ILogger<AdminAuthService> _logger;

    public AdminAuthService(
        AppDbContext db,
        IPasswordHasher hasher,
        IAdminSessionStore sessions,
        TimeProvider clock,
        IOptions<JwtOptions> jwt,
        ILogger<AdminAuthService> logger)
    {
        _db = db;
        _hasher = hasher;
        _sessions = sessions;
        _clock = clock;
        _jwt = jwt.Value;
        _logger = logger;
    }

    public async Task<AdminLoginResponseDto?> LoginAsync(AdminLoginRequestDto request)
    {
        DateTime now = _clock.GetUtcNow().UtcDateTime;
        string email = request.Email.Trim().ToLowerInvariant();

        // NFR-13: EF Core sends the email as a query parameter.
        AdminUser? admin = await _db.AdminUsers.FirstOrDefaultAsync(a => a.Email.ToLower() == email);

        if (admin == null)
        {
            _dummyHash ??= _hasher.Hash(Guid.NewGuid().ToString());
            _hasher.Verify(request.Password, _dummyHash);
            _logger.LogWarning("Admin login failed: unknown account");
            return null;
        }

        // FR-54: while locked, even the correct password is refused.
        if (admin.LockedUntil != null && admin.LockedUntil > now)
        {
            _hasher.Verify(request.Password, admin.PasswordHash); // same timing as a normal attempt
            _logger.LogWarning("Admin login refused: account {AdminId} is locked", admin.Id);
            return null;
        }

        // FR-50, NFR-12: PBKDF2 hash with constant-time comparison.
        if (!_hasher.Verify(request.Password, admin.PasswordHash))
        {
            admin.FailedAttempts++;

            // FR-54: five consecutive failures lock the account for 15 minutes.
            if (admin.FailedAttempts >= MaxFailedAttempts)
            {
                admin.LockedUntil = now + LockoutDuration;
                admin.FailedAttempts = 0;
                _logger.LogWarning("Admin account {AdminId} locked for {Minutes} minutes",
                    admin.Id, LockoutDuration.TotalMinutes);
            }
            else
            {
                _logger.LogWarning("Admin login failed for account {AdminId} (attempt {Attempt})",
                    admin.Id, admin.FailedAttempts);
            }

            await _db.SaveChangesAsync();
            return null;
        }

        // Success: reset the counter and record the sign-in time.
        admin.FailedAttempts = 0;
        admin.LockedUntil = null;
        admin.LastLoginAt = now;
        await _db.SaveChangesAsync();

        string jti = Guid.NewGuid().ToString("N");
        DateTime expiresAt = now + TokenLifetime;
        string token = CreateToken(admin, jti, now, expiresAt);

        _sessions.Start(jti, expiresAt); // FR-55, FR-56
        _logger.LogInformation("Admin {AdminId} signed in", admin.Id);

        return new AdminLoginResponseDto(token, "Bearer", expiresAt, admin.DisplayName);
    }

    public void Logout(string jti)
    {
        _sessions.End(jti);
    }

    // FR-51: HS256-signed JWT with a unique jti so the session can be tracked and revoked.
    private string CreateToken(AdminUser admin, string jti, DateTime issuedAt, DateTime expiresAt)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Key));

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _jwt.Issuer,
            Audience = _jwt.Audience,
            IssuedAt = issuedAt,
            NotBefore = issuedAt,
            Expires = expiresAt,
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
            Subject = new ClaimsIdentity(new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, admin.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, jti),
                new Claim(JwtRegisteredClaimNames.Email, admin.Email),
                new Claim("name", admin.DisplayName),
                new Claim("role", AdminRole),
            }),
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
