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
    galleryLabel: (name) => `Photos of ${name}`,
    showPhoto: (n, total, description) => `Show photo ${n} of ${total}: ${description}`,

    // NFR-07
    indicativeNotice: 'Opening hours and entry fees are indicative; please verify before travelling.',

    // FR-21, FR-22: add / remove buttons on cards, details and map popups
    addToPlan: 'Add to day plan',
    removeFromPlan: 'Remove from day plan',
    addedToPlan: (name, n) => `${name} added to your day plan (${n === 1 ? '1 place' : `${n} places`}).`,
    removedFromPlan: (name) => `${name} removed from your day plan.`,
    viewPlan: 'View day plan',

    // SRS 4.4, 4.5: day-plan panel
    planTitle: 'Your day plan',
    planCountLabel: (n) => (n === 1 ? 'place in plan' : 'places in plan'),
    planSummary: (n) => (n === 1 ? '1 place' : `${n} places`),
    closePlan: 'Close day plan',
    planDate: 'Day of visit',
    startTime: 'Start time',
    planSettingsHint: 'The plan starts and ends at Chundikuli.',
    planEmpty: 'Your day plan is empty. Choose “Add to day plan” on any place to start.',
    planCalculating: 'Calculating your plan…',
    planUpdating: 'Updating your plan…',
    planUpdated: 'Plan updated.',
    planFailed: 'The plan could not be calculated. Check your connection and try again.',
    planUnavailablePlaces: 'Remove unavailable places',
    planDayLine: (day, date, start) => `${day}, ${date} · starting ${start}`,

    // FR-24, FR-25
    orderAuto: 'Order: nearest place first (automatic).',
    orderManual: 'Order: your own order.',
    autoOrder: 'Auto-order',
    autoOrderHint: 'Puts the places back in nearest-first order.',
    reorderHint: 'Use the arrow buttons (or drag a place) to change the order.',
    moveEarlier: (name) => `Move ${name} earlier`,
    moveLater: (name) => `Move ${name} later`,
    removeStop: (name) => `Remove ${name} from the plan`,
    remove: 'Remove',
    stopNumber: (n) => `Stop ${n}`,

    // FR-26 to FR-28
    arriveLeave: (arrival, departure) => `Arrive ${arrival} · Leave ${departure}`,
    travelFromStart: (km, time) => `${km} · ${time} drive from Chundikuli`,
    travelFromPrevious: (km, time) => `${km} · ${time} drive from the previous stop`,
    visitMinutes: 'Visit (minutes)',
    visitMinutesHint: 'Leave empty for the suggested time.',
    visitMinutesInvalid: (max) => `Enter a whole number of minutes from 1 to ${max}.`,

    // NFR-10
    notScheduled: 'Not scheduled',
    estimatedArrival: (time) => `Estimated arrival ${time}`,
    notScheduledHint: 'Move the start time earlier or remove places so this stop fits in the day.',
    unscheduledSummary: (n) => (n === 1
        ? '1 place could not be fitted in before the latest arrival time.'
        : `${n} places could not be fitted in before the latest arrival time.`),

    // FR-24, FR-32
    returnLeg: (time, km, travel) => `Back at Chundikuli at ${time} (${km}, ${travel} drive).`,
    totalsTitle: 'Day totals',
    totalDistance: 'Travel distance',
    totalTravel: 'Travel time',
    totalVisit: 'Time at places',
    totalElapsed: 'Whole day',
    endTime: 'Back at Chundikuli',
    totalsNote: 'Totals include the drive back to Chundikuli.',

    // FR-34
    clearPlan: 'Clear plan',
    clearPlanConfirm: 'Remove every place from your day plan? This cannot be undone.',
    planCleared: 'Your day plan was cleared.',

    // FR-29, FR-30, NFR-25: every warning has an icon AND a text label, plus a fix (SRS 2.6).
    warnings: {
        ArrivesAfterClosing: {
            icon: '✕',
            label: 'Closed when you arrive',
            hint: 'Move this place earlier, start earlier, or shorten the visits before it.',
        },
        ArrivesBeforeOpening: {
            icon: '◷',
            label: 'Not open yet',
            hint: 'Move this place later, start later, or spend longer at the places before it.',
        },
        ClosedAllDay: {
            icon: '✕',
            label: 'Closed that day',
            hint: 'Choose another day of visit or remove this place.',
        },
        HoursNotRecorded: {
            icon: '?',
            label: 'Opening hours not known',
            hint: 'We do not have opening hours for this day. Check locally before you go.',
        },
        NotScheduled: {
            icon: '!',
            label: 'Not scheduled',
            hint: 'Move the start time earlier or remove places.',
        },
    },
    unknownWarning: { icon: '!', label: 'Check this stop', hint: '' },

    // SRS 2.6: contextual help
    warningsHelpTitle: 'What do these warnings mean?',
    warningsHelpIntro: 'A timing warning means the planned arrival time does not match the opening hours for the day you chose. The place stays in your plan so you can decide what to do:',
    planHelpTitle: 'How the day plan works',
    planHelp: [
        'Add places with “Add to day plan”. The plan is saved in this browser, so it is still here when you come back.',
        'Places are visited nearest-first, starting and ending at Chundikuli. Use the arrows or drag to choose your own order; “Auto-order” goes back to nearest-first.',
        'Travel times are estimates from road distance and an average driving speed. Change the visit minutes to match how long you want to stay.',
        'A realistic day has about six places. Places that would be reached too late in the day are marked “Not scheduled”.',
        'Opening hours are indicative; please verify before travelling.',
    ],

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
