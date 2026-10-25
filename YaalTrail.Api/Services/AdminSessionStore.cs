using System.Collections.Concurrent;

namespace YaalTrail.Api.Services;

// FR-55, FR-56: server-side record of active admin sessions, keyed by the token's jti.
// A token is only accepted while its jti is in this store, so:
//  - signing out removes the jti and the token stops working at once (FR-56);
//  - a session idle for more than 30 minutes is removed (FR-55).
// Kept in memory (single server): restarting the API signs every admin out.
public interface IAdminSessionStore
{
    void Start(string jti, DateTime expiresAtUtc);

    // Called on every authenticated request. Returns false if the session was
    // signed out, has expired, or has been idle too long; otherwise records the activity.
    bool TryTouch(string jti);

    void End(string jti);
}

public class AdminSessionStore : IAdminSessionStore
{
    // FR-55
    public static readonly TimeSpan IdleTimeout = TimeSpan.FromMinutes(30);

    private record Session(DateTime ExpiresAtUtc, DateTime LastActivityUtc);

    private readonly ConcurrentDictionary<string, Session> _sessions = new();
    private readonly TimeProvider _clock;

    public AdminSessionStore(TimeProvider clock)
    {
        _clock = clock;
    }

    public void Start(string jti, DateTime expiresAtUtc)
    {
        DateTime now = _clock.GetUtcNow().UtcDateTime;
        RemoveFinishedSessions(now);
        _sessions[jti] = new Session(expiresAtUtc, now);
    }

    public bool TryTouch(string jti)
    {
        DateTime now = _clock.GetUtcNow().UtcDateTime;

        if (!_sessions.TryGetValue(jti, out Session? session))
            return false; // signed out, or the server was restarted

        if (IsFinished(session, now))
        {
            _sessions.TryRemove(jti, out _);
            return false;
        }

        _sessions[jti] = session with { LastActivityUtc = now };
        return true;
    }

    public void End(string jti)
    {
        _sessions.TryRemove(jti, out _);
    }

    private static bool IsFinished(Session session, DateTime now)
    {
        return now >= session.ExpiresAtUtc || now - session.LastActivityUtc > IdleTimeout;
    }

    // Housekeeping so abandoned sessions do not pile up in memory.
    private void RemoveFinishedSessions(DateTime now)
    {
        foreach (var entry in _sessions)
        {
            if (IsFinished(entry.Value, now))
                _sessions.TryRemove(entry.Key, out _);
        }
    }
}
