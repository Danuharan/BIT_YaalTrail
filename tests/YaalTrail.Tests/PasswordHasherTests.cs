using YaalTrail.Api.Services;

namespace YaalTrail.Tests;

// FR-50, NFR-12
public class PasswordHasherTests
{
    private readonly PasswordHasher _hasher = new();

    [Fact]
    public void Verify_SamePasswordAsHashed_ReturnsTrue()
    {
        string hash = _hasher.Hash("Correct horse battery staple");

        Assert.True(_hasher.Verify("Correct horse battery staple", hash));
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        string hash = _hasher.Hash("Correct horse battery staple");

        Assert.False(_hasher.Verify("correct horse battery staple", hash));
    }

    // A random salt per hash means equal passwords never give equal hashes.
    [Fact]
    public void Hash_SamePasswordTwice_GivesDifferentHashes()
    {
        string first = _hasher.Hash("same password");
        string second = _hasher.Hash("same password");

        Assert.NotEqual(first, second);
        Assert.True(_hasher.Verify("same password", first));
        Assert.True(_hasher.Verify("same password", second));
    }

    [Fact]
    public void Hash_AnyPassword_DoesNotContainThePassword()
    {
        string hash = _hasher.Hash("plain-text-secret");

        Assert.DoesNotContain("plain-text-secret", hash);
    }

    [Theory]
    [InlineData("not base64 !!")]
    [InlineData("c2hvcnQ=")] // valid Base64 but far too short to hold salt + hash
    [InlineData("")]
    public void Verify_MalformedStoredHash_ReturnsFalse(string storedHash)
    {
        Assert.False(_hasher.Verify("anything", storedHash));
    }
}
