// Interactive map (FR-14 to FR-20, NFR-08, NFR-24).
// Tiles are loaded by the browser straight from OpenStreetMap, not through the API.
import { el } from './dom.js';
import { STRINGS } from './strings.js';
import { categoryClass, formatDistance } from './format.js';

// FR-15: centred on the reference location. Zoom 11 shows greater Jaffna around that point.
const FIRST_ZOOM = 11;

const POPUP_OPTIONS = { maxWidth: 260, minWidth: 200, autoPan: true, keepInView: true };

// Builds the map the first time the visitor opens it, then updates markers in place.
export function createPlaceMap({
    canvas, legend, notice, onViewDetails, onAddToPlan, onShowList, isDialogOpen,
}) {
    let L = null;
    let map = null;
    let placeLayer = null;
    let referenceMarker = null;
    let centered = false;
    let unavailable = false;

    let categories = [];
    let places = [];
    let reference = undefined;

    const notices = new Map();

    function setCategories(next) {
        categories = Array.isArray(next) ? next : [];
        renderLegend();
    }

    // undefined = settings not loaded yet. null = they failed to load.
    function setReference(next) {
        if (next === undefined) return;
        reference = next;
        if (!referencePoint(reference)) setNotice('reference', STRINGS.referenceMissing);
        refresh();
    }

    function setPlaces(next) {
        places = Array.isArray(next) ? next : [];
        refresh();
    }

    function invalidateSize() {
        map?.invalidateSize();
    }

    function refresh() {
        renderLegend();
        if (!ensureMap()) return;

        drawReferenceMarker();
        drawPlaces();

        // Centre only the first time we know the reference location (FR-15).
        // Later filter changes update the markers and leave the visitor's pan and zoom alone.
        const point = referencePoint(reference);
        if (!point) return;
        if (!centered) {
            map.setView(point, FIRST_ZOOM);
            centered = true;
        }
        setNotice('reference', null);
    }

    function ensureMap() {
        if (map || unavailable) return map;

        L = window.L;
        if (!L?.map) {
            unavailable = true;
            canvas.hidden = true;
            setNotice('library', STRINGS.mapLibraryFailed);
            return null;
        }

        // Wait for the settings request before falling back to the places themselves.
        if (reference === undefined) return null;

        const center = referencePoint(reference) ?? averagePoint(places);
        if (!center) return null;

        canvas.hidden = false;
        const reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
        map = L.map(canvas, {
            zoomControl: false,
            zoomAnimation: !reduceMotion,
            fadeAnimation: !reduceMotion,
            markerZoomAnimation: !reduceMotion,
            scrollWheelZoom: true,
        });
        // Bottom corner, so the sticky header does not cover the zoom buttons.
        L.control.zoom({ position: 'bottomleft' }).addTo(map);
        map.setView(center, FIRST_ZOOM);
        if (referencePoint(reference)) centered = true;

        // FR-19: the attribution control stays on the map even when a tile fails.
        const tiles = L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19,
            attribution: '&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors',
        });
        // FR-20, NFR-24: a tile failure shows a notice. The list is left untouched.
        tiles.on('tileerror', () => setNotice('tiles', STRINGS.tilesFailed));
        tiles.addTo(map);

        placeLayer = L.layerGroup().addTo(map);

        const container = map.getContainer();
        container.setAttribute('role', 'region');
        container.setAttribute('aria-label', STRINGS.mapLabel);
        return map;
    }

    function drawPlaces() {
        placeLayer.clearLayers();
        for (const place of places) {
            const point = placePoint(place);
            if (!point) continue;
            placeLayer.addLayer(createPlaceMarker(place, point));
        }
    }

    function drawReferenceMarker() {
        const point = referencePoint(reference);
        if (!point) {
            referenceMarker?.remove();
            referenceMarker = null;
            return;
        }

        if (referenceMarker) {
            referenceMarker.setLatLng(point);
            return;
        }

        referenceMarker = L.marker(point, {
            icon: referenceIcon(),
            title: STRINGS.referenceLocation,
            keyboard: true,
            zIndexOffset: 1000,
        });
        const popup = createReferencePopup();
        popup.addEventListener('keydown', (event) => closePopupOnEscape(event, referenceMarker));
        referenceMarker.bindPopup(popup, POPUP_OPTIONS);
        labelMarker(referenceMarker, STRINGS.referenceLocation);
        watchPopupFocus(referenceMarker);
        referenceMarker.addTo(map);
    }

    // FR-16, FR-17, FR-18, FR-21: one marker per place that matches the active filters.
    function createPlaceMarker(place, point) {
        const marker = L.marker(point, {
            icon: placeIcon(place.category),
            title: place.name,
            keyboard: true,
            riseOnHover: true,
        });
        const popup = createPlacePopup(place, point);
        popup.addEventListener('keydown', (event) => closePopupOnEscape(event, marker));
        marker.bindPopup(popup, POPUP_OPTIONS);
        labelMarker(marker, placeLabel(place));
        watchPopupFocus(marker);
        return marker;
    }

    function placeIcon(category) {
        return L.divIcon({
            className: `place-marker ${categoryClass(category)}`,
            // Static markup only. Place names are added as text nodes in the popup (NFR-14).
            html: '<span class="place-marker-dot"></span>',
            iconSize: [28, 28],
            iconAnchor: [14, 14],
            popupAnchor: [0, -16],
        });
    }

    function referenceIcon() {
        return L.divIcon({
            className: 'place-marker marker-reference',
            html: '<span class="ref-marker-shape"></span>',
            iconSize: [28, 28],
            iconAnchor: [14, 14],
            popupAnchor: [0, -16],
        });
    }

    function createPlacePopup(place, point) {
        const status = el('p', { className: 'action-status', role: 'status' });
        return el('div', { className: 'map-popup', tabindex: '-1' }, [
            el('h3', { className: 'popup-title', text: place.name }),
            el('p', {}, [
                el('span', { className: `tag ${categoryClass(place.category)}`, text: place.category }),
            ]),
            el('p', { className: 'muted', text: STRINGS.distanceFromReference(formatDistance(place.distanceKm)) }),
            el('div', { className: 'popup-actions' }, [
                el('button', {
                    type: 'button',
                    className: 'button button-secondary',
                    onClick: (event) => onViewDetails(place.id, event.currentTarget),
                }, [STRINGS.viewDetails]),
                el('button', {
                    type: 'button',
                    className: 'button button-primary',
                    onClick: () => onAddToPlan(place, status),
                }, [STRINGS.addToPlan]),
                // NFR-08: route guidance opens in Google Maps. No turn-by-turn in this page.
                el('a', {
                    className: 'button button-secondary',
                    href: directionsHref(point[0], point[1]),
                    target: '_blank',
                    rel: 'noopener noreferrer',
                }, [
                    STRINGS.directions,
                    el('span', { className: 'visually-hidden', text: ` ${STRINGS.opensNewTab}` }),
                ]),
            ]),
            status,
        ]);
    }

    function createReferencePopup() {
        return el('div', { className: 'map-popup', tabindex: '-1' }, [
            el('h3', { className: 'popup-title', text: STRINGS.referenceLocation }),
            el('p', { text: STRINGS.referencePopup }),
        ]);
    }

    function labelMarker(marker, label) {
        marker.on('add', () => {
            const icon = marker.getElement();
            if (!icon) return;
            icon.setAttribute('role', 'button');
            icon.setAttribute('aria-label', label);
        });
    }

    function watchPopupFocus(marker) {
        marker.on('popupopen', () => {
            marker.getPopup()?.getElement()?.querySelector('.map-popup')?.focus();
        });
        marker.on('popupclose', () => {
            if (isDialogOpen()) return;
            const popup = marker.getPopup()?.getElement();
            if (popup?.contains(document.activeElement)) marker.getElement()?.focus();
        });
    }

    // FR-17: reference location, then every category, using the same colours as the markers.
    function renderLegend() {
        const names = [];
        const seen = new Set();
        for (const category of categories) {
            if (!category?.name || seen.has(category.name)) continue;
            seen.add(category.name);
            names.push(category.name);
        }
        for (const place of places) {
            if (place.category && !seen.has(place.category)) {
                seen.add(place.category);
                names.push(place.category);
            }
        }

        const items = [];
        if (referencePoint(reference)) {
            items.push(legendItem('swatch swatch-reference', STRINGS.referenceLocation));
        }
        for (const name of names) {
            items.push(legendItem(`swatch ${categoryClass(name)}`, name));
        }
        legend.replaceChildren(...items);
    }

    function legendItem(swatchClass, label) {
        return el('li', { className: 'legend-item' }, [
            el('span', { className: swatchClass, 'aria-hidden': 'true' }),
            el('span', { text: label }),
        ]);
    }

    function setNotice(key, text) {
        if (text) {
            if (notices.get(key) === text) return;
            notices.set(key, text);
        } else if (!notices.delete(key)) {
            return;
        }

        const children = [...notices.values()].map((message) => el('p', { text: message }));
        if (children.length > 0) {
            children.push(el('button', {
                type: 'button',
                className: 'button button-secondary',
                onClick: onShowList,
            }, [STRINGS.showList]));
        }
        notice.replaceChildren(...children);
    }

    return { setCategories, setReference, setPlaces, invalidateSize };
}

function closePopupOnEscape(event, marker) {
    if (event.key !== 'Escape') return;
    event.preventDefault();
    event.stopPropagation();
    marker.closePopup();
}

function placeLabel(place) {
    return `${place.name}. ${place.category}. ${STRINGS.distanceFromReference(formatDistance(place.distanceKm))}`;
}

function placePoint(place) {
    return finitePoint(place?.latitude, place?.longitude);
}

// Reads the configured reference point. 0,0 means the Planner section was not set.
function referencePoint(settings) {
    if (!settings) return null;
    const point = finitePoint(settings.referenceLatitude, settings.referenceLongitude);
    if (!point || (point[0] === 0 && point[1] === 0)) return null;
    return point;
}

function finitePoint(latitude, longitude) {
    const lat = Number(latitude);
    const lng = Number(longitude);
    if (!Number.isFinite(lat) || !Number.isFinite(lng)) return null;
    return [lat, lng];
}

function averagePoint(placeList) {
    const points = placeList.map(placePoint).filter(Boolean);
    if (points.length === 0) return null;
    const lat = points.reduce((sum, point) => sum + point[0], 0) / points.length;
    const lng = points.reduce((sum, point) => sum + point[1], 0) / points.length;
    return [lat, lng];
}

// FR-18: destination coordinates only. The link opens in a new tab.
function directionsHref(latitude, longitude) {
    const lat = Number(latitude).toFixed(6);
    const lng = Number(longitude).toFixed(6);
    return `https://www.google.com/maps/dir/?api=1&destination=${lat},${lng}`;
}
