// Day-plan panel: side panel on wide screens, drawer on narrow ones (SRS 4.4, 4.5).
// After every change the plan is sent to POST api/plan/calculate and redrawn.
import { ApiError, calculatePlan } from './api.js';
import { el } from './dom.js';
import { STRINGS } from './strings.js';
import { categoryClass, formatDate, formatDistance, formatMinutes, todayIsoInColombo } from './format.js';
import { MAX_VISIT_MINUTES } from './plan.js';

// PlanService's message for places that were deleted or unpublished since they were added.
const UNAVAILABLE_PREFIX = 'Unknown or unpublished place id(s):';

export function createPlanPanel({ root, store, onClose, onShowPlace }) {
    let latestRequestNo = 0;
    let lastResult = null;   // last plan from the API, kept on screen while the next one loads
    let draggedId = null;

    const parts = buildFrame();
    root.replaceChildren(parts.header, parts.settings, parts.status, parts.body, parts.footer, buildHelp());

    // ---------- Static frame (built once, so the inputs keep focus) ----------

    function buildFrame() {
        const title = el('h2', { id: 'plan-title', className: 'plan-title', tabindex: '-1', text: STRINGS.planTitle });
        const summary = el('p', { className: 'plan-summary muted' });
        const header = el('header', { className: 'plan-header' }, [
            el('div', {}, [title, summary]),
            el('button', {
                type: 'button',
                className: 'icon-button plan-close',
                'aria-label': STRINGS.closePlan,
                onClick: onClose,
            }, [el('span', { 'aria-hidden': 'true', text: '✕' })]),
        ]);

        // BR-03: one calendar day, from today on.
        const dateInput = el('input', {
            id: 'plan-date', type: 'date', min: todayIsoInColombo(), required: true,
            onChange: () => store.setPlanDate(dateInput.value),
        });
        // FR-23: default 08:00.
        const timeInput = el('input', {
            id: 'plan-start', type: 'time', required: true,
            onChange: () => store.setStartTime(timeInput.value),
        });
        const settings = el('div', { className: 'plan-settings' }, [
            el('div', { className: 'plan-field' }, [el('label', { for: 'plan-date', text: STRINGS.planDate }), dateInput]),
            el('div', { className: 'plan-field' }, [el('label', { for: 'plan-start', text: STRINGS.startTime }), timeInput]),
            el('p', { className: 'plan-hint muted', text: STRINGS.planSettingsHint }),
        ]);

        const status = el('p', { className: 'plan-status', role: 'status', 'aria-live': 'polite' });
        const body = el('div', { className: 'plan-body' });

        // FR-34
        const clearButton = el('button', {
            type: 'button',
            className: 'button button-secondary button-danger',
            onClick: confirmClear,
        }, [STRINGS.clearPlan]);
        const footer = el('div', { className: 'plan-footer' }, [clearButton]);

        return { title, summary, header, dateInput, timeInput, settings, status, body, footer, clearButton };
    }

    // SRS 2.6: short help on how the planner works.
    function buildHelp() {
        return el('details', { className: 'plan-help' }, [
            el('summary', { text: STRINGS.planHelpTitle }),
            el('ul', {}, STRINGS.planHelp.map((text) => el('li', { text }))),
        ]);
    }

    // ---------- Refresh after every change ----------

    async function refresh() {
        const plan = store.get();
        updateFrame(plan);

        if (plan.items.length === 0) {
            latestRequestNo++; // ignore any request still on its way
            lastResult = null;
            setStatus('');
            renderBody([el('p', { className: 'plan-empty', text: STRINGS.planEmpty })]);
            return;
        }

        const requestNo = ++latestRequestNo;
        setBusy(true);
        setStatus(lastResult ? STRINGS.planUpdating : STRINGS.planCalculating);
        if (!lastResult) renderBody([]);

        try {
            const result = await calculatePlan(store.toRequest());
            if (requestNo !== latestRequestNo) return;
            lastResult = result;
            renderResult(result);
            setStatus(STRINGS.planUpdated);
        } catch (error) {
            if (requestNo !== latestRequestNo) return;
            lastResult = null;
            renderError(error);
            setStatus('');
        } finally {
            if (requestNo === latestRequestNo) setBusy(false);
        }
    }

    function updateFrame(plan) {
        parts.summary.textContent = STRINGS.planSummary(plan.items.length);
        if (parts.dateInput.value !== plan.planDate) parts.dateInput.value = plan.planDate;
        if (parts.timeInput.value !== plan.startTime) parts.timeInput.value = plan.startTime;
        parts.footer.hidden = plan.items.length === 0;
    }

    function setBusy(isBusy) {
        parts.body.setAttribute('aria-busy', String(isBusy));
        parts.body.classList.toggle('is-loading', isBusy);
    }

    function setStatus(text) {
        parts.status.textContent = text;
    }

    // Replaces the body but puts keyboard focus back on the same control
    // (e.g. the "move earlier" button that was just pressed).
    function renderBody(children) {
        const active = document.activeElement;
        const hadFocus = parts.body.contains(active);
        const focusKey = hadFocus ? active.dataset.focusKey : undefined;

        // replaceChildren turns null/undefined into the text "null", so drop them first
        // (el() already does this for its own children).
        parts.body.replaceChildren(...children.filter((child) => child !== null && child !== undefined && child !== false));
        if (!hadFocus) return;

        // A stop moved to the top or bottom has its pressed arrow disabled; use the other one.
        const keys = focusKey ? [focusKey, swapArrowKey(focusKey)] : [];
        const target = keys
            .map((key) => parts.body.querySelector(`[data-focus-key="${key}"]`))
            .find((node) => node && !node.disabled);
        (target ?? parts.title).focus();
    }

    // ---------- Result ----------

    function renderResult(result) {
        const hasWarnings = result.stops.some((stop) => stop.warnings.length > 0);
        renderBody([
            el('p', {
                className: 'plan-day',
                text: STRINGS.planDayLine(result.day, formatDate(result.planDate), result.startTime),
            }),
            result.advisory ? createNotice(result.advisory) : null,                       // FR-31
            result.totals.unscheduledCount > 0                                             // NFR-10
                ? createNotice(`${STRINGS.unscheduledSummary(result.totals.unscheduledCount)} ${STRINGS.notScheduledHint}`)
                : null,
            createOrderBar(result),
            hasWarnings ? createWarningsHelp() : null,
            createStopList(result.stops),
            createReturnLeg(result.returnLeg),
            createTotals(result.totals),
        ]);
    }

    // FR-24, FR-25
    function createOrderBar(result) {
        return el('div', { className: 'plan-order' }, [
            el('p', {}, [
                el('span', { text: result.isManualOrder ? STRINGS.orderManual : STRINGS.orderAuto }),
                ' ',
                el('span', { className: 'muted', text: STRINGS.reorderHint }),
            ]),
            result.isManualOrder
                ? el('button', {
                    type: 'button',
                    className: 'button button-secondary button-small',
                    title: STRINGS.autoOrderHint,
                    'data-focus-key': 'auto-order',
                    onClick: () => {
                        store.autoOrder();
                        parts.title.focus();
                    },
                }, [STRINGS.autoOrder])
                : null,
        ]);
    }

    // FR-28
    function createStopList(stops) {
        const shownOrder = stops.map((stop) => stop.placeId);
        return el('ol', { className: 'plan-stops' },
            stops.map((stop, index) => createStop(stop, index, stops.length, shownOrder)));
    }

    function createStop(stop, index, count, shownOrder) {
        const item = el('li', {
            className: `plan-stop ${categoryClass(stop.category)}${stop.isScheduled ? '' : ' is-unscheduled'}`,
            'data-place-id': String(stop.placeId),
        }, [
            // Only the heading is draggable, so text in the minutes box can still be selected.
            el('div', { className: 'stop-head', draggable: 'true' }, [
                el('span', { className: 'stop-number', 'aria-hidden': 'true', text: String(stop.order) }),
                el('div', { className: 'stop-title' }, [
                    el('span', { className: 'visually-hidden', text: `${STRINGS.stopNumber(stop.order)}: ` }),
                    el('button', {
                        type: 'button',
                        className: 'link-button',
                        onClick: (event) => onShowPlace(stop.placeId, event.currentTarget),
                    }, [stop.name]),
                    el('span', { className: 'tag', text: stop.category }),
                ]),
            ]),
            createStopTime(stop),
            el('p', { className: 'stop-travel muted', text: travelText(stop, index) }),
            createDurationField(stop),
            createWarnings(stop.warnings),
            createStopActions(stop, index, count, shownOrder),
        ]);

        addDragHandlers(item, stop.placeId, shownOrder);
        return item;
    }

    // NFR-10: unscheduled stops show "Not scheduled" and the estimated arrival
    // taken from the server's warning text.
    function createStopTime(stop) {
        // Arrival and departure are null for unscheduled stops (NFR-10), never print them then.
        if (stop.isScheduled && stop.arrival && stop.departure) {
            return el('p', { className: 'stop-time', text: STRINGS.arriveLeave(stop.arrival, stop.departure) });
        }

        const warning = stop.warnings.find((w) => w.type === 'NotScheduled');
        const estimate = warning?.text.match(/\d{2}:\d{2}(?: \(next day\))?/)?.[0];
        return el('p', { className: 'stop-time' }, [
            el('strong', { className: 'stop-unscheduled' }, [
                el('span', { 'aria-hidden': 'true', text: '! ' }),
                STRINGS.notScheduled,
            ]),
            estimate ? el('span', { className: 'muted', text: ` · ${STRINGS.estimatedArrival(estimate)}` }) : null,
        ]);
    }

    function travelText(stop, index) {
        const km = formatDistance(stop.distanceFromPreviousKm);
        const time = formatMinutes(stop.travelMinutesFromPrevious);
        return index === 0 ? STRINGS.travelFromStart(km, time) : STRINGS.travelFromPrevious(km, time);
    }

    // FR-27: the visitor can change how long they stay. Empty = suggested time.
    function createDurationField(stop) {
        const inputId = `visit-${stop.placeId}`;
        const hintId = `${inputId}-hint`;
        const error = el('p', { className: 'field-error', id: `${inputId}-error`, hidden: true });

        const input = el('input', {
            id: inputId,
            type: 'number',
            inputmode: 'numeric',
            min: '1',
            max: String(MAX_VISIT_MINUTES),
            step: '5',
            value: String(stop.visitMinutes),
            'aria-describedby': hintId,
            'data-focus-key': `visit-${stop.placeId}`,
            onChange: () => {
                const minutes = readMinutes(input.value);
                if (minutes === undefined) {
                    showFieldError(input, error, STRINGS.visitMinutesInvalid(MAX_VISIT_MINUTES));
                    return;
                }
                showFieldError(input, error, null);
                store.setDuration(stop.placeId, minutes);
            },
        });

        return el('div', { className: 'stop-duration' }, [
            el('label', { for: inputId, text: STRINGS.visitMinutes }),
            input,
            el('span', { id: hintId, className: 'muted field-hint', text: STRINGS.visitMinutesHint }),
            error,
        ]);
    }

    // FR-29, FR-30, NFR-25: icon AND text label, plus a hint on how to fix it.
    function createWarnings(warnings) {
        if (warnings.length === 0) return null;

        return el('ul', { className: 'stop-warnings' }, warnings.map((warning) => {
            const info = STRINGS.warnings[warning.type] ?? STRINGS.unknownWarning;
            return el('li', { className: `stop-warning warning-${warning.type}` }, [
                el('span', { className: 'warning-icon', 'aria-hidden': 'true', text: info.icon }),
                el('span', { className: 'warning-text' }, [
                    el('strong', { text: `${info.label}: ` }),
                    warning.text,
                    info.hint ? el('span', { className: 'warning-hint', text: info.hint }) : null,
                ]),
            ]);
        }));
    }

    // FR-22, FR-25
    function createStopActions(stop, index, count, shownOrder) {
        return el('div', { className: 'stop-actions' }, [
            el('button', {
                type: 'button',
                className: 'icon-button icon-button-small',
                'aria-label': STRINGS.moveEarlier(stop.name),
                title: STRINGS.moveEarlier(stop.name),
                'data-focus-key': `up-${stop.placeId}`,
                disabled: index === 0,
                onClick: () => store.move(stop.placeId, -1, shownOrder),
            }, [el('span', { 'aria-hidden': 'true', text: '↑' })]),
            el('button', {
                type: 'button',
                className: 'icon-button icon-button-small',
                'aria-label': STRINGS.moveLater(stop.name),
                title: STRINGS.moveLater(stop.name),
                'data-focus-key': `down-${stop.placeId}`,
                disabled: index === count - 1,
                onClick: () => store.move(stop.placeId, 1, shownOrder),
            }, [el('span', { 'aria-hidden': 'true', text: '↓' })]),
            el('button', {
                type: 'button',
                className: 'button button-secondary button-small',
                'aria-label': STRINGS.removeStop(stop.name),
                onClick: () => {
                    store.remove(stop.placeId);
                    parts.title.focus();
                },
            }, [STRINGS.remove]),
        ]);
    }

    // FR-25: drag and drop for mouse users. The arrow buttons do the same for keyboards.
    function addDragHandlers(item, placeId, shownOrder) {
        const handle = item.querySelector('.stop-head');
        handle.addEventListener('dragstart', (event) => {
            draggedId = placeId;
            event.dataTransfer.effectAllowed = 'move';
            event.dataTransfer.setData('text/plain', String(placeId));
            event.dataTransfer.setDragImage(item, 16, 16);
            item.classList.add('is-dragging');
        });
        handle.addEventListener('dragend', () => {
            draggedId = null;
            item.classList.remove('is-dragging');
        });
        item.addEventListener('dragover', (event) => {
            if (draggedId === null || draggedId === placeId) return;
            event.preventDefault();
            item.classList.add('is-drop-target');
        });
        item.addEventListener('dragleave', () => item.classList.remove('is-drop-target'));
        item.addEventListener('drop', (event) => {
            event.preventDefault();
            item.classList.remove('is-drop-target');
            if (draggedId === null || draggedId === placeId) return;
            store.moveTo(draggedId, shownOrder.indexOf(placeId), shownOrder);
        });
    }

    // FR-24
    function createReturnLeg(returnLeg) {
        return el('p', {
            className: 'plan-return',
            text: STRINGS.returnLeg(returnLeg.arrivalBack, formatDistance(returnLeg.distanceKm), formatMinutes(returnLeg.travelMinutes)),
        });
    }

    // FR-32
    function createTotals(totals) {
        const rows = [
            [STRINGS.totalDistance, formatDistance(totals.totalTravelDistanceKm)],
            [STRINGS.totalTravel, formatMinutes(totals.totalTravelMinutes)],
            [STRINGS.totalVisit, formatMinutes(totals.totalVisitMinutes)],
            [STRINGS.totalElapsed, formatMinutes(totals.totalElapsedMinutes)],
            [STRINGS.endTime, totals.endTime],
        ];
        return el('section', { className: 'plan-totals', 'aria-labelledby': 'plan-totals-title' }, [
            el('h3', { id: 'plan-totals-title', text: STRINGS.totalsTitle }),
            el('dl', { className: 'facts' }, rows.map(([label, value]) =>
                el('div', { className: 'fact' }, [el('dt', { text: label }), el('dd', { text: value })]))),
            el('p', { className: 'muted plan-hint', text: STRINGS.totalsNote }),
        ]);
    }

    // SRS 2.6: what a timing warning means and how to fix each kind.
    function createWarningsHelp() {
        const items = Object.values(STRINGS.warnings).map((info) => el('li', {}, [
            el('span', { className: 'warning-icon', 'aria-hidden': 'true', text: info.icon }),
            el('span', {}, [el('strong', { text: `${info.label}: ` }), info.hint]),
        ]));
        return el('details', { className: 'plan-help plan-help-warnings' }, [
            el('summary', { text: STRINGS.warningsHelpTitle }),
            el('p', { text: STRINGS.warningsHelpIntro }),
            el('ul', { className: 'warning-help-list' }, items),
        ]);
    }

    function createNotice(text) {
        return el('p', { className: 'notice', role: 'note' }, [
            el('span', { className: 'notice-icon', 'aria-hidden': 'true', text: '!' }),
            el('span', { text }),
        ]);
    }

    // ---------- Errors ----------

    function renderError(error) {
        const detail = error instanceof ApiError && error.status === 400 ? error.detail : null;
        const unavailableIds = detail ? parseUnavailableIds(detail) : [];

        const action = unavailableIds.length > 0
            ? { label: STRINGS.planUnavailablePlaces, onClick: () => unavailableIds.forEach((id) => store.remove(id)) }
            : { label: STRINGS.retry, onClick: refresh };

        renderBody([
            el('div', { className: 'list-message', role: 'alert' }, [
                el('p', { text: detail || STRINGS.planFailed }),
                el('button', { type: 'button', className: 'button button-secondary', onClick: action.onClick }, [action.label]),
            ]),
        ]);
    }

    function parseUnavailableIds(detail) {
        if (!detail.startsWith(UNAVAILABLE_PREFIX)) return [];
        return (detail.slice(UNAVAILABLE_PREFIX.length).match(/\d+/g) ?? []).map(Number);
    }

    // ---------- FR-34 ----------

    function confirmClear() {
        if (!window.confirm(STRINGS.clearPlanConfirm)) return;
        store.clear();
        setStatus(STRINGS.planCleared);
        parts.title.focus();
    }

    function focusTitle() {
        parts.title.focus();
    }

    return { refresh, focusTitle };
}

// "up-12" <-> "down-12"
function swapArrowKey(key) {
    if (key.startsWith('up-')) return `down-${key.slice(3)}`;
    if (key.startsWith('down-')) return `up-${key.slice(5)}`;
    return key;
}

// "" -> null (use the suggested time), a valid whole number -> that number, otherwise undefined.
function readMinutes(text) {
    const trimmed = text.trim();
    if (trimmed === '') return null;
    const minutes = Number(trimmed);
    if (!Number.isInteger(minutes) || minutes < 1 || minutes > MAX_VISIT_MINUTES) return undefined;
    return minutes;
}

function showFieldError(input, error, message) {
    error.hidden = !message;
    error.textContent = message ?? '';
    input.setAttribute('aria-invalid', String(Boolean(message)));
    const describedBy = [`${input.id}-hint`, message ? error.id : null].filter(Boolean).join(' ');
    input.setAttribute('aria-describedby', describedBy);
}
