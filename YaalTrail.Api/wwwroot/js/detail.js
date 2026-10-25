// Place detail view shown inside the <dialog> (FR-08 to FR-13, NFR-07, NFR-25).
import { el } from './dom.js';
import { STRINGS } from './strings.js';
import { createPhotoPlaceholder } from './catalogue.js';
import {
    categoryClass, formatCoordinates, formatDate, formatDistance, formatHours, todayNameInColombo,
} from './format.js';

export function renderDetailLoading(container, onClose) {
    container.className = 'detail';
    container.replaceChildren(
        createHeader(STRINGS.loading, null, onClose),
        el('div', { className: 'detail-body' }, [el('p', { className: 'muted', text: STRINGS.loading })]),
    );
}

export function renderDetailError(container, onClose) {
    container.className = 'detail';
    container.replaceChildren(
        createHeader(STRINGS.placeUnavailable, null, onClose),
        el('div', { className: 'detail-body' }, [el('p', { className: 'error-text', text: STRINGS.detailFailed })]),
    );
}

export function renderDetail(container, place, { onClose, onAddToPlan }) {
    container.className = `detail ${categoryClass(place.category)}`;
    container.replaceChildren(
        createHeader(place.name, place.category, onClose),
        el('div', { className: 'detail-body' }, [
            createGallery(place),
            createStatusRow(place),
            createFacts(place),
            createTextSection(STRINGS.about, place.description),
            createTextSection(STRINGS.travelTips, place.travelTips),
            createHoursTable(place.openingHours),
            createNotice(),
            createActions(place, onAddToPlan),
        ]),
    );
}

function createHeader(title, category, onClose) {
    return el('header', { className: 'detail-header' }, [
        el('div', { className: 'detail-heading' }, [
            category ? el('span', { className: 'tag', text: category }) : null,
            el('h2', { id: 'detail-title', text: title }),
        ]),
        el('button', {
            type: 'button',
            className: 'icon-button',
            'aria-label': STRINGS.closeDetails,
            onClick: onClose,
        }, [el('span', { 'aria-hidden': 'true', text: '✕' })]),
    ]);
}

// FR-11: up to five photos; a placeholder when there are none.
function createGallery(place) {
    if (place.images.length === 0) {
        return el('div', { className: 'gallery' }, [createPhotoPlaceholder(place.category)]);
    }

    const photos = place.images.map((image) => el('img', {
        src: image.imageUrl,
        alt: image.caption || STRINGS.photoOf(place.name),
        loading: 'lazy',
    }));
    return el('div', { className: `gallery gallery-${Math.min(photos.length, 5)}` }, photos);
}

// FR-09, NFR-25: open/closed shown with an icon AND a text label.
function createStatusRow(place) {
    const badge = place.isOpenNow
        ? el('span', { className: 'status-badge is-open' }, [el('span', { 'aria-hidden': 'true', text: '✓' }), STRINGS.openNow])
        : el('span', { className: 'status-badge is-closed' }, [el('span', { 'aria-hidden': 'true', text: '✕' }), STRINGS.closedNow]);

    const todayHours = `${STRINGS.today}: ${formatHours(place.todayOpenTime, place.todayCloseTime)}`;

    return el('div', { className: 'status-row' }, [badge, el('span', { className: 'today-hours', text: todayHours })]);
}

// FR-08, FR-10, FR-13
function createFacts(place) {
    const facts = [
        [STRINGS.distance, STRINGS.distanceFromReference(formatDistance(place.distanceKm))],
        [STRINGS.entryFee, place.entryFeeText],
        [STRINGS.coordinates, formatCoordinates(place.latitude, place.longitude)],
        [STRINGS.lastUpdated, formatDate(place.updatedAt)],
    ];

    return el('dl', { className: 'facts' }, facts.map(([label, value]) =>
        el('div', { className: 'fact' }, [el('dt', { text: label }), el('dd', { text: value })])));
}

function createTextSection(title, text) {
    return el('section', { className: 'detail-section' }, [
        el('h3', { text: title }),
        el('p', { text }),
    ]);
}

// FR-44: all seven days, with today's row marked by text, not only colour.
function createHoursTable(openingHours) {
    const today = todayNameInColombo();

    const rows = openingHours.map((hour) => {
        const isToday = hour.day === today;
        return el('tr', { className: isToday ? 'is-today' : null }, [
            el('th', { scope: 'row' }, [
                hour.day,
                isToday ? el('span', { className: 'today-marker', text: STRINGS.todayMarker }) : null,
            ]),
            el('td', { text: formatHours(hour.openTime, hour.closeTime) }),
        ]);
    });

    return el('section', { className: 'detail-section' }, [
        el('h3', { text: STRINGS.openingHours }),
        el('table', { className: 'hours-table' }, [
            el('thead', {}, [el('tr', {}, [
                el('th', { scope: 'col', text: STRINGS.day }),
                el('th', { scope: 'col', text: STRINGS.hours }),
            ])]),
            el('tbody', {}, rows),
        ]),
    ]);
}

// NFR-07
function createNotice() {
    return el('p', { className: 'notice', role: 'note' }, [
        el('span', { className: 'notice-icon', 'aria-hidden': 'true', text: '!' }),
        el('span', { text: STRINGS.indicativeNotice }),
    ]);
}

// FR-21 (placeholder until the planner is built).
function createActions(place, onAddToPlan) {
    const status = el('p', { className: 'action-status', role: 'status' });
    const button = el('button', {
        type: 'button',
        className: 'button button-primary',
        onClick: () => onAddToPlan(place, status),
    }, [STRINGS.addToPlan]);

    return el('div', { className: 'detail-actions' }, [button, status]);
}
