// Admin console entry point: sign-in, sign-out, idle timeout and screen routing.
// Screens: #/places, #/places/new, #/places/{id}, #/categories (BR-02: all need sign-in).
import { el } from '../dom.js';
import { ADMIN_STRINGS as S } from './adminStrings.js';
import { AdminApiError, configureApi, getCategories, login, logout } from './adminApi.js';
import { clearSession, loadSession, saveSession, sessionEndReason, startIdleWatch } from './session.js';
import { showPlacesScreen } from './placesView.js';
import { showPlaceForm } from './placeForm.js';
import { showCategoriesScreen } from './categoriesView.js';

const elements = {
    nav: document.getElementById('admin-nav'),
    navPlaces: document.getElementById('nav-places'),
    navCategories: document.getElementById('nav-categories'),
    user: document.getElementById('admin-user'),
    userName: document.getElementById('admin-user-name'),
    userEmail: document.getElementById('admin-user-email'),
    signOut: document.getElementById('sign-out'),
    idleWarning: document.getElementById('idle-warning'),
    idleWarningText: document.getElementById('idle-warning-text'),
    staySignedIn: document.getElementById('stay-signed-in'),
    loginScreen: document.getElementById('login-screen'),
    loginForm: document.getElementById('login-form'),
    loginEmail: document.getElementById('login-email'),
    loginPassword: document.getElementById('login-password'),
    loginSubmit: document.getElementById('login-submit'),
    loginReason: document.getElementById('login-reason'),
    loginError: document.getElementById('login-error'),
    appScreen: document.getElementById('app-screen'),
    screen: document.getElementById('screen'),
    status: document.getElementById('admin-status'),
    alert: document.getElementById('admin-alert'),
    dialog: document.getElementById('confirm-dialog'),
};

const END_REASONS = {
    signedOut: S.reasonSignedOut,
    idle: S.reasonIdle,
    expired: S.reasonExpired,
    unauthorized: S.reasonUnauthorized,
};

let session = loadSession();
let idleWatch = null;
let pendingMessage = null; // shown on the next screen, e.g. "Place saved"

// What every screen needs from here.
const ctx = {
    dialog: elements.dialog,
    navigate,
    showMessage,
};

configureApi({
    getToken: () => session?.token ?? null,
    onUnauthorized: () => endSession('unauthorized'), // FR-52: back to sign-in on any 401
    onServerContact: () => idleWatch?.markServerContact(),
});

init();

function init() {
    elements.loginForm.addEventListener('submit', onLogin);
    elements.signOut.addEventListener('click', signOut);
    elements.staySignedIn.addEventListener('click', () => {
        idleWatch?.stayActive();
        hideIdleWarning();
    });
    window.addEventListener('hashchange', route);

    // A session saved in this tab may already be too old (FR-51, FR-55).
    const reason = session ? sessionEndReason(session) : null;
    if (reason) {
        session = null;
        clearSession();
        showLogin(END_REASONS[reason]);
        return;
    }

    if (session) startSession();
    route();
}

// ---------- Sign-in (FR-49 to FR-54) ----------

async function onLogin(event) {
    event.preventDefault();
    setLoginError(null);

    const email = elements.loginEmail.value.trim();
    const password = elements.loginPassword.value;
    if (!email || !password || !elements.loginEmail.checkValidity()) {
        setLoginError(S.loginInvalidInput);
        return;
    }

    elements.loginSubmit.disabled = true;
    elements.loginSubmit.textContent = S.signingIn;
    try {
        const result = await login(email, password);
        const expiresAt = Date.parse(result.expiresAt);
        session = {
            token: result.accessToken,
            displayName: result.displayName,
            email,
            expiresAt: Number.isFinite(expiresAt) ? expiresAt : Date.now() + 60 * 60 * 1000,
            lastActivity: Date.now(),
        };
        saveSession(session);
        elements.loginPassword.value = '';
        startSession();
        route();
    } catch (error) {
        elements.loginPassword.value = '';
        setLoginError(loginErrorText(error));
        elements.loginPassword.focus();
    } finally {
        elements.loginSubmit.disabled = false;
        elements.loginSubmit.textContent = S.signIn;
    }
}

// FR-53: only the server's own generic message is shown for a failed sign-in.
function loginErrorText(error) {
    if (!(error instanceof AdminApiError)) return S.serverError;
    if (error.status === 401) return error.detail || S.reasonUnauthorized;
    if (error.status === 400) return S.loginInvalidInput;
    if (error.status === 0) return S.networkError;
    return S.serverError;
}

function setLoginError(text) {
    elements.loginError.textContent = text ?? '';
}

function startSession() {
    elements.userName.textContent = session.displayName;
    elements.userEmail.textContent = session.email;
    elements.user.hidden = false;
    elements.nav.hidden = false;
    elements.loginScreen.hidden = true;
    elements.appScreen.hidden = false;
    setLoginError(null);

    idleWatch?.stop();
    idleWatch = startIdleWatch(session, {
        onWarning: showIdleWarning,
        onActive: hideIdleWarning,
        onEnd: (reason) => {
            logout().catch(() => {}); // tell the server too (FR-56); ignore if it fails
            endSession(reason);
        },
        // Keeps the server-side session alive while the admin works in a form (FR-55).
        keepAlive: () => getCategories().catch(() => {}),
    });
}

// ---------- Sign-out (FR-55, FR-56) ----------

function signOut() {
    // The request is sent with the current token before the session is cleared below.
    logout().catch(() => {});
    endSession('signedOut');
}

function endSession(reason) {
    if (!session) return;
    session = null;
    clearSession();
    idleWatch?.stop();
    idleWatch = null;
    if (elements.dialog.open) elements.dialog.close();
    showLogin(END_REASONS[reason]);
}

function showLogin(reasonText) {
    hideIdleWarning();
    elements.user.hidden = true;
    elements.nav.hidden = true;
    elements.appScreen.hidden = true;
    elements.screen.replaceChildren();
    clearMessages();
    elements.loginScreen.hidden = false;

    elements.loginReason.textContent = reasonText ?? '';
    elements.loginEmail.focus();
}

function showIdleWarning(minutes) {
    elements.idleWarningText.textContent = S.idleWarning(minutes);
    elements.idleWarning.hidden = false;
}

function hideIdleWarning() {
    elements.idleWarning.hidden = true;
}

// ---------- Screens ----------

function route() {
    if (!session) {
        showLogin(null);
        return;
    }

    clearMessages();
    if (pendingMessage) {
        showMessage(pendingMessage);
        pendingMessage = null;
    }

    const hash = location.hash;
    const placeMatch = hash.match(/^#\/places\/(\d+)$/);

    if (hash === '#/categories') {
        setCurrentNav(elements.navCategories);
        showCategoriesScreen(elements.screen, ctx);
    } else if (hash === '#/places/new') {
        setCurrentNav(elements.navPlaces);
        showPlaceForm(elements.screen, null, ctx);
    } else if (placeMatch) {
        setCurrentNav(elements.navPlaces);
        showPlaceForm(elements.screen, Number(placeMatch[1]), ctx);
    } else {
        setCurrentNav(elements.navPlaces);
        showPlacesScreen(elements.screen, ctx);
    }
}

// Moves to a screen and shows a message there (e.g. after saving).
function navigate(hash, message) {
    pendingMessage = message ?? null;
    if (location.hash === hash) route();
    else location.hash = hash;
}

function setCurrentNav(link) {
    for (const item of [elements.navPlaces, elements.navCategories]) {
        if (item === link) item.setAttribute('aria-current', 'page');
        else item.removeAttribute('aria-current');
    }
}

// ---------- Messages ----------

// kind 'info' is announced politely; 'error' is announced at once.
function showMessage(text, kind = 'info') {
    clearMessages();
    const region = kind === 'error' ? elements.alert : elements.status;
    region.replaceChildren(el('p', { text }));
}

function clearMessages() {
    elements.status.replaceChildren();
    elements.alert.replaceChildren();
}
