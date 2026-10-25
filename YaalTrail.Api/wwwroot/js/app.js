// Entry point: holds the page state and connects the UI to the API.
import { getCategories, getPlace, getPlaces } from './api.js';
import { STRINGS } from './strings.js';
import {
    renderChips, renderListMessage, renderPlaceList, renderResultCount, updateChips,
} from './catalogue.js';
import { renderDetail, renderDetailError, renderDetailLoading } from './detail.js';

const SEARCH_DELAY_MS = 300;

// Current filter, search and sort chosen by the visitor.
const state = {
    categoryIds: new Set(),
    search: '',
    sort: 'distance',
};

const elements = {
    chips: document.getElementById('category-chips'),
    search: document.getElementById('search-input'),
    sort: document.getElementById('sort-select'),
    planButton: document.getElementById('plan-button'),
    resultCount: document.getElementById('result-count'),
    listMessage: document.getElementById('list-message'),
    placeList: document.getElementById('place-list'),
    dialog: document.getElementById('detail-dialog'),
    detail: document.getElementById('detail-content'),
    toast: document.getElementById('toast'),
};

let latestRequestNo = 0;   // ignore slow responses that arrive after a newer request
let searchTimer = null;
let toastTimer = null;
let focusBeforeDialog = null;

init();

async function init() {
    wireEvents();
    await loadCategories();
    await loadPlaces();
}

function wireEvents() {
    // FR-04: wait until the visitor pauses typing before searching.
    elements.search.addEventListener('input', () => {
        clearTimeout(searchTimer);
        searchTimer = setTimeout(() => {
            state.search = elements.search.value.trim();
            loadPlaces();
        }, SEARCH_DELAY_MS);
    });

    // FR-03
    elements.sort.addEventListener('change', () => {
        state.sort = elements.sort.value;
        loadPlaces();
    });

    elements.planButton.addEventListener('click', () => showToast(STRINGS.planComingSoon));

    // Close the dialog when the dark backdrop is clicked.
    elements.dialog.addEventListener('click', (event) => {
        if (event.target === elements.dialog) closeDetail();
    });

    // Runs for the close button, Escape key and backdrop click.
    elements.dialog.addEventListener('close', () => {
        document.body.classList.remove('modal-open');
        focusBeforeDialog?.focus();
    });
}

// ---------- Catalogue ----------

async function loadCategories() {
    try {
        const categories = await getCategories();
        renderChips(elements.chips, categories, toggleCategory);
        updateChips(elements.chips, state.categoryIds);
    } catch {
        elements.chips.textContent = STRINGS.categoriesFailed;
    }
}

// id = null means the "All" chip.
function toggleCategory(id) {
    if (id === null) {
        state.categoryIds.clear();
    } else if (state.categoryIds.has(id)) {
        state.categoryIds.delete(id);
    } else {
        state.categoryIds.add(id);
    }

    updateChips(elements.chips, state.categoryIds);
    loadPlaces();
}

async function loadPlaces() {
    const requestNo = ++latestRequestNo;
    setListBusy(true);

    try {
        const result = await getPlaces({
            categoryIds: [...state.categoryIds],
            search: state.search,
            sort: state.sort,
        });
        if (requestNo !== latestRequestNo) return;

        renderResultCount(elements.resultCount, result.count);
        renderPlaceList(elements.placeList, result.items, openPlace);

        // FR-06
        if (result.count === 0) {
            renderListMessage(elements.listMessage, STRINGS.noResults, { label: STRINGS.clearFilters, onClick: clearFilters });
        } else {
            renderListMessage(elements.listMessage, null);
        }
    } catch {
        if (requestNo !== latestRequestNo) return;

        renderResultCount(elements.resultCount, null);
        elements.placeList.replaceChildren();
        renderListMessage(elements.listMessage, STRINGS.placesFailed, { label: STRINGS.retry, onClick: loadPlaces });
    } finally {
        if (requestNo === latestRequestNo) setListBusy(false);
    }
}

function clearFilters() {
    state.categoryIds.clear();
    state.search = '';
    elements.search.value = '';
    updateChips(elements.chips, state.categoryIds);
    loadPlaces();
}

function setListBusy(isBusy) {
    elements.placeList.setAttribute('aria-busy', String(isBusy));
    elements.placeList.classList.toggle('is-loading', isBusy);
    if (isBusy && elements.placeList.children.length === 0) {
        elements.resultCount.textContent = STRINGS.loadingPlaces;
    }
}

// ---------- Detail dialog ----------

async function openPlace(id, triggerElement) {
    focusBeforeDialog = triggerElement;
    renderDetailLoading(elements.detail, closeDetail);

    if (!elements.dialog.open) {
        elements.dialog.showModal();
        document.body.classList.add('modal-open');
    }

    try {
        const place = await getPlace(id);
        renderDetail(elements.detail, place, { onClose: closeDetail, onAddToPlan: addToPlan });
    } catch {
        renderDetailError(elements.detail, closeDetail);
    }

    // The old close button was replaced, so move focus to the new one.
    elements.detail.querySelector('.icon-button')?.focus();
}

function closeDetail() {
    elements.dialog.close();
}

// FR-21 placeholder: the planner will be added later.
function addToPlan(place, statusElement) {
    statusElement.textContent = STRINGS.planComingSoon;
}

// ---------- Toast ----------

function showToast(message) {
    elements.toast.textContent = message;
    elements.toast.classList.add('is-visible');

    clearTimeout(toastTimer);
    toastTimer = setTimeout(() => elements.toast.classList.remove('is-visible'), 3000);
}
