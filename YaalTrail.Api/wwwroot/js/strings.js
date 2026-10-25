// All interface text in one place so Tamil/Sinhala can be added later (SRS 6.3).
export const STRINGS = {
    allCategories: 'All',
    resultCount: (n) => (n === 1 ? '1 place' : `${n} places`),
    loadingPlaces: 'Loading places…',

    noResults: 'No places match your filters or search. Try a different keyword or clear the filters.',
    clearFilters: 'Clear filters and search',
    placesFailed: 'Places could not be loaded. Check your connection and try again.',
    retry: 'Try again',
    categoriesFailed: 'Categories could not be loaded. You can still browse all places.',

    distanceFromReference: (km) => `${km} from Chundikuli`,

    loading: 'Loading…',
    placeUnavailable: 'Place unavailable',
    detailFailed: 'This place could not be loaded. It may no longer be available. Close this window and try again.',
    closeDetails: 'Close details',

    openNow: 'Open now',
    closedNow: 'Closed now',
    today: 'Today',
    todayMarker: 'Today',
    closed: 'Closed',
    open24h: 'Open 24 hours',

    distance: 'Distance',
    entryFee: 'Entry fee',
    coordinates: 'Coordinates',
    lastUpdated: 'Last updated',
    about: 'About',
    travelTips: 'Travel tips',
    openingHours: 'Opening hours',
    day: 'Day',
    hours: 'Hours',
    photoOf: (name) => `Photo of ${name}`,

    // NFR-07
    indicativeNotice: 'Opening hours and entry fees are indicative; please verify before travelling.',

    addToPlan: 'Add to day plan',
    planComingSoon: 'The day planner is coming soon.',

    // FR-14 to FR-20: map view
    mapLabel: 'Map of places around Chundikuli',
    referenceLocation: 'Chundikuli (reference location)',
    referencePopup: 'Day plans start and end here.',
    viewDetails: 'View details',
    directions: 'Directions',
    opensNewTab: '(opens in a new tab)',
    tilesFailed: 'Map tiles could not be loaded. Switch to List to keep browsing places.',
    mapLibraryFailed: 'The map could not be loaded. Switch to List to keep browsing places.',
    referenceMissing: 'The reference location could not be loaded, so the map is not centred on Chundikuli. Switch to List to keep browsing places.',
    showList: 'Show list',
};
