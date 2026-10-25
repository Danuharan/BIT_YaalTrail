// Entry point: holds the page state and connects the UI to the API.
import { getCategories, getPlace, getPlaces, getPlannerSettings } from './api.js';
import { STRINGS } from './strings.js';
import {
    renderChips, renderListMessage, renderPlaceList, renderResultCount, updateChips,
} from './catalogue.js';
import { renderDetail, renderDetailError, renderDetailLoading } from './detail.js';
import { createPlaceMap } from './map.js';

const SEARCH_DELAY_MS = 300;

// Current filter, search, sort and view chosen by the visitor.
// The map reads this same state, so both views show the same places (FR-14).
const state = {
    categoryIds: new Set(),
    search: '',
    sort: 'distance',
    view: 'list',
    categories: [],
    places: [],
    reference: undefined,
};

const elements = {
    chips: document.getElementById('category-chips'),
    search: document.getElementById('search-input'),
    sort: document.getElementById('sort-select'),
    planButton: document.getElementById('plan-button'),
    viewList: document.getElementById('view-list'),
    viewMap: document.getElementById('view-map'),
    resultCount: document.getElementById('result-count'),
    listMessage: document.getElementById('list-message'),
    listView: document.getElementById('list-view'),
    placeList: document.getElementById('place-list'),
    mapView: document.getElementById('map-view'),
    mapCanvas: document.getElementById('map'),
    mapLegend: document.getElementById('map-legend'),
    mapNotice: document.getElementById('map-notice'),
    dialog: document.getElementById('detail-dialog'),
    detail: document.getElementById('detail-content'),
    toast: document.getElementById('toast'),
};

let mapController = null;

let latestRequestNo = 0;   // ignore slow responses that arrive after a newer request
let searchTimer = null;
let toastTimer = null;
let focusBeforeDialog = null;

init();

async function init() {
    wireEvents();
    await Promise.all([loadCategories(), loadSettings()]);
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

    // FR-14
    elements.viewList.addEventListener('click', () => setView('list'));
    elements.viewMap.addEventListener('click', () => setView('map'));

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
        state.categories = await getCategories();
        renderChips(elements.chips, state.categories, toggleCategory);
        updateChips(elements.chips, state.categoryIds);
        if (state.view === 'map') ensureMap().setCategories(state.categories);
    } catch {
        elements.chips.textContent = STRINGS.categoriesFailed;
    }
}

// FR-15: Chundikuli comes from the API, which reads the Planner settings.
async function loadSettings() {
    try {
        state.reference = await getPlannerSettings();
    } catch {
        state.reference = null;
    }

    if (state.view === 'map') {
        const map = ensureMap();
        map.setReference(state.reference);
        map.invalidateSize();
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

        state.places = result.items;
        renderResultCount(elements.resultCount, result.count);
        renderPlaceList(elements.placeList, state.places, openPlace);
        if (state.view === 'map') ensureMap().setPlaces(state.places);

        // FR-06
        if (result.count === 0) {
            renderListMessage(elements.listMessage, STRINGS.noResults, { label: STRINGS.clearFilters, onClick: clearFilters });
        } else {
            renderListMessage(elements.listMessage, null);
        }
    } catch {
        if (requestNo !== latestRequestNo) return;

        state.places = [];
        renderResultCount(elements.resultCount, null);
        elements.placeList.replaceChildren();
        if (state.view === 'map') mapController?.setPlaces([]);
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

// FR-14: List and Map show the same filtered places. The list stays in the page
// when the map is open, so a tile failure never removes it (FR-20, NFR-24).
function setView(view) {
    if (state.view === view) return;
    state.view = view;

    const showMap = view === 'map';
    elements.listView.hidden = showMap;
    elements.mapView.hidden = !showMap;
    elements.viewList.setAttribute('aria-pressed', String(!showMap));
    elements.viewMap.setAttribute('aria-pressed', String(showMap));

    if (!showMap) return;

    const map = ensureMap();
    // The panel has just been shown. Wait a frame so Leaflet can measure it.
    requestAnimationFrame(() => {
        if (state.view !== 'map') return;
        map.setCategories(state.categories);
        map.setReference(state.reference);
        map.setPlaces(state.places);
        map.invalidateSize();
        requestAnimationFrame(() => {
            if (state.view === 'map') map.invalidateSize();
        });
    });
}

function ensureMap() {
    if (!mapController) {
        mapController = createPlaceMap({
            canvas: elements.mapCanvas,
            legend: elements.mapLegend,
            notice: elements.mapNotice,
            onViewDetails: openPlace,
            onAddToPlan: addToPlan,
            onShowList: () => setView('list'),
            isDialogOpen: () => elements.dialog.open,
        });
    }
    return mapController;
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
