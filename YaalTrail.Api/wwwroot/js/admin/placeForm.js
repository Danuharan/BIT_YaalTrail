// Add / edit a place (FR-39, FR-40) with a 7-day opening-hours editor (FR-44).
// The browser checks the same rules as the server first; the server's answer
// (e.g. FR-42 radius, FR-43 closing time) is always shown as well.
import { el } from '../dom.js';
import { ADMIN_STRINGS as S, DAYS } from './adminStrings.js';
import { createPlace, getCategories, getPlace, updatePlace } from './adminApi.js';
import { errorMessage, formatDateTime } from './adminFormat.js';
import { statusBadge } from './placesView.js';

// Text inputs and their ids. Keys match the request field names (AdminPlaceRequestDto).
const FIELD_IDS = {
    name: 'pf-name',
    categoryId: 'pf-category',
    description: 'pf-description',
    latitude: 'pf-latitude',
    longitude: 'pf-longitude',
    distanceKm: 'pf-distance',
    entryFee: 'pf-fee',
    travelTips: 'pf-tips',
    isPublished: 'pf-published',
};

// placeId = null for a new place. ctx = { navigate(hash, message), showMessage(text, kind) }
export async function showPlaceForm(container, placeId, ctx) {
    const isNew = placeId === null;
    const heading = el('h1', { className: 'screen-title', tabindex: '-1', text: isNew ? S.newPlaceTitle : S.loading });
    // Everything goes into this screen's own root, so a slow answer that arrives after
    // the admin has moved to another screen only changes a detached element.
    const root = el('div', {}, [backLink(), heading]);
    container.replaceChildren(root);
    heading.focus();

    let categories;
    let place = null;
    try {
        [categories, place] = await Promise.all([getCategories(), isNew ? null : getPlace(placeId)]);
    } catch (error) {
        if (error.status === 401) return;
        heading.textContent = S.placesTitle;
        root.append(el('div', { className: 'message message-error', role: 'alert' }, [
            el('p', { text: errorMessage(error) }),
        ]));
        return;
    }

    if (place) heading.textContent = S.editPlaceTitle(place.name);

    const form = buildForm(place, categories);
    if (place) root.append(createMeta(place));
    root.append(form.element);

    form.element.addEventListener('submit', async (event) => {
        event.preventDefault();
        await submit(form, place, ctx);
    });
}

function backLink() {
    return el('a', { className: 'back-link', href: '#/places' }, [
        el('span', { 'aria-hidden': 'true', text: '← ' }), S.backToPlaces,
    ]);
}

// FR-48: when it was created and changed, and by whom.
function createMeta(place) {
    const rows = [
        [S.colStatus, statusBadge(place.isPublished)],
        [S.metaCreated, formatDateTime(place.createdAt)],
        [S.metaUpdated, S.updatedBy(formatDateTime(place.updatedAt), place.maintainedBy)],
        [S.metaSavedPlans, String(place.savedPlanCount)],
    ];
    return el('dl', { className: 'meta' }, rows.map(([label, value]) =>
        el('div', {}, [el('dt', { text: label }), el('dd', {}, [value])])));
}

// ---------- Building the form ----------

function buildForm(place, categories) {
    const summary = el('div', { className: 'error-summary', role: 'alert', tabindex: '-1', hidden: true });
    const saveButton = el('button', { type: 'submit', className: 'button button-primary' }, [S.savePlace]);
    const hours = buildHoursEditor(place?.openingHours ?? []);

    const element = el('form', { className: 'place-form', novalidate: true }, [
        summary,
        el('fieldset', { className: 'form-section' }, [
            el('legend', { text: S.sectionDetails }),
            textField('name', S.fieldName, place?.name, { required: true, maxlength: '200', autocomplete: 'off' }),
            categoryField(place?.categoryId, categories),
            textArea('description', S.fieldDescription, place?.description),
            textArea('travelTips', S.fieldTravelTips, place?.travelTips),
        ]),
        el('fieldset', { className: 'form-section' }, [
            el('legend', { text: S.sectionLocation }),
            el('p', { id: 'pf-coordinates-hint', className: 'hint', text: S.coordinatesHint }),
            el('div', { className: 'field-row' }, [
                textField('latitude', S.fieldLatitude, place?.latitude,
                    { type: 'number', step: 'any', min: '-90', max: '90', required: true, inputmode: 'decimal', hintId: 'pf-coordinates-hint' }),
                textField('longitude', S.fieldLongitude, place?.longitude,
                    { type: 'number', step: 'any', min: '-180', max: '180', required: true, inputmode: 'decimal', hintId: 'pf-coordinates-hint' }),
            ]),
            el('div', { className: 'field-row' }, [
                textField('distanceKm', S.fieldDistance, place?.distanceKm,
                    { type: 'number', step: '0.01', min: '0', inputmode: 'decimal', hint: S.distanceHint }),
                textField('entryFee', S.fieldEntryFee, place?.entryFee ?? 0,
                    { type: 'number', step: '1', min: '0', inputmode: 'decimal', required: true, hint: S.entryFeeHint }),
            ]),
        ]),
        hours.element,
        el('div', { className: 'checkbox-field' }, [
            el('input', { id: FIELD_IDS.isPublished, type: 'checkbox', checked: Boolean(place?.isPublished) }),
            el('label', { for: FIELD_IDS.isPublished, text: S.fieldPublished }),
        ]),
        place ? el('p', { className: 'hint', text: S.photosNote }) : null,
        el('div', { className: 'form-actions' }, [
            saveButton,
            el('a', { className: 'button button-secondary', href: '#/places' }, [S.cancel]),
        ]),
    ]);

    return { element, summary, saveButton, hours };
}

function fieldLabel(text, required) {
    return required
        ? [text, ' ', el('span', { className: 'required', text: S.required })]
        : [text];
}

// options: input attributes plus hint (text shown under the field) or hintId (shared hint).
function textField(key, label, value, options = {}) {
    const { hint, hintId: sharedHintId, ...attributes } = options;
    const id = FIELD_IDS[key];
    const hintId = hint ? `${id}-hint` : sharedHintId;

    return el('div', { className: 'field', 'data-field': key }, [
        el('label', { for: id }, fieldLabel(label, attributes.required)),
        el('input', {
            id,
            type: 'text',
            value: value === null || value === undefined ? null : String(value),
            'aria-describedby': hintId ?? null,
            ...attributes,
        }),
        hint ? el('p', { id: hintId, className: 'hint', text: hint }) : null,
        errorSlot(id),
    ]);
}

function textArea(key, label, value) {
    const id = FIELD_IDS[key];
    return el('div', { className: 'field', 'data-field': key }, [
        el('label', { for: id, text: label }),
        el('textarea', { id, rows: '4', maxlength: '4000', text: value ?? '' }),
        errorSlot(id),
    ]);
}

// BR-05: every place needs a category.
function categoryField(selectedId, categories) {
    const id = FIELD_IDS.categoryId;
    const options = [
        el('option', { value: '', text: S.chooseCategory }),
        ...categories.map((category) => el('option', {
            value: String(category.id),
            selected: category.id === selectedId,
            text: category.name,
        })),
    ];

    return el('div', { className: 'field', 'data-field': 'categoryId' }, [
        el('label', { for: id }, fieldLabel(S.fieldCategory, true)),
        el('select', { id, required: true }, options),
        errorSlot(id),
    ]);
}

function errorSlot(id) {
    return el('p', { id: `${id}-error`, className: 'field-error', hidden: true });
}

// FR-44: one row per day with opening time, closing time and "closed all day".
function buildHoursEditor(openingHours) {
    const rows = DAYS.map((day) => buildHoursRow(day, openingHours.find((h) => h.day === day)));

    const copyButton = el('button', {
        type: 'button',
        className: 'button button-secondary button-small',
        onClick: () => copyFirstRow(rows),
    }, [S.copyMonday]);

    const element = el('fieldset', { className: 'form-section hours-editor', 'data-field': 'openingHours' }, [
        el('legend', { text: S.hoursTitle }),
        el('p', { id: 'pf-hours-hint', className: 'hint', text: S.hoursHint }),
        el('table', { className: 'hours-table' }, [
            el('thead', {}, [el('tr', {}, [S.colDay, S.colOpens, S.colCloses, S.colClosed]
                .map((text) => el('th', { scope: 'col', text })))]),
            el('tbody', {}, rows.map((row) => row.element)),
        ]),
        copyButton,
        errorSlot('pf-hours'),
    ]);

    return { element, rows };
}

function buildHoursRow(day, saved) {
    const open = el('input', {
        id: `pf-open-${day}`, type: 'time', value: saved?.openTime ?? null, 'aria-label': S.opensOn(day),
    });
    const close = el('input', {
        id: `pf-close-${day}`, type: 'time', value: saved?.closeTime ?? null, 'aria-label': S.closesOn(day),
    });
    const closed = el('input', {
        id: `pf-closed-${day}`, type: 'checkbox', checked: Boolean(saved?.isClosed), 'aria-label': S.closedOn(day),
    });

    const syncDisabled = () => {
        open.disabled = closed.checked;
        close.disabled = closed.checked;
    };
    closed.addEventListener('change', syncDisabled);
    syncDisabled();

    const error = el('p', { id: `pf-hours-${day}-error`, className: 'field-error', hidden: true });
    const element = el('tr', { 'data-day': day }, [
        el('th', { scope: 'row', text: day }),
        el('td', { 'data-label': S.colOpens }, [open]),
        el('td', { 'data-label': S.colCloses }, [close]),
        // The visible word repeats the column heading for narrow screens, where headings are hidden;
        // screen readers use the checkbox's aria-label instead.
        el('td', { 'data-label': S.colClosed }, [
            el('label', { className: 'closed-toggle' }, [closed, el('span', { 'aria-hidden': 'true', text: S.colClosed })]),
        ]),
    ]);
    // The row's error message goes in its own full-width row underneath.
    const errorRow = el('tr', { className: 'hours-error-row', hidden: true }, [
        el('td', { colspan: '4' }, [error]),
    ]);

    return { day, open, close, closed, error, errorRow, element, syncDisabled };
}

function copyFirstRow(rows) {
    const [first, ...others] = rows;
    for (const row of others) {
        row.open.value = first.open.value;
        row.close.value = first.close.value;
        row.closed.checked = first.closed.checked;
        row.syncDisabled();
    }
}

// ---------- Reading and checking the form ----------

function readForm(form) {
    const value = (key) => document.getElementById(FIELD_IDS[key]).value.trim();
    const number = (key) => (value(key) === '' ? null : Number(value(key)));

    return {
        name: value('name'),
        categoryId: Number(value('categoryId')) || 0,
        description: value('description') || null,
        latitude: number('latitude'),
        longitude: number('longitude'),
        distanceKm: number('distanceKm'),
        entryFee: number('entryFee'),
        travelTips: value('travelTips') || null,
        isPublished: document.getElementById(FIELD_IDS.isPublished).checked,
        // A day with no times and not closed is left out: "hours not recorded" (FR-12).
        openingHours: form.hours.rows
            .filter((row) => row.closed.checked || row.open.value || row.close.value)
            .map((row) => ({
                day: row.day,
                openTime: row.closed.checked ? null : row.open.value || null,
                closeTime: row.closed.checked ? null : row.close.value || null,
                isClosed: row.closed.checked,
            })),
    };
}

// The same rules the server applies, so most mistakes are caught before sending.
// Returns [{ key, day?, message }].
function validate(request) {
    const errors = [];
    const inRange = (n, min, max) => n !== null && Number.isFinite(n) && n >= min && n <= max;

    if (!request.name) errors.push({ key: 'name', message: S.errNameRequired });
    if (!request.categoryId) errors.push({ key: 'categoryId', message: S.errCategoryRequired });
    if (!inRange(request.latitude, -90, 90)) errors.push({ key: 'latitude', message: S.errLatitude });
    if (!inRange(request.longitude, -180, 180)) errors.push({ key: 'longitude', message: S.errLongitude });
    if (request.distanceKm !== null && !inRange(request.distanceKm, 0, 9999)) errors.push({ key: 'distanceKm', message: S.errDistance });
    if (!inRange(request.entryFee, 0, 99_999_999)) errors.push({ key: 'entryFee', message: S.errEntryFee });

    for (const hours of request.openingHours) {
        if (hours.isClosed) continue;
        if (!hours.openTime || !hours.closeTime) {
            errors.push({ key: 'openingHours', day: hours.day, message: S.errTimesMissing(hours.day) });
        } else if (hours.closeTime <= hours.openTime) {
            // FR-43. "HH:mm" strings compare correctly as text.
            errors.push({ key: 'openingHours', day: hours.day, message: S.errCloseBeforeOpen(hours.day) });
        }
    }
    return errors;
}

// ---------- Saving ----------

async function submit(form, place, ctx) {
    const request = readForm(form);
    clearErrors(form);

    const clientErrors = validate(request);
    if (clientErrors.length > 0) {
        showErrors(form, clientErrors);
        return;
    }

    form.saveButton.disabled = true;
    form.saveButton.textContent = S.saving;
    try {
        const saved = place ? await updatePlace(place.id, request) : await createPlace(request);
        ctx.navigate('#/places', place ? S.placeSaved(saved.name) : S.placeCreated(saved.name));
    } catch (error) {
        if (error.status === 401) return;
        form.saveButton.disabled = false;
        form.saveButton.textContent = S.savePlace;

        const serverErrors = error.status === 400 ? serverErrorList(error) : [];
        if (serverErrors.length > 0) {
            showErrors(form, serverErrors);
        } else {
            ctx.showMessage(errorMessage(error), 'error');
        }
    }
}

// Turns the server's 400 answer into [{ key, day?, message }].
// ASP.NET model validation gives field names; the admin service gives a list of
// sentences, which are matched to a field by their wording.
function serverErrorList(error) {
    const list = [];

    for (const [key, messages] of Object.entries(error.fieldErrors)) {
        for (const message of messages) list.push({ key: key in FIELD_IDS || key === 'openingHours' ? key : null, message });
    }
    for (const message of error.messages) list.push(matchMessage(message));

    if (list.length === 0 && error.detail) list.push({ key: null, message: error.detail });
    return list;
}

function matchMessage(message) {
    const day = DAYS.find((d) => message.startsWith(d) || message.startsWith(`'${d}'`));
    if (day) return { key: 'openingHours', day, message };
    if (/coordinates/i.test(message)) return { key: 'latitude', also: 'longitude', message };  // FR-42
    if (/category/i.test(message)) return { key: 'categoryId', message };                      // BR-05
    if (/^name/i.test(message)) return { key: 'name', message };
    if (/day of the week|more than once/i.test(message)) return { key: 'openingHours', message };
    return { key: null, message };
}

// ---------- Showing errors ----------

function clearErrors(form) {
    form.summary.hidden = true;
    form.summary.replaceChildren();
    for (const node of form.element.querySelectorAll('.field-error')) {
        node.hidden = true;
        node.textContent = '';
    }
    for (const row of form.hours.rows) row.errorRow.remove();
    for (const node of form.element.querySelectorAll('[aria-invalid="true"]')) {
        node.removeAttribute('aria-invalid');
        resetDescribedBy(node);
    }
}

// Each error goes next to its field, and every error is listed in the summary
// with a link that moves focus to the field.
function showErrors(form, errors) {
    const links = errors.map((error) => {
        const target = markField(form, error);
        return el('li', {}, [target
            ? el('a', { href: `#${target.id}`, onClick: (event) => { event.preventDefault(); target.focus(); } }, [error.message])
            : error.message]);
    });

    form.summary.replaceChildren(
        el('h2', { text: S.errorSummaryTitle(errors.length) }),
        el('ul', {}, links),
    );
    form.summary.hidden = false;
    form.summary.focus();
}

// Shows the message next to the field and returns the control to focus (or null).
function markField(form, error) {
    if (error.key === 'openingHours') return markHours(form, error);

    const id = FIELD_IDS[error.key];
    const input = id ? document.getElementById(id) : null;
    if (!input) return null;

    addFieldMessage(input, document.getElementById(`${id}-error`), error.message);
    if (error.also) {
        const other = document.getElementById(FIELD_IDS[error.also]);
        other?.setAttribute('aria-invalid', 'true');
    }
    return input;
}

function markHours(form, error) {
    const row = form.hours.rows.find((r) => r.day === error.day);
    if (!row) {
        const slot = document.getElementById('pf-hours-error');
        appendMessage(slot, error.message);
        return form.hours.rows[0].open;
    }

    row.element.after(row.errorRow);
    row.errorRow.hidden = false;
    appendMessage(row.error, error.message);
    const input = row.open.disabled ? row.closed : row.open;
    for (const control of [row.open, row.close]) {
        control.setAttribute('aria-invalid', 'true');
        addDescribedBy(control, row.error.id);
    }
    return input;
}

function addFieldMessage(input, slot, message) {
    appendMessage(slot, message);
    input.setAttribute('aria-invalid', 'true');
    addDescribedBy(input, slot.id);
}

function appendMessage(slot, message) {
    slot.textContent = slot.textContent ? `${slot.textContent} ${message}` : message;
    slot.hidden = false;
}

function addDescribedBy(input, id) {
    if (!input.dataset.describedBy) input.dataset.describedBy = input.getAttribute('aria-describedby') ?? '';
    const ids = new Set((input.getAttribute('aria-describedby') ?? '').split(' ').filter(Boolean));
    ids.add(id);
    input.setAttribute('aria-describedby', [...ids].join(' '));
}

function resetDescribedBy(input) {
    if (input.dataset.describedBy === undefined) return;
    if (input.dataset.describedBy) input.setAttribute('aria-describedby', input.dataset.describedBy);
    else input.removeAttribute('aria-describedby');
    delete input.dataset.describedBy;
}
