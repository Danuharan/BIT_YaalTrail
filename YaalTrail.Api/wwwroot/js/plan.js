// Day-plan state, kept only in this browser (FR-21 to FR-27, FR-33, FR-34, NFR-16).
// The server calculates times from this state but never stores it.
import { todayIsoInColombo } from './format.js';

const STORAGE_KEY = 'yaaltrail.dayPlan.v1';

export const DEFAULT_START_TIME = '08:00'; // FR-23
export const MAX_VISIT_MINUTES = 720;      // same limit as PlanBuilderService

const TIME_PATTERN = /^\d{2}:\d{2}$/;
const DATE_PATTERN = /^\d{4}-\d{2}-\d{2}$/;

// plan = {
//   items:        [{ id, name, category }]   places the visitor added (FR-21)
//   startTime:    "HH:mm"                     (FR-23)
//   planDate:     "yyyy-MM-dd"                (BR-03: one calendar day)
//   manualOrder:  [placeId] or null           null = nearest-neighbour (FR-24, FR-25)
//   durations:    { placeId: minutes }        the visitor's own visit times (FR-27)
// }
// onChange is called after every change, once the plan has been saved.
export function createPlanStore(onChange) {
    let plan = loadPlan() ?? emptyPlan();

    function commit() {
        savePlan(plan);
        onChange(plan);
    }

    const has = (id) => plan.items.some((item) => item.id === id);

    // FR-21
    function add(place) {
        if (has(place.id)) return;
        plan.items.push({ id: place.id, name: place.name, category: place.category });
        if (plan.manualOrder) plan.manualOrder.push(place.id);
        commit();
    }

    // FR-22
    function remove(id) {
        if (!has(id)) return;
        plan.items = plan.items.filter((item) => item.id !== id);
        if (plan.manualOrder) plan.manualOrder = plan.manualOrder.filter((placeId) => placeId !== id);
        delete plan.durations[id];
        commit();
    }

    // FR-34: keeps the chosen day, everything else goes back to the defaults.
    function clear() {
        plan = { ...emptyPlan(), planDate: plan.planDate };
        commit();
    }

    function setStartTime(time) {
        if (!TIME_PATTERN.test(time) || time === plan.startTime) return;
        plan.startTime = time;
        commit();
    }

    function setPlanDate(date) {
        if (!DATE_PATTERN.test(date) || date === plan.planDate) return;
        plan.planDate = date;
        commit();
    }

    // FR-27: minutes = null goes back to the suggested time for the category.
    function setDuration(id, minutes) {
        if (minutes === null) {
            if (!(id in plan.durations)) return;
            delete plan.durations[id];
        } else {
            if (plan.durations[id] === minutes) return;
            plan.durations[id] = minutes;
        }
        commit();
    }

    // FR-25: move one place to a new position. shownOrder is the order on screen
    // (the last calculated plan), which is the starting point for the first manual move.
    function moveTo(id, newIndex, shownOrder) {
        const order = currentOrder(shownOrder).filter((placeId) => placeId !== id);
        const index = Math.max(0, Math.min(newIndex, order.length));
        order.splice(index, 0, id);
        plan.manualOrder = order;
        commit();
    }

    function move(id, delta, shownOrder) {
        const index = currentOrder(shownOrder).indexOf(id);
        if (index < 0) return;
        moveTo(id, index + delta, shownOrder);
    }

    // FR-24: back to nearest-neighbour.
    function autoOrder() {
        if (!plan.manualOrder) return;
        plan.manualOrder = null;
        commit();
    }

    // The manual order if there is one, otherwise the order on screen.
    // Places added since the last calculation go at the end.
    function currentOrder(shownOrder) {
        const ids = plan.items.map((item) => item.id);
        const base = (plan.manualOrder ?? shownOrder ?? []).filter((id) => ids.includes(id));
        return [...base, ...ids.filter((id) => !base.includes(id))];
    }

    // Body for POST api/plan/calculate (names match PlanRequestDto).
    function toRequest() {
        return {
            startTime: plan.startTime,
            planDate: plan.planDate,
            placeIds: plan.items.map((item) => item.id),
            manualOrder: plan.manualOrder,
            durationOverrides: { ...plan.durations },
        };
    }

    return {
        get: () => plan,
        has,
        add,
        remove,
        clear,
        setStartTime,
        setPlanDate,
        setDuration,
        move,
        moveTo,
        autoOrder,
        toRequest,
    };
}

function emptyPlan() {
    return {
        items: [],
        startTime: DEFAULT_START_TIME,
        planDate: todayIsoInColombo(),
        manualOrder: null,
        durations: {},
    };
}

// ---------- FR-33: localStorage ----------
// Storage can be full, blocked or turned off (private windows), so every access
// is wrapped in try/catch and the planner still works without it.

function savePlan(plan) {
    try {
        localStorage.setItem(STORAGE_KEY, JSON.stringify(plan));
    } catch {
        // Not saved; the plan still works until the page is closed.
    }
}

function loadPlan() {
    let saved;
    try {
        saved = JSON.parse(localStorage.getItem(STORAGE_KEY));
    } catch {
        return null;
    }
    return cleanPlan(saved);
}

// Anything odd in the saved data is dropped instead of breaking the page.
function cleanPlan(saved) {
    if (!saved || !Array.isArray(saved.items)) return null;

    const items = [];
    for (const item of saved.items) {
        const valid = Number.isInteger(item?.id) && item.id > 0 && typeof item.name === 'string';
        if (valid && !items.some((other) => other.id === item.id)) {
            items.push({ id: item.id, name: item.name, category: String(item.category ?? '') });
        }
    }
    const ids = items.map((item) => item.id);

    // A plan saved for a day that has passed moves to today.
    const today = todayIsoInColombo();
    const planDate = DATE_PATTERN.test(saved.planDate) && saved.planDate >= today ? saved.planDate : today;

    return {
        items,
        startTime: TIME_PATTERN.test(saved.startTime) ? saved.startTime : DEFAULT_START_TIME,
        planDate,
        manualOrder: cleanManualOrder(saved.manualOrder, ids),
        durations: cleanDurations(saved.durations, ids),
    };
}

// The server needs every place exactly once (FR-25), otherwise use auto-order.
function cleanManualOrder(order, ids) {
    if (!Array.isArray(order) || order.length !== ids.length) return null;
    const unique = new Set(order);
    return unique.size === ids.length && ids.every((id) => unique.has(id)) ? [...order] : null;
}

function cleanDurations(durations, ids) {
    const result = {};
    if (!durations || typeof durations !== 'object') return result;
    for (const id of ids) {
        const minutes = durations[id];
        if (Number.isInteger(minutes) && minutes >= 1 && minutes <= MAX_VISIT_MINUTES) result[id] = minutes;
    }
    return result;
}
