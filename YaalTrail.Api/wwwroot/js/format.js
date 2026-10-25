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

// Number(null) is 0 and Number(undefined) is NaN, so a missing value must be
// caught first or the page would show "0.0 km" / "NaN km".
function toFiniteNumber(value) {
    if (value === null || value === undefined || value === '') return null;
    const number = Number(value);
    return Number.isFinite(number) ? number : null;
}

export function formatDistance(km) {
    const value = toFiniteNumber(km);
    return value === null ? NOT_AVAILABLE : `${value.toFixed(1)} km`;
}

export function formatCoordinates(latitude, longitude) {
    const lat = toFiniteNumber(latitude);
    const lng = toFiniteNumber(longitude);
    return lat === null || lng === null ? NOT_AVAILABLE : `${lat.toFixed(6)}, ${lng.toFixed(6)}`;
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
    if (!openTime && !closeTime) return NOT_AVAILABLE;
    if (openTime === API_CLOSED) return STRINGS.closed;
    if (!openTime || !closeTime || openTime === NOT_AVAILABLE || closeTime === NOT_AVAILABLE) return NOT_AVAILABLE;
    if (openTime === '00:00' && closeTime === '23:59') return STRINGS.open24h;
    return `${openTime} – ${closeTime}`;
}

// Today's weekday name in Colombo, e.g. "Monday" (matches the API's day names).
export function todayNameInColombo() {
    return new Intl.DateTimeFormat('en-US', { timeZone: TIME_ZONE, weekday: 'long' }).format(new Date());
}

// Today's date in Colombo as "yyyy-MM-dd" (the format the plan API expects).
// The en-CA locale writes dates exactly in that order.
export function todayIsoInColombo() {
    return new Intl.DateTimeFormat('en-CA', {
        timeZone: TIME_ZONE, year: 'numeric', month: '2-digit', day: '2-digit',
    }).format(new Date());
}

// FR-32: 95 -> "1 h 35 min", 40 -> "40 min".
export function formatMinutes(minutes) {
    const total = Math.max(0, Math.round(Number(minutes) || 0));
    const hours = Math.floor(total / 60);
    const rest = total % 60;
    if (hours === 0) return `${rest} min`;
    return rest === 0 ? `${hours} h` : `${hours} h ${rest} min`;
}
