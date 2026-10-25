namespace YaalTrail.Api.Configuration;

// FR-51: settings for the administrator access token.
// NFR-15: Key is the HS256 signing key and must come only from user-secrets
// or an environment variable (Jwt__Key), never from a committed file.
public class JwtOptions
{
    public const string SectionName = "Jwt";

    // HS256 needs a key of at least 256 bits (32 bytes).
    public const int MinKeyBytes = 32;

    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = "YaalTrail.Api";
    public string Audience { get; set; } = "YaalTrail.Admin";
}
