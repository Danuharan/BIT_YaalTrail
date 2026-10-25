using YaalTrail.Api.Services;

namespace YaalTrail.Tests;

// FR-55 inactivity timeout, FR-56 sign-out, FR-51 token lifetime.
public class AdminSessionStoreTests
{
    private readonly FakeClock _clock = FakeClock.MondayMorning();
    private readonly AdminSessionStore _store;

    public AdminSessionStoreTests()
    {
        _store = new AdminSessionStore(_clock);
    }

    private void StartSession(string jti) => _store.Start(jti, _clock.UtcNow.AddMinutes(60));

    [Fact]
    public void TryTouch_UnknownJti_ReturnsFalse()
    {
        Assert.False(_store.TryTouch("never-started"));
    }

    [Fact]
    public void TryTouch_ActiveSession_ReturnsTrue()
    {
        StartSession("a");
        _clock.Advance(TimeSpan.FromMinutes(29));

        Assert.True(_store.TryTouch("a"));
    }

    [Fact]
    public void TryTouch_IdleMoreThan30Minutes_ReturnsFalseAndEndsSession()
    {
        StartSession("a");
        _clock.Advance(TimeSpan.FromMinutes(30).Add(TimeSpan.FromSeconds(1)));

        Assert.False(_store.TryTouch("a"));
    }

    // Each request restarts the 30-minute idle timer.
    [Fact]
    public void TryTouch_RegularActivity_KeepsSessionAliveUntilTokenExpires()
    {
        StartSession("a");
        for (int i = 0; i < 2; i++)
        {
            _clock.Advance(TimeSpan.FromMinutes(25));
            Assert.True(_store.TryTouch("a"));
        }

        _clock.Advance(TimeSpan.FromMinutes(10)); // 60 minutes after sign-in: token expired
        Assert.False(_store.TryTouch("a"));
    }

    [Fact]
    public void End_ActiveSession_TryTouchReturnsFalse()
    {
        StartSession("a");

        _store.End("a");

        Assert.False(_store.TryTouch("a"));
    }

    [Fact]
    public void End_OneSession_LeavesOtherSessionsActive()
    {
        StartSession("a");
        StartSession("b");

        _store.End("a");

        Assert.True(_store.TryTouch("b"));
    }

    [Fact]
    public void Start_NewSession_RemovesIdleSessions()
    {
        StartSession("old");
        _clock.Advance(TimeSpan.FromMinutes(31));

        StartSession("new");

        Assert.False(_store.TryTouch("old"));
        Assert.True(_store.TryTouch("new"));
    }
}
