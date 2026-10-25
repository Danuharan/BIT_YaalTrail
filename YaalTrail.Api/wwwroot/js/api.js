// All calls to the YaalTrail Web API.

async function getJson(url) {
    const response = await fetch(url, { headers: { Accept: 'application/json' } });
    if (!response.ok) {
        throw new Error(`Request to ${url} failed with status ${response.status}`);
    }
    return response.json();
}

// Thrown when the API answers with an error. detail is the server's
// ProblemDetails text (e.g. "Start time must be in HH:mm format"), if any.
export class ApiError extends Error {
    constructor(status, detail) {
        super(detail || `Request failed with status ${status}`);
        this.status = status;
        this.detail = detail;
    }
}

async function postJson(url, body) {
    const response = await fetch(url, {
        method: 'POST',
        headers: { Accept: 'application/json', 'Content-Type': 'application/json' },
        body: JSON.stringify(body),
    });
    if (!response.ok) {
        let detail = null;
        try {
            detail = (await response.json()).detail ?? null;
        } catch {
            // The body was not JSON; keep the status only.
        }
        throw new ApiError(response.status, detail);
    }
    return response.json();
}

// FR-02
export function getCategories() {
    return getJson('/api/categories');
}

// FR-15: reference location from the Planner settings, not a number in this file.
export function getPlannerSettings() {
    return getJson('/api/settings/planner');
}

// FR-01 to FR-05: returns { count, items }.
export function getPlaces({ categoryIds, search, sort }) {
    const params = new URLSearchParams();
    if (categoryIds.length > 0) params.set('categories', categoryIds.join(','));
    if (search) params.set('search', search);
    params.set('sort', sort);
    return getJson(`/api/places?${params}`);
}

// FR-08
export function getPlace(id) {
    return getJson(`/api/places/${encodeURIComponent(id)}`);
}

// FR-23 to FR-32: body is { startTime, planDate, placeIds, manualOrder, durationOverrides }.
// The server keeps nothing; the plan itself lives in the browser (FR-33, NFR-16).
export function calculatePlan(request) {
    return postJson('/api/plan/calculate', request);
}
