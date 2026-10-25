// Small helpers shared by the admin screens.
import { ADMIN_STRINGS } from './adminStrings.js';

const TIME_ZONE = 'Asia/Colombo';

// FR-48: e.g. "25 Oct 2026, 14:05" in Sri Lanka time.
export function formatDateTime(isoText) {
    const date = new Date(isoText);
    if (!isoText || Number.isNaN(date.getTime())) return '';
    return new Intl.DateTimeFormat('en-GB', {
        timeZone: TIME_ZONE, day: 'numeric', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit', hour12: false,
    }).format(date);
}

export function formatKm(km) {
    const value = Number(km);
    return km === null || km === undefined || !Number.isFinite(value) ? '' : `${value.toFixed(1)} km`;
}

// A friendly message for any failed admin request.
export function errorMessage(error) {
    if (!error || typeof error.status !== 'number') return ADMIN_STRINGS.serverError;
    if (error.status === 0) return ADMIN_STRINGS.networkError;
    if (error.status === 403) return ADMIN_STRINGS.forbidden;
    if (error.status === 404) return ADMIN_STRINGS.notFound;
    if (error.status === 502 && error.detail) return error.detail; // photo storage problem, explained by the server
    if (error.status >= 500) return ADMIN_STRINGS.serverError;
    return error.detail || error.messages[0] || ADMIN_STRINGS.serverError;
}
