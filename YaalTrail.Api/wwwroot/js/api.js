// All calls to the YaalTrail Web API.

async function getJson(url) {
    const response = await fetch(url, { headers: { Accept: 'application/json' } });
    if (!response.ok) {
        throw new Error(`Request to ${url} failed with status ${response.status}`);
    }
    return response.json();
}

// FR-02
export function getCategories() {
    return getJson('/api/categories');
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
