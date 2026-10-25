// All calls to the admin endpoints (SRS 4.6, 4.7). Every call except login
// sends the access token as "Authorization: Bearer ..." (FR-52).

let getToken = () => null;
let onUnauthorized = () => {};
let onServerContact = () => {};

// getToken: returns the current token or null.
// onUnauthorized: called when the server answers 401 (expired, idle or signed out).
// onServerContact: called after every authenticated request (used for the idle keep-alive).
export function configureApi(options) {
    if (options.getToken) getToken = options.getToken;
    if (options.onUnauthorized) onUnauthorized = options.onUnauthorized;
    if (options.onServerContact) onServerContact = options.onServerContact;
}

// status 0 = the server could not be reached.
// detail: the server's message. fieldErrors: { fieldName: [messages] } from ASP.NET model
// validation. messages: the list of rule errors from the admin services (FR-42, FR-43, ...).
export class AdminApiError extends Error {
    constructor(status, problem) {
        super(problem?.detail || problem?.title || `Request failed with status ${status}`);
        this.status = status;
        this.title = problem?.title ?? null;
        this.detail = problem?.detail ?? null;
        this.fieldErrors = {};
        this.messages = [];

        const errors = problem?.errors;
        if (Array.isArray(errors)) {
            this.messages = errors.filter((e) => typeof e === 'string');
        } else if (errors && typeof errors === 'object') {
            for (const [key, value] of Object.entries(errors)) {
                const list = Array.isArray(value) ? value.filter((e) => typeof e === 'string') : [];
                if (list.length > 0) this.fieldErrors[fieldKey(key)] = list;
            }
        }
    }
}

// "Latitude" -> "latitude", "$.entryFee" -> "entryFee", "OpeningHours[2].OpenTime" -> "openingHours".
function fieldKey(key) {
    const first = String(key).replace(/^\$\.?/, '').split(/[.[]/)[0];
    return first ? first.charAt(0).toLowerCase() + first.slice(1) : '';
}

async function request(method, url, { body, auth = true } = {}) {
    const headers = { Accept: 'application/json' };
    if (body !== undefined) headers['Content-Type'] = 'application/json';

    if (auth) {
        const token = getToken();
        if (!token) {
            onUnauthorized();
            throw new AdminApiError(401, null);
        }
        headers.Authorization = `Bearer ${token}`;
    }

    let response;
    try {
        response = await fetch(url, {
            method,
            headers,
            body: body === undefined ? undefined : JSON.stringify(body),
        });
    } catch {
        throw new AdminApiError(0, null);
    }

    if (auth && response.status === 401) {
        onUnauthorized();
        throw new AdminApiError(401, null);
    }
    if (auth) onServerContact();

    if (!response.ok) throw new AdminApiError(response.status, await readJson(response));
    return response.status === 204 ? null : readJson(response);
}

async function readJson(response) {
    try {
        const text = await response.text();
        return text ? JSON.parse(text) : null;
    } catch {
        return null;
    }
}

// ---------- SRS 4.7 ----------

// FR-49 to FR-54. Returns { accessToken, tokenType, expiresAt, displayName }.
export function login(email, password) {
    return request('POST', '/api/admin/login', { body: { email, password }, auth: false });
}

// FR-56: the server forgets this token's session.
export function logout() {
    return request('POST', '/api/admin/logout');
}

// ---------- SRS 4.6 places ----------

export const getPlaces = () => request('GET', '/api/admin/places');
export const getPlace = (id) => request('GET', `/api/admin/places/${encodeURIComponent(id)}`);
export const createPlace = (place) => request('POST', '/api/admin/places', { body: place });
export const updatePlace = (id, place) => request('PUT', `/api/admin/places/${encodeURIComponent(id)}`, { body: place });

// FR-41: without confirm the server answers 400 with how many saved plans use the place.
export function deletePlace(id, confirm) {
    const query = confirm ? '?confirm=true' : '';
    return request('DELETE', `/api/admin/places/${encodeURIComponent(id)}${query}`);
}

// FR-47
export function setPublished(id, published) {
    return request('PATCH', `/api/admin/places/${encodeURIComponent(id)}/${published ? 'publish' : 'unpublish'}`);
}

// ---------- FR-46 categories ----------

export const getCategories = () => request('GET', '/api/admin/categories');
export const createCategory = (category) => request('POST', '/api/admin/categories', { body: category });
export const updateCategory = (id, category) => request('PUT', `/api/admin/categories/${encodeURIComponent(id)}`, { body: category });
export const deleteCategory = (id) => request('DELETE', `/api/admin/categories/${encodeURIComponent(id)}`);
