// Place catalogue: category chips, place cards, result count and messages.
import { el } from './dom.js';
import { STRINGS } from './strings.js';
import { categoryClass, formatDistance } from './format.js';
import { createPlanToggle } from './planToggle.js';

// ---------- Category chips (FR-02) ----------

export function renderChips(container, categories, onToggle) {
    const allChip = createChip(STRINGS.allCategories, '', () => onToggle(null));
    const categoryChips = categories.map((category) =>
        createChip(category.name, String(category.id), () => onToggle(category.id)));

    container.replaceChildren(allChip, ...categoryChips);
}

function createChip(label, id, onClick) {
    return el('button', {
        type: 'button',
        className: 'chip',
        'data-id': id,
        'aria-pressed': 'false',
        onClick,
    }, [label]);
}

// aria-pressed tells screen readers which chips are selected;
// the CSS also shows a tick, so selection is not shown by colour alone.
export function updateChips(container, selectedIds) {
    for (const chip of container.querySelectorAll('.chip')) {
        const id = chip.dataset.id;
        const pressed = id === '' ? selectedIds.size === 0 : selectedIds.has(Number(id));
        chip.setAttribute('aria-pressed', String(pressed));
    }
}

// ---------- Result count (FR-05) ----------

export function renderResultCount(target, count) {
    target.textContent = count === null ? '' : STRINGS.resultCount(count);
}

// ---------- Empty state / error message (FR-06) ----------

// action is optional: { label, onClick }
export function renderListMessage(container, text, action) {
    if (!text) {
        container.hidden = true;
        container.replaceChildren();
        return;
    }

    container.hidden = false;
    container.replaceChildren(el('p', { text }));
    if (action) {
        container.append(el('button', { type: 'button', className: 'button button-secondary', onClick: action.onClick }, [action.label]));
    }
}

// ---------- Place cards (FR-01) ----------

// plan = { has(id), toggle(place) }. The card itself is a button, so the
// FR-21 "Add to day plan" button sits next to it rather than inside it.
export function renderPlaceList(list, places, onSelect, plan) {
    const items = places.map((place) => el('li', { className: `card-item ${categoryClass(place.category)}` }, [
        createPlaceCard(place, onSelect),
        el('div', { className: 'card-footer' }, [createPlanToggle(place, plan, 'button-small')]),
    ]));
    list.replaceChildren(...items);
}

function createPlaceCard(place, onSelect) {
    const card = el('button', {
        type: 'button',
        className: `card ${categoryClass(place.category)}`,
        onClick: () => onSelect(place.id, card),
    }, [
        createCardPhoto(place),
        el('span', { className: 'card-body' }, [
            el('span', { className: 'tag', text: place.category }),
            el('span', { className: 'card-title', text: place.name }),
            el('span', { className: 'card-meta' }, [
                el('span', { text: STRINGS.distanceFromReference(formatDistance(place.distanceKm)) }),
                el('span', { className: 'card-fee', text: place.entryFeeText }),
            ]),
        ]),
    ]);
    return card;
}

function createCardPhoto(place) {
    if (!place.photoUrl) return createPhotoPlaceholder(place.category);

    // The place name is already in the card text, so the image is decorative here.
    const img = el('img', {
        className: 'card-photo',
        src: place.photoUrl,
        alt: '',
        loading: 'lazy',
        onError: () => img.replaceWith(createPhotoPlaceholder(place.category)),
    });
    return img;
}

export function createPhotoPlaceholder(categoryName) {
    return el('span', {
        className: 'photo-placeholder',
        'aria-hidden': 'true',
        text: String(categoryName ?? '?').charAt(0),
    });
}
