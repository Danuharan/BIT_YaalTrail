// Place detail view shown inside the <dialog> (FR-08 to FR-13, NFR-07, NFR-25).
import { el } from './dom.js';
import { STRINGS } from './strings.js';
import { createPhotoPlaceholder } from './catalogue.js';
import {
    categoryClass, formatCoordinates, formatDate, formatDistance, formatHours, todayNameInColombo,
} from './format.js';
import { createPlanToggle } from './planToggle.js';

const MAX_PHOTOS = 5; // FR-11

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

// plan = { has(id), toggle(place) }; onOpenPlan shows the day-plan panel.
export function renderDetail(container, place, { onClose, plan, onOpenPlan }) {
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
            createActions(place, plan, onOpenPlan),
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

// FR-11: up to five photos: one large photo and, if there are more, a row of
// thumbnail buttons to switch between them (Tab, Enter/Space, or the arrow keys).
// A placeholder is shown when there are no photos or a photo cannot be loaded (NFR-24).
function createGallery(place) {
    const images = place.images.slice(0, MAX_PHOTOS);
    if (images.length === 0) {
        return el('div', { className: 'gallery' }, [createPhotoPlaceholder(place.category)]);
    }

    const altText = (image) => image.caption || STRINGS.photoOf(place.name);
    const frame = el('div', { className: 'gallery-main' });
    const caption = el('figcaption', { className: 'gallery-caption' });
    let current = 0;

    function show(index) {
        current = index;
        const image = images[index];
        const img = el('img', {
            src: image.imageUrl,
            alt: altText(image),
            onError: () => img.replaceWith(createPhotoPlaceholder(place.category)),
        });
        frame.replaceChildren(img);
        caption.textContent = image.caption ?? '';
        caption.hidden = !image.caption;

        for (const [i, button] of thumbButtons.entries()) {
            button.setAttribute('aria-current', i === index ? 'true' : 'false');
        }
    }

    const thumbButtons = images.length > 1 ? images.map((image, index) => {
        const button = el('button', {
            type: 'button',
            className: 'gallery-thumb',
            'aria-label': STRINGS.showPhoto(index + 1, images.length, altText(image)),
            onClick: () => show(index),
        });
        const thumb = el('img', {
            src: image.imageUrl,
            alt: '', // the button's label already describes the photo
            loading: 'lazy',
            onError: () => thumb.replaceWith(el('span', { className: 'gallery-thumb-missing', 'aria-hidden': 'true', text: String(index + 1) })),
        });
        button.append(thumb);
        return button;
    }) : [];

    // Left/Right (and Home/End) move between the thumbnails and show that photo.
    const strip = thumbButtons.length > 0
        ? el('div', {
            className: 'gallery-strip',
            role: 'group',
            'aria-label': STRINGS.galleryLabel(place.name),
            onKeydown: (event) => {
                const keys = { ArrowLeft: current - 1, ArrowRight: current + 1, Home: 0, End: images.length - 1 };
                if (!(event.key in keys)) return;
                event.preventDefault();
                const next = (keys[event.key] + images.length) % images.length;
                show(next);
                thumbButtons[next].focus();
            },
        }, thumbButtons)
        : null;

    show(0);
    return el('figure', { className: 'gallery' }, [frame, caption, strip]);
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

// FR-21, FR-22: add or remove this place; the day plan can be opened from here too.
function createActions(place, plan, onOpenPlan) {
    return el('div', { className: 'detail-actions' }, [
        createPlanToggle(place, plan),
        el('button', { type: 'button', className: 'button button-secondary', onClick: onOpenPlan }, [STRINGS.viewPlan]),
    ]);
}
