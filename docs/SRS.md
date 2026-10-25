# YaalTrail – SRS (working copy for Cursor)

Source: SRS v1.1 (R. Danuharan, E2410987), ITE2953 Programming Group Project 25S2.
YaalTrail = time-aware one-day visit planner and information system for Chundikuli and greater Jaffna.

## 0. Conventions
- FR-nn = functional requirement, NFR-nn = non-functional, BR-nn = business rule, SN-nn = stakeholder need.
- Priority: High = must implement; Medium = implement if schedule permits (project decision: implement ALL, High first).
- Reference location: Chundikuli, Jaffna (9.6548 N, 80.0307 E). Permitted radius: 25 km (BR-01).

## 1. Architecture and constraints
- Three tiers: browser (HTML5, CSS3, ES6 JavaScript, Leaflet.js 1.9.x) -> ASP.NET Core 8 Web API (C#) over HTTPS/JSON -> Supabase PostgreSQL 15+ (+ Supabase Storage for photos).
- Backend: C# ASP.NET Core 8. Frontend: plain HTML/CSS/JS, NO frontend framework.
- Business logic ONLY in a service layer; controllers contain no scheduling, distance or validation logic (NFR-22).
- Data access ONLY via Entity Framework Core + Npgsql (not Supabase client libraries).
- Free components only. OpenStreetMap tiles must be attributed on every map view (FR-19).
- Route guidance is delegated to an external mapping app (Google Maps directions link); no in-browser turn-by-turn navigation (NFR-08).
- Secrets (DB credentials, JWT signing key) via environment/user-secrets, never in the repo (NFR-15).
- Reference location, radius and average speed are configuration values, not constants in code (SRS 6.4).
- Times displayed in 24-hour format, interpreted in Sri Lanka Standard Time (Asia/Colombo).
- Interface text in English; keep UI strings in a single resource/constants file so Tamil/Sinhala can be added later (SRS 6.3). Place names stored so Tamil/Sinhala fields can be added later.

## 2. User classes
- Day Visitor (unauthenticated, uses all planning functions, first-time users, self-explanatory UI).
- Local Resident / Host (same functions as visitor).
- Administrator (authenticated; manages places, categories, opening hours, photos; cannot alter schema/config via the app).

## 3. External interfaces (summary)
- UI: single-page browser UI with views: place catalogue, place detail, map, day-plan panel, printable itinerary, admin console.
- Persistent header: product name, category filter, search field, day-plan count indicator.
- Day-plan panel available from every visitor view (side panel on wide screens, collapsible drawer on narrow screens).
- Responsive 360px to 1920px, no horizontal scroll (NFR-21).
- Error messages appear next to the control and state the corrective action (no raw technical codes).
- Timing warnings use icon AND text label, never colour alone (NFR-25).
- All controls keyboard-operable with visible focus indicator.
- Admin console visually distinct, shows signed-in administrator and a sign-out control on every screen.
- No location services required; reference location is fixed.
- HTTP status codes: 200 success, 201 created, 400 validation failure, 401 missing/invalid token, 403 insufficient privilege, 404 unknown resource, 500 server fault.
- Admin requests carry a bearer token in the Authorization header; tokens never in query strings.
- Responses gzip-compressed where supported. CORS only from the registered frontend origin.
- Map tiles fetched by the browser directly from the tile service (not via the API).
- Shared plan = URL with an opaque random token (NFR-17).

## 4. Functional requirements

### 4.1 Place Catalogue and Category Filtering (High)
Stimulus/response: open app -> list of published places ordered by distance; select categories -> filtered list + updated count; enter keyword -> matches name/description; clear filters -> full list.

| Ref | Requirement |
|---|---|
| FR-01 | Display a list of all published places showing name, category, distance from the reference location and a representative photograph. |
| FR-02 | Filter the place list by one or more categories: religious, heritage, natural, recreational, dining, public. |
| FR-03 | Sort the list by distance from the reference location or alphabetically by name. |
| FR-04 | Keyword search matching the text against name and description. |
| FR-05 | Display the number of places matching the active filter and search criteria. |
| FR-06 | Display an explanatory message when filter/search returns no places. |
| FR-07 | Exclude any place marked unpublished from all visitor-facing views. |

### 4.2 Place Detail Information (High)
| Ref | Requirement |
|---|---|
| FR-08 | For a selected place display: name, category, full description, distance from reference location, opening time, closing time, entry fee, travel tips and geographic coordinates. |
| FR-09 | Display an open/closed indicator derived from the current time and the recorded opening hours for the current day of week. |
| FR-10 | Display the text "Free entry" when the entry fee is zero. |
| FR-11 | Display up to five photographs per place. (Medium; a single photograph is enough for the MVP.) |
| FR-12 | Display "Information not available" for any place attribute not recorded. |
| FR-13 | Display the date on which the place record was last updated. |

### 4.3 Interactive Map and Route Guidance (High)
Stimulus/response: open map -> map centred on reference location with a marker per matching place; select marker -> summary (name, category, distance) with link to detail; request route guidance -> open destination in an external mapping app; tile service unavailable -> notice shown, list access retained.

| Ref | Requirement |
|---|---|
| FR-14 | Display all places matching the active filter as markers on an interactive map. |
| FR-15 | Centre the map on the reference location (Chundikuli, Jaffna) when first displayed. |
| FR-16 | Display a summary (name, category, distance) when a marker is selected. |
| FR-17 | Distinguish markers by category using distinct colour or icon per category. (Medium) |
| FR-18 | Provide a route guidance link that opens the selected place as a destination in an external mapping application. |
| FR-19 | Display the map data provider's attribution on every view containing a map. |
| FR-20 | Display a notice and keep list-based access to place information when map tiles cannot be retrieved. |

### 4.4 Time-Aware One-Day Visit Planner (High) — the distinguishing feature
Stimulus/response: add place -> inserted and schedule recalculated; set start time -> all arrival/departure times recalculated; arrival after closing -> warning against that place and offer alternative ordering; add a seventh place -> advisory that plan may not be achievable in one day; manual reorder -> schedule recalculated in given order.

| Ref | Requirement |
|---|---|
| FR-21 | Add a place to the current day plan from the list, the detail view or a map marker summary. |
| FR-22 | Remove a place from the current day plan. |
| FR-23 | Allow the visitor to specify the start time of the plan, default 08:00. |
| FR-24 | Order places using a nearest-neighbour heuristic on straight-line distance between coordinates, beginning and ending at the reference location, to reduce total travel distance. |
| FR-25 | Allow the visitor to override the generated ordering by rearranging manually. (Medium) |
| FR-26 | Estimate travel time between consecutive places from the road distance between them and a configured average travel speed. |
| FR-27 | Apply a default visit duration per category: religious 45 min; heritage/cultural 60 min; natural/recreational 60 min (90 min for a beach); dining 45 min; public/shopping 30 min; visitor can adjust per place. (Medium) |
| FR-28 | Calculate and display estimated arrival and departure time for every place in the plan. |
| FR-29 | Display a warning against any place whose estimated arrival is after its recorded closing time for the selected day. |
| FR-30 | Display a warning against any place whose estimated arrival is before its recorded opening time for the selected day. |
| FR-31 | Display an advisory when the plan contains more than six places. |
| FR-32 | Display total travel distance, total travel time and total elapsed duration. |
| FR-33 | Retain the current plan in browser storage so it survives page reloads in the same browser. (Medium) |
| FR-34 | Allow the visitor to clear the entire plan after confirmation. |

### 4.5 Itinerary Sharing and Export (Medium)
| Ref | Requirement |
|---|---|
| FR-35 | Generate a shareable link that reproduces the current plan, including start time and ordering, when opened. |
| FR-36 | Generate a route link supplying every place in the plan to an external mapping application as ordered waypoints. |
| FR-37 | Provide a printable view showing for each place its name, arrival time, entry fee and travel tips. |
| FR-38 | Treat a shared plan as read-only unless the recipient chooses to copy it into their own plan. |

### 4.6 Administrative Place Management (High)
Stimulus/response: submit new valid place -> stored and confirmed; coordinates outside radius -> rejected with reason; delete a place used in a saved plan -> ask confirmation and warn that saved plans will be affected.

| Ref | Requirement |
|---|---|
| FR-39 | Authenticated administrator can create a place: name, category, description, coordinates, distance, entry fee, travel tips, opening hours. |
| FR-40 | Authenticated administrator can update any attribute of an existing place. |
| FR-41 | Authenticated administrator can delete a place after explicit confirmation. |
| FR-42 | Reject any place whose coordinates lie more than 25 km from the reference location and display the reason. |
| FR-43 | Reject any opening-hours entry where closing time is not later than opening time, unless the day is marked closed. |
| FR-44 | Record different opening/closing times for each day of the week, and mark a day as closed. |
| FR-45 | Upload, reorder and remove photographs for a place. (Medium; a single photo upload is enough for the MVP.) |
| FR-46 | Create, rename and delete categories; prevent deletion of a category to which places are assigned. |
| FR-47 | Mark a place as published or unpublished. |
| FR-48 | Record date/time each place record was created and last modified, together with the administrator responsible. |

### 4.7 Administrator Authentication and Session Management (High)
Stimulus/response: valid credentials -> access token issued and admin module opens; invalid credentials -> generic failure message and failed-attempt counter incremented; admin endpoint request without valid token -> refused.

| Ref | Requirement |
|---|---|
| FR-49 | Require an administrator to authenticate with email and password before any administrative function is available. |
| FR-50 | Store administrator passwords only as a salted one-way hash. |
| FR-51 | Issue a signed access token valid for at most 60 minutes on successful authentication. |
| FR-52 | Refuse any request to an administrative endpoint without a valid, unexpired access token. |
| FR-53 | Return a generic authentication failure message that does not reveal whether email or password was wrong. |
| FR-54 | Lock an administrator account for 15 minutes after five consecutive failed attempts. |
| FR-55 | Terminate an administrative session after 30 minutes of inactivity. |
| FR-56 | Provide an explicit sign-out function that invalidates the current session. |

## 5. Non-functional requirements

### 5.1 Performance
| Ref | Requirement |
|---|---|
| NFR-01 | Place catalogue rendered within 3 s of the initial request over a 1 Mbps connection. |
| NFR-02 | API responses for place queries complete within 500 ms at the 95th percentile for up to 100 places. |
| NFR-03 | Day plan recalculation completes within 1 s for a plan of up to 10 places. |
| NFR-04 | All map markers for the active filter rendered within 3 s of the tile layer becoming available. |
| NFR-05 | Serve at least 50 concurrent visitors without any response time exceeding twice its specified value. |
| NFR-06 | Assets transferred on first load do not exceed 2 MB, excluding map tiles and place photographs. |

### 5.2 Safety
| Ref | Requirement |
|---|---|
| NFR-07 | Every place detail view shows a notice that opening hours and entry fees are indicative and should be verified before travelling. |
| NFR-08 | No turn-by-turn navigation in the browser; route guidance delegated to an external app. |
| NFR-09 | Display any recorded access warning (unpaved roads, tidal causeways, seasonal closures) within the travel tips field. |
| NFR-10 | By default do not schedule an arrival at any place later than 18:00. |

### 5.3 Security
| Ref | Requirement |
|---|---|
| NFR-11 | All browser-server communication over HTTPS with TLS 1.2 or higher. |
| NFR-12 | Administrator passwords stored using salted PBKDF2 with at least 10,000 iterations. |
| NFR-13 | All database access uses parameterised queries. |
| NFR-14 | All user-supplied content is encoded before being rendered in the browser. |
| NFR-15 | Database credentials and signing keys supplied through environment configuration, never in the source repository. |
| NFR-16 | No visitor account required; no personally identifying information collected from visitors. |
| NFR-17 | Shared plan link uses a random token with at least 128 bits of entropy. |

### 5.4 Quality attributes
| Ref | Requirement |
|---|---|
| NFR-18 | Availability: at least 95% of each calendar month during the demonstration period. |
| NFR-19 | Usability: a first-time visitor can build a three-place plan within 3 minutes without instruction. |
| NFR-20 | Portability: works in the two most recent major versions of Chrome, Firefox, Edge and Safari. |
| NFR-21 | Responsiveness: usable from 360 px to 1920 px width without horizontal scrolling. |
| NFR-22 | Maintainability: business logic in the service layer; controllers contain no scheduling, distance or validation logic. |
| NFR-23 | Testability: opening-hours validation and plan-ordering services covered by automated unit tests with at least 70% statement coverage of the service layer. |
| NFR-24 | Reliability: failure of any external service must not prevent browsing places or viewing details. |
| NFR-25 | Accessibility: all interactive controls reachable by keyboard; text contrast at least 4.5:1. |

### 5.5 Business rules
| Ref | Rule |
|---|---|
| BR-01 | Only places within 25 km of the reference location (Chundikuli, Jaffna) may be published. |
| BR-02 | Only an authenticated administrator may create, modify or delete a place record. |
| BR-03 | A day plan applies to a single calendar day; multi-day itineraries are not supported. |
| BR-04 | The system accepts no reservation, booking or payment of any kind. |
| BR-05 | Every place is assigned to at least one category. |
| BR-06 | A day plan is advised to contain no more than six places (stakeholder finding SN-07). |
| BR-07 | Opening hours may differ by day of week; irregular closures are recorded in the travel tips field. |
| BR-08 | A visitor may use all planning functions without authentication. |

## 6. Database requirements
Entities (SRS Figure B.2): CATEGORY, PLACE, PLACE_IMAGE, OPENING_HOUR, VISIT_PLAN, PLAN_ITEM, ADMIN_USER.
- PLACE 1:N PLACE_IMAGE, PLACE 1:N OPENING_HOUR, CATEGORY 1:N PLACE, VISIT_PLAN 1:N PLAN_ITEM, PLACE 1:N PLAN_ITEM, ADMIN_USER maintains PLACE.
- Coordinates stored as decimal latitude/longitude with at least six decimal places.
- Opening hours stored as one row per place per day of week.
- Entry fee stored as a decimal in Sri Lankan rupees; zero = free entry.
- Deleting a place must NOT delete any saved plan; plan items referring to a deleted place are retained and marked unavailable (implemented with nullable PlanItem.PlaceId + PlaceNameSnapshot).
- Seed the database with the 17 places in the project proposal (Table 1.2) and their categories.
- Take a database backup before the final demonstration.

Seventeen preliminary places (name – category – approx. distance km):
Nallur Kandaswamy Kovil – Religious – 1.5; Old Park – Recreational – 1.2; Mangos Restaurant – Dining – 1.6; Rio Ice Cream – Dining – 1.8; Jaffna Public Library – Heritage/Cultural – 2.0; Jaffna Central Market – Public/Shopping – 2.0; Jaffna Cultural Centre – Cultural – 2.0; Sangiliyan Thoppu & Mantri Manai – Heritage – 2.2; St. Mary's Cathedral – Religious/Heritage – 2.3; Jaffna Fort – Heritage – 2.5; Kandarodai (Kadurugoda) Vihara – Heritage/Religious – 11; Nilavarai Bottomless Well, Puttur – Nature – 12; Maviddapuram Kandaswamy Kovil – Religious – 16; Keerimalai Naguleswaram Temple – Religious/Nature – 18; Casuarina Beach, Karainagar – Nature/Recreational – 20; Charty Beach, Vellanai – Nature – 20; Dambakola Patuna Vihara – Religious/Heritage – 23.
(Implementation uses ONE primary category per place, matching the 1:N ERD.)

## 7. Stakeholder needs traceability (SN)
- SN-01 opening/closing times visible before travelling -> FR-08, FR-09, FR-29, FR-30
- SN-02 distance visible -> FR-01, FR-08, FR-32
- SN-03 entry fee shown -> FR-08, FR-10
- SN-04 all places on a single map with distances -> FR-14, FR-16, FR-32
- SN-05 filter by category -> FR-02, FR-17
- SN-06 itinerary ordered and validated against opening hours -> FR-24, FR-26, FR-28, FR-29
- SN-07 realistic plan is about 5-6 places -> FR-31, BR-06
- SN-08 small local places missing/out of date elsewhere -> FR-39, FR-40, FR-48, FR-13
- SN-09 plan shareable -> FR-35, FR-36, FR-37
- SN-10 photographs, parking, transport info -> FR-11, FR-08
- SN-11 include at least one dining stop -> FR-02, FR-27
- SN-12 avoid backtracking -> FR-24, FR-25, FR-36

## 8. Open items (TBD)
- TBD-01 road distances need field verification before the dataset is final.
- TBD-02 average travel speed constant to be set from measured journeys (use configuration value, default 30 km/h, road factor 1.3 on straight-line distance).
- TBD-04 saved plans retention (indefinite vs expiry) — undecided.
