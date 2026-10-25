// "Add to day plan" / "Remove from day plan" button used on cards, the detail
// view and map popups (FR-21, FR-22). plan = { has(id), toggle(place) }.
import { el } from './dom.js';
import { STRINGS } from './strings.js';

export function createPlanToggle(place, plan, extraClass = '') {
    const button = el('button', {
        type: 'button',
        className: `button plan-toggle ${extraClass}`.trim(),
        'data-plan-id': String(place.id),
        onClick: () => plan.toggle(place),
    });
    updatePlanToggle(button, plan.has(place.id));
    return button;
}

// After the plan changes, update every toggle inside root (e.g. the whole page).
export function syncPlanToggles(root, has) {
    for (const button of root.querySelectorAll('[data-plan-id]')) {
        updatePlanToggle(button, has(Number(button.dataset.planId)));
    }
}

// The label says what the button will do, and the icon + style show the
// current state, so it is not shown by colour alone (NFR-25).
function updatePlanToggle(button, inPlan) {
    button.classList.toggle('button-primary', !inPlan);
    button.classList.toggle('button-secondary', inPlan);
    button.classList.toggle('is-in-plan', inPlan);
    button.replaceChildren(
        el('span', { className: 'plan-toggle-icon', 'aria-hidden': 'true', text: inPlan ? '✓' : '+' }),
        el('span', { text: inPlan ? STRINGS.removeFromPlan : STRINGS.addToPlan }),
    );
}
