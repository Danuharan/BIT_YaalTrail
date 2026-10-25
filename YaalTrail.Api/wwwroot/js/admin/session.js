// The signed-in administrator's session, kept in sessionStorage so it ends when the
// browser tab closes (FR-51), plus the 30-minute inactivity sign-out (FR-55).
// Storage can be blocked (private windows, strict settings), so every access is
// wrapped in try/catch; the console then still works until the page is reloaded.

const STORAGE_KEY = 'yaaltrail.admin.session';

export const IDLE_LIMIT_MS = 30 * 60 * 1000;    // FR-55
const WARNING_BEFORE_MS = 2 * 60 * 1000;         // warn 2 minutes before signing out
const KEEP_ALIVE_MS = 10 * 60 * 1000;            // see keepAlive below
const CHECK_EVERY_MS = 15 * 1000;
const SAVE_ACTIVITY_EVERY_MS = 15 * 1000;

let memorySession = null; // used when sessionStorage is not available

// session = { token, displayName, email, expiresAt (ms), lastActivity (ms) }
export function loadSession() {
    let session = memorySession;
    try {
        const saved = JSON.parse(sessionStorage.getItem(STORAGE_KEY));
        if (saved) session = saved;
    } catch {
        // keep the in-memory copy
    }

    if (!session || typeof session.token !== 'string' || !session.token) return null;
    return session;
}

export function saveSession(session) {
    memorySession = session;
    try {
        sessionStorage.setItem(STORAGE_KEY, JSON.stringify(session));
    } catch {
        // in memory only
    }
}

export function clearSession() {
    memorySession = null;
    try {
        sessionStorage.removeItem(STORAGE_KEY);
    } catch {
        // nothing stored
    }
}

// Returns why a stored session can no longer be used, or null if it is still fine.
export function sessionEndReason(session, now = Date.now()) {
    if (now >= session.expiresAt) return 'expired';
    if (now - session.lastActivity >= IDLE_LIMIT_MS) return 'idle';
    return null;
}

// FR-55: watches mouse, keyboard and touch activity.
//  onWarning(minutesLeft) - shortly before the idle sign-out
//  onActive()             - activity after a warning was shown
//  onEnd(reason)          - 'idle' or 'expired'
//  keepAlive()            - the server measures inactivity from the last request, so while
//                           the admin is busy in a form without saving, ping it now and then.
// Returns { stop, markServerContact, stayActive }.
export function startIdleWatch(session, { onWarning, onActive, onEnd, keepAlive }) {
    let lastServerContact = Date.now();
    let lastSaved = 0;
    let warned = false;
    let ended = false;

    const events = ['pointerdown', 'keydown', 'wheel', 'touchstart', 'input'];

    function recordActivity() {
        if (ended) return;
        const now = Date.now();
        session.lastActivity = now;

        if (now - lastSaved > SAVE_ACTIVITY_EVERY_MS) {
            saveSession(session);
            lastSaved = now;
        }
        if (warned) {
            warned = false;
            onActive();
        }
        if (now - lastServerContact > KEEP_ALIVE_MS) {
            lastServerContact = now;
            keepAlive();
        }
    }

    function check() {
        if (ended) return;
        const now = Date.now();
        const reason = sessionEndReason(session, now);
        if (reason) {
            end(reason);
            return;
        }

        const idleLeft = IDLE_LIMIT_MS - (now - session.lastActivity);
        if (idleLeft <= WARNING_BEFORE_MS && !warned) {
            warned = true;
            onWarning(Math.max(1, Math.ceil(idleLeft / 60000)));
        }
    }

    function end(reason) {
        stop();
        onEnd(reason);
    }

    // Timers slow down in background tabs, so check again when the tab is shown.
    function onVisibility() {
        if (document.visibilityState === 'visible') check();
    }

    function stop() {
        ended = true;
        clearInterval(timer);
        for (const name of events) document.removeEventListener(name, recordActivity, true);
        document.removeEventListener('visibilitychange', onVisibility);
    }

    for (const name of events) document.addEventListener(name, recordActivity, { capture: true, passive: true });
    document.addEventListener('visibilitychange', onVisibility);
    const timer = setInterval(check, CHECK_EVERY_MS);

    return {
        stop,
        markServerContact: () => { lastServerContact = Date.now(); },
        stayActive: recordActivity,
    };
}
