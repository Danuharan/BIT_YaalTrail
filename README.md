# YaalTrail – One-Day Visit Planner for Chundikuli, Jaffna

YaalTrail is a web application that helps a visitor plan a one-day trip to tourist attractions within 25 km of Chundikuli, Jaffna (9.6548° N, 80.0307° E). Visitors browse and filter places, read details and opening hours, see them on a map, and build a time-aware day plan. An administrator maintains places, categories, opening hours and photos through a secured console.

| Item | Details |
|---|---|
| Module | ITE2953 – Programming Group Project (BIT, CODL, University of Moratuwa) |
| Student | Ramanathan Danuharan |
| Registration number | E2410987 |
| Group | SH_Grp_69 (individual project) |
| Source repository | https://github.com/Danuharan/BIT_YaalTrail.git |
| Deployment | Not deployed – runs locally |

## 1. Features

### Visitor side (no sign-in)

- List of the 17 seeded places, sorted by distance from Chundikuli or by name.
- Category filter (Religious, Heritage, Natural, Recreational, Dining, Public) and text search.
- Place details: photo gallery, open-now badge (Sri Lanka time), distance, entry fee, description, travel tips and opening hours for all seven days.
- Map view (Leaflet + OpenStreetMap) with category-coloured markers, popups and a Google Maps directions link.

### One-day planner

- Add places to a day plan, choose the date and start time, and set the visit time for each stop.
- Automatic nearest-first ordering from Chundikuli, or manual ordering with the ↑/↓ buttons.
- Arrival and leaving time for every stop, drive time between stops and the return to Chundikuli.
- Warnings: arrives before opening, arrives after closing, closed all day, hours not recorded, and not scheduled (arrival after 18:00). An advisory appears above six places.
- Day totals (distance, travel time, time at places, return time). The plan is kept in the browser and survives a page reload.

### Administrator console

- Sign-in with JWT (60 minutes), 30-minute idle sign-out, and lockout for 15 minutes after 5 failed attempts.
- Add, edit, publish/unpublish and delete places (with confirmation); 25 km radius check on coordinates.
- Seven-day opening-hours editor, category management, and photo upload / reorder / delete (up to 5 photos per place, 3 MB, JPEG/PNG/WebP).

## 2. Technology stack

| Layer | Technology |
|---|---|
| Back end | ASP.NET Core 8 Web API (C#) |
| Data access | Entity Framework Core 8.0.11 with the Npgsql provider |
| Database | PostgreSQL hosted on Supabase |
| Photo storage | Supabase Storage, public bucket `place-photos` |
| Front end | HTML, CSS and JavaScript ES modules (served from `wwwroot`) |
| Map | Leaflet 1.9.4 with OpenStreetMap tiles |
| Security | JWT (HS256), PBKDF2-SHA256 password hashing |
| Testing | xUnit (192 automated tests), Coverlet for coverage |

## 3. Repository structure

```
YaalTrail/
├── YaalTrail.sln
├── .gitignore                     VS template + TestResults/, coveragereport/
├── YaalTrail.Api/                 ASP.NET Core 8 Web API + front end
│   ├── Controllers/               visitor and admin API controllers
│   ├── Data/                      AppDbContext, DbSeeder (seed data)
│   ├── Dtos/                      request and response records
│   ├── Migrations/                EF Core migrations
│   ├── Models/                    entity classes
│   ├── Services/                  business logic
│   ├── Properties/launchSettings.json
│   ├── wwwroot/                   index.html, admin.html, css/, js/
│   ├── appsettings.json           non-secret settings (Planner section)
│   └── Program.cs
├── tests/
│   └── YaalTrail.Tests/           xUnit test project
└── README.md
```

## 4. Prerequisites

- .NET 8 SDK.
- Visual Studio 2022 (17.8 or later) with the ASP.NET workload, or Visual Studio Code with the C# extension.
- EF Core CLI tool: `dotnet tool install --global dotnet-ef --version 8.0.11` (not needed if you use the Package Manager Console in Visual Studio).
- A free Supabase account (PostgreSQL database and Storage).
- A modern web browser (Chrome, Edge or Firefox).

## 5. Installation and setup

### Step 1 – Get the source code

Extract the submitted ZIP, or clone the repository:

```
git clone https://github.com/Danuharan/BIT_YaalTrail.git YaalTrail
cd YaalTrail
```

### Step 2 – Create the Supabase project

1. Create a new project in Supabase and keep its database password.
2. Open **Connect** and copy the **Session pooler** connection string in **.NET** format.
3. Open **Storage**, create a bucket named `place-photos` and mark it **Public**.
4. Open **Project Settings → API Keys** and copy the project URL and the **service_role** key. Keep this key private.

### Step 3 – Set the configuration secrets

Secrets are never stored in the repository or the submitted ZIP, so the developer’s own Supabase credentials and admin password are not included. Use your own Supabase project from Step 2. Set the secrets with .NET user-secrets from the `YaalTrail.Api` folder (values in angle brackets are your own):

```
cd YaalTrail.Api
dotnet user-secrets set "ConnectionStrings:Default" "<Supabase .NET connection string>"
dotnet user-secrets set "Jwt:Key" "<random string of at least 32 bytes>"
dotnet user-secrets set "Admin:InitialPassword" "<password for the first admin account>"
dotnet user-secrets set "Supabase:Url" "https://<project-ref>.supabase.co"
dotnet user-secrets set "Supabase:ServiceKey" "<service_role key>"
```

In Visual Studio you can instead right-click the `YaalTrail.Api` project, choose **Manage User Secrets**, and enter the same keys in `secrets.json`. This file is stored in the Windows user profile (`%APPDATA%\Microsoft\UserSecrets\`), outside the project folder, so it is never committed or zipped. A random JWT key can be generated with `openssl rand -base64 48`.

| Key | Required | Purpose |
|---|---|---|
| `ConnectionStrings:Default` | Yes | PostgreSQL connection used by EF Core. |
| `Jwt:Key` | Yes | JWT signing key; at least 32 bytes, otherwise the app does not start. |
| `Admin:InitialPassword` | Yes (first run) | Password for the seeded admin `admin@yaaltrail.local`. If it is not set, no admin account is created. |
| `Supabase:Url` | For photos | Supabase project URL. |
| `Supabase:ServiceKey` | For photos | Service key for uploading and deleting photo files. |
| `Supabase:Bucket` | No | Storage bucket name; default `place-photos`. |
| `Jwt:Issuer`, `Jwt:Audience` | No | Defaults `YaalTrail.Api` and `YaalTrail.Admin`. |
| `Planner:*` | No | Planner settings in `appsettings.json` (reference point, 25 km radius, 30 km/h, road factor 1.3, latest arrival 18:00). |

Without the Supabase settings the application still runs, but photo uploads return the error “Photo storage is not configured”.

### Step 4 – Create the database

Apply the EF Core migrations (`InitialCreate` and `AddPlaceMaintainedByAdmin`) from the repository root:

```
dotnet ef database update --project YaalTrail.Api
```

Or, in Visual Studio: **Tools → NuGet Package Manager → Package Manager Console**, select `YaalTrail.Api` as the default project and run `Update-Database`.

This creates the tables Categories, Places, OpeningHours, PlaceImages, VisitPlans, PlanItems and AdminUsers.

### Step 5 – Run the application

```
dotnet run --project YaalTrail.Api
```

Or open the solution in Visual Studio, set `YaalTrail.Api` as the start-up project and press **F5** (or **Ctrl+F5**).

On the first start, the seeder adds 6 categories, 17 published places (each with 7 opening-hour rows) and the administrator account. Photos are not seeded; add them through the admin console.

| Page | Address (default http profile) |
|---|---|
| Visitor site | `http://localhost:5261/` |
| Admin console | `http://localhost:5261/admin.html` |
| Health check | `http://localhost:5261/api/health` – returns `{"db":true}` when the database is reachable |

The port comes from `YaalTrail.Api/Properties/launchSettings.json`; use the address shown in the console if it is different.

### Step 6 – Sign in as administrator

Open the admin console and sign in with `admin@yaaltrail.local` and the password you set in `Admin:InitialPassword`.

## 6. Running the tests

```
dotnet test
```

All 192 xUnit tests should pass. They cover the planner, place, category, admin and photo services, using a fake clock and a fake photo store. To produce a coverage report:

```
dotnet test --collect:"XPlat Code Coverage"
dotnet tool install --global dotnet-reportgenerator-globaltool
reportgenerator -reports:"tests/**/coverage.cobertura.xml" -targetdir:coveragereport
```

Latest result: service layer line coverage 93.9%; whole assembly 40.8% line and 74.6% branch (controllers and migrations are not unit-tested).

## 7. API overview

| Endpoint | Purpose |
|---|---|
| **Visitor (no authorization)** | |
| `GET api/places` | List places (query: categories, search, sort) |
| `GET api/places/{id}` | Place details |
| `GET api/categories` | Categories |
| `POST api/plan/calculate` | Calculate a day plan |
| `GET api/settings/planner` | Planner settings for the browser |
| **Administrator (JWT, role Admin)** | |
| `POST api/admin/login`, `POST api/admin/logout` | Sign in and sign out |
| `api/admin/places` (GET, POST, PUT, DELETE, PATCH publish/unpublish) | Place maintenance (7 endpoints) |
| `api/admin/categories` (GET, POST, PUT, DELETE) | Category maintenance (5 endpoints) |
| `api/admin/places/{id}/images` (POST, PUT order, DELETE) | Photo maintenance (3 endpoints) |

## 8. Deployment

1. Publish a release build: `dotnet publish YaalTrail.Api -c Release -o publish`.
2. Copy the `publish` folder to a host that runs ASP.NET Core 8 (for example Azure App Service or a Linux server).
3. Set the secrets as environment variables, using double underscores: `ConnectionStrings__Default`, `Jwt__Key`, `Admin__InitialPassword`, `Supabase__Url`, `Supabase__ServiceKey`.
4. Apply the migrations to the production database (Step 4) before the first start.

The submitted version is **not deployed**; it runs locally as described in Section 5. The steps above describe how it can be deployed.

## 9. Security notes

- No passwords, connection strings or keys are committed. Secrets live in .NET user-secrets (outside the project) or environment variables; `appsettings.json` holds only non-secret settings.
- `.gitignore` (Visual Studio template) excludes `bin/`, `obj/`, `.vs/`, `*.user`, `TestResults/` and `coveragereport/`.
- Admin passwords are hashed with PBKDF2-SHA256 (100,000 iterations). A failed sign-in returns one general message.
- Admin API endpoints require a valid JWT with the Admin role; sessions end after 30 minutes of inactivity or on sign-out.
- Uploaded photos are checked by file signature (JPEG, PNG, WebP), not by file name.

## 10. Known limitations

- Sharing a plan, a whole-route link, a printable view and a read-only shared plan (SRS FR-35 to FR-38) are not implemented.
- The day plan is stored only in the visitor’s browser (localStorage), so it does not move between devices.
- Admin sessions are kept in server memory; restarting the application signs administrators out.
- A photo caption cannot be edited after upload (delete and upload again).
- Travel times are estimates (straight-line distance × 1.3 at 30 km/h), not live traffic.

## 11. Troubleshooting

| Problem | Fix |
|---|---|
| App stops at start-up with a JWT key error | Set `Jwt:Key` to a value of at least 32 bytes. |
| Cannot sign in as admin on a new database | `Admin:InitialPassword` was not set before the first start. Set it and restart. |
| Database connection fails | Use the Supabase Session pooler string in .NET format and check the password; open `api/health`. |
| Photo upload shows a storage error | Check `Supabase:Url`, `Supabase:ServiceKey` and that the `place-photos` bucket exists and is public. |
| Map tiles do not load | The map needs internet access for Leaflet and OpenStreetMap tiles; the list view still works. |

## 12. Submission package contents

```
YaalTrail_E2410987_System.zip
├── README.md                      this file
├── source/YaalTrail/             source code + Git history
├── diagrams/                      UML diagrams (PNG + .puml)
├── database/                      DB setup (README Section 5)
├── video/                         YaalTrail_Demo_E2410987.mp4
├── presentation/                  YaalTrail_Presentation_E2410987.pptx
└── docs/                          SRS (ITE2953_SRS_E2410987.pdf)
```

The final project report is submitted separately in the report portal.
