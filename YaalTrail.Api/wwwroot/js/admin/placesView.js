// Places screen: every place, including unpublished ones, with search,
// edit, delete (FR-41) and publish / unpublish (FR-47).
import { el } from '../dom.js';
import { ADMIN_STRINGS as S } from './adminStrings.js';
import { deletePlace, getPlace, getPlaces, setPublished } from './adminApi.js';
import { confirmAction } from './confirmDialog.js';
import { errorMessage, formatDateTime, formatKm } from './adminFormat.js';

// ctx = { dialog, navigate(hash, message), showMessage(text, kind) }
export async function showPlacesScreen(container, ctx) {
    const filters = { search: '', status: 'all' };
    let places = [];

    const heading = el('h1', { className: 'screen-title', tabindex: '-1', text: S.placesTitle });
    const countText = el('p', { className: 'muted', 'aria-live': 'polite' });
    const tableArea = el('div', { className: 'table-area' }, [el('p', { className: 'muted', text: S.loading })]);

    container.replaceChildren(
        el('div', { className: 'screen-head' }, [
            heading,
            el('a', { className: 'button button-primary', href: '#/places/new' }, [
                el('span', { 'aria-hidden': 'true', text: '+ ' }), S.addPlace,
            ]),
        ]),
        createToolbar(filters, render),
        countText,
        tableArea,
    );
    heading.focus();

    await load();

    async function load() {
        try {
            places = await getPlaces();
            render();
        } catch (error) {
            if (error.status === 401) return; // admin.js already shows the sign-in screen
            tableArea.replaceChildren(el('div', { className: 'message message-error', role: 'alert' }, [
                el('p', { text: errorMessage(error) }),
                el('button', { type: 'button', className: 'button button-secondary', onClick: load }, [S.retry]),
            ]));
        }
    }

    function render() {
        const shown = filterPlaces(places, filters);
        countText.textContent = S.showingCount(shown.length, places.length);
        tableArea.replaceChildren(shown.length === 0
            ? el('p', { className: 'empty', text: S.noPlaces })
            : createTable(shown, { onTogglePublish: togglePublish, onDelete: remove }));
    }

    // FR-47
    async function togglePublish(place, button) {
        button.disabled = true;
        try {
            await setPublished(place.id, !place.isPublished);
            ctx.showMessage(place.isPublished ? S.placeUnpublished(place.name) : S.placePublished(place.name));
            await load();
            focusRowAction(place.id, 'publish');
        } catch (error) {
            if (error.status === 401) return;
            button.disabled = false;
            // e.g. FR-42 / BR-01: the place is now outside the permitted radius.
            ctx.showMessage(errorMessage(error), 'error');
        }
    }

    // FR-41: find out how many saved plans use the place, ask, then delete.
    async function remove(place) {
        let savedPlans;
        try {
            savedPlans = await countSavedPlans(place.id);
        } catch (error) {
            if (error.status === 401) return;
            ctx.showMessage(errorMessage(error), 'error');
            if (error.status === 404) await load();
            return;
        }

        const confirmed = await confirmAction(ctx.dialog, {
            title: S.deleteTitle(place.name),
            lines: [savedPlansText(savedPlans), S.deleteCannotUndo],
            confirmLabel: S.deleteConfirm,
        });
        if (!confirmed) return;

        try {
            await deletePlace(place.id, true);
            ctx.showMessage(S.placeDeleted(place.name));
            await load();
            heading.focus();
        } catch (error) {
            if (error.status === 401) return;
            ctx.showMessage(errorMessage(error), 'error');
        }
    }

    function focusRowAction(placeId, action) {
        tableArea.querySelector(`[data-row="${placeId}"] [data-action="${action}"]`)?.focus();
    }
}

// The server's answer to DELETE without ?confirm=true is a 400 whose message says
// "N saved plan(s) include this place" or "No saved plans use this place".
// If that text cannot be read, fall back to SavedPlanCount from the place detail.
async function countSavedPlans(placeId) {
    try {
        await deletePlace(placeId, false);
    } catch (error) {
        if (error.status !== 400) throw error;
        const text = error.detail ?? '';
        const match = text.match(/(\d+) saved plan/);
        if (match) return Number(match[1]);
        if (/No saved plans/i.test(text)) return 0;
    }

    const detail = await getPlace(placeId);
    return Number.isInteger(detail?.savedPlanCount) ? detail.savedPlanCount : null;
}

function savedPlansText(count) {
    if (count === null) return S.deleteUnknownPlans;
    return count === 0 ? S.deleteNoSavedPlans : S.deleteSavedPlans(count);
}

function filterPlaces(places, { search, status }) {
    const term = search.trim().toLowerCase();
    return places.filter((place) => {
        if (status === 'published' && !place.isPublished) return false;
        if (status === 'unpublished' && place.isPublished) return false;
        if (!term) return true;
        return place.name.toLowerCase().includes(term) || (place.category ?? '').toLowerCase().includes(term);
    });
}

function createToolbar(filters, onChange) {
    const search = el('input', {
        id: 'place-search',
        type: 'search',
        autocomplete: 'off',
        onInput: () => {
            filters.search = search.value;
            onChange();
        },
    });

    const status = el('select', {
        id: 'place-status',
        onChange: () => {
            filters.status = status.value;
            onChange();
        },
    }, [
        el('option', { value: 'all', text: S.statusAll }),
        el('option', { value: 'published', text: S.published }),
        el('option', { value: 'unpublished', text: S.unpublished }),
    ]);

    return el('div', { className: 'toolbar' }, [
        el('div', { className: 'field' }, [el('label', { for: 'place-search', text: S.searchPlaces }), search]),
        el('div', { className: 'field' }, [el('label', { for: 'place-status', text: S.statusFilter }), status]),
    ]);
}

// On narrow screens admin.css turns each row into a card; data-label gives every cell its heading.
function createTable(places, actions) {
    const headings = [S.colName, S.colCategory, S.colDistance, S.colStatus, S.colUpdated, S.colActions];

    return el('table', { className: 'data-table' }, [
        el('caption', { className: 'visually-hidden', text: S.placesTitle }),
        el('thead', {}, [el('tr', {}, headings.map((text) => el('th', { scope: 'col', text })))]),
        el('tbody', {}, places.map((place) => createRow(place, actions))),
    ]);
}

function createRow(place, { onTogglePublish, onDelete }) {
    const publishButton = el('button', {
        type: 'button',
        className: 'button button-secondary button-small',
        'data-action': 'publish',
        'aria-label': place.isPublished ? S.unpublishPlace(place.name) : S.publishPlace(place.name),
        onClick: () => onTogglePublish(place, publishButton),
    }, [place.isPublished ? S.unpublish : S.publish]);

    return el('tr', { 'data-row': String(place.id) }, [
        el('th', { scope: 'row', 'data-label': S.colName }, [
            el('a', { href: `#/places/${place.id}`, text: place.name }),
        ]),
        el('td', { 'data-label': S.colCategory, text: place.category }),
        el('td', { 'data-label': S.colDistance, text: formatKm(place.distanceKm) }),
        el('td', { 'data-label': S.colStatus }, [statusBadge(place.isPublished)]),
        el('td', {
            'data-label': S.colUpdated,
            text: S.updatedBy(formatDateTime(place.updatedAt), place.maintainedBy),
        }),
        el('td', { 'data-label': S.colActions }, [
            el('div', { className: 'row-actions' }, [
                el('a', {
                    className: 'button button-secondary button-small',
                    href: `#/places/${place.id}`,
                    'aria-label': S.editPlace(place.name),
                }, [S.edit]),
                publishButton,
                el('button', {
                    type: 'button',
                    className: 'button button-danger-outline button-small',
                    'aria-label': S.deletePlace(place.name),
                    onClick: () => onDelete(place),
                }, [S.delete]),
            ]),
        ]),
    ]);
}

// FR-47, NFR-25: icon AND text, not colour alone.
export function statusBadge(isPublished) {
    return el('span', { className: `status-badge ${isPublished ? 'is-published' : 'is-unpublished'}` }, [
        el('span', { 'aria-hidden': 'true', text: isPublished ? '✓' : '○' }),
        isPublished ? S.published : S.unpublished,
    ]);
}
