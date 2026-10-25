using System.Security.Cryptography;

namespace YaalTrail.Api.Services;

// FR-50, NFR-12: passwords are stored only as a salted PBKDF2 one-way hash.
public interface IPasswordHasher
{
    string Hash(string password);
    bool Verify(string password, string storedHash);
}

public class PasswordHasher : IPasswordHasher
{
    private const int SaltSize = 16;        // 128-bit random salt
    private const int HashSize = 32;        // 256-bit derived key
    private const int Iterations = 100_000; // NFR-12 requires at least 10,000

    // Stored format: Base64(salt + hash).
    public string Hash(string password)
    {
        byte[] salt = RandomNumberGenerator.GetBytes(SaltSize);
        byte[] hash = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);

        byte[] combined = new byte[SaltSize + HashSize];
        salt.CopyTo(combined, 0);
        hash.CopyTo(combined, SaltSize);
        return Convert.ToBase64String(combined);
    }

    public bool Verify(string password, string storedHash)
    {
        byte[] combined;
        try
        {
            combined = Convert.FromBase64String(storedHash);
        }
        catch (FormatException)
        {
            return false;
        }

        if (combined.Length != SaltSize + HashSize)
            return false;

        byte[] salt = combined[..SaltSize];
        byte[] expected = combined[SaltSize..];
        byte[] actual = Rfc2898DeriveBytes.Pbkdf2(password, salt, Iterations, HashAlgorithmName.SHA256, HashSize);

        // Constant-time comparison so timing does not leak how many bytes matched.
        return CryptographicOperations.FixedTimeEquals(actual, expected);
    }
}
