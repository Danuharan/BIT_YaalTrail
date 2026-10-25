import { STRINGS } from './strings.js';

// Times are shown in Sri Lanka Standard Time (SRS section 1).
const TIME_ZONE = 'Asia/Colombo';

// Values sent by the API (PlaceService).
export const NOT_AVAILABLE = 'Information not available'; // FR-12
const API_CLOSED = 'Closed';

// "Religious" -> "cat-religious", used for category colours.
export function categoryClass(categoryName) {
    return 'cat-' + String(categoryName ?? '').toLowerCase().replace(/[^a-z]/g, '');
}

export function formatDistance(km) {
    return `${Number(km).toFixed(1)} km`;
}

export function formatCoordinates(latitude, longitude) {
    return `${Number(latitude).toFixed(6)}, ${Number(longitude).toFixed(6)}`;
}

// FR-13: e.g. "25 October 2026".
export function formatDate(isoText) {
    const date = new Date(isoText);
    if (Number.isNaN(date.getTime())) return NOT_AVAILABLE;

    return new Intl.DateTimeFormat('en-GB', {
        timeZone: TIME_ZONE,
        day: 'numeric',
        month: 'long',
        year: 'numeric',
    }).format(date);
}

// "04:00" + "18:15" -> "04:00 – 18:15"; handles closed, 24-hour and missing values.
export function formatHours(openTime, closeTime) {
    if (openTime === API_CLOSED) return STRINGS.closed;
    if (openTime === NOT_AVAILABLE || closeTime === NOT_AVAILABLE) return NOT_AVAILABLE;
    if (openTime === '00:00' && closeTime === '23:59') return STRINGS.open24h;
    return `${openTime} – ${closeTime}`;
}

// Today's weekday name in Colombo, e.g. "Monday" (matches the API's day names).
export function todayNameInColombo() {
    return new Intl.DateTimeFormat('en-US', { timeZone: TIME_ZONE, weekday: 'long' }).format(new Date());
}
