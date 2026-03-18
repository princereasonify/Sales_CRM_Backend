  # SingularityCRM Backend — Project Documentation

## Table of Contents
1. [Overview](#overview)
2. [Architecture](#architecture)
3. [Project Structure](#project-structure)
4. [Configuration](#configuration)
5. [Authentication](#authentication)
6. [API Endpoints (All Controllers)](#api-endpoints)
7. [Tracking System — Deep Dive](#tracking-system)
8. [Real-Time (SignalR)](#real-time-signalr)
9. [Background Services](#background-services)
10. [Database & Migrations](#database--migrations)
11. [How Things Work End-to-End](#how-things-work-end-to-end)
12. [Known Fixes](#known-fixes)

---

## Overview

SingularityCRM is a **Sales CRM backend** built with **.NET 8** and **PostgreSQL**. It manages:
- Field officer GPS tracking (live + offline sync)
- Leads, Deals, Activities
- Targets & Daily Allowances with fraud detection
- Role-based dashboards and notifications
- Real-time location streaming via SignalR WebSockets

**Base URL (Production):** `https://singularity-learn.com/sales-crm/api`

---

## Architecture

```
SalesCRM.API            → HTTP layer (Controllers, Hubs, Background Services, Program.cs)
SalesCRM.Core           → Entities, DTOs, Interfaces, Enums (no dependencies)
SalesCRM.Infrastructure → EF Core DbContext, Repositories, Service implementations
```

**Pattern:** Clean Architecture + Repository + Unit of Work

**Stack:**
- .NET 8 / ASP.NET Core
- PostgreSQL via Npgsql / EF Core 8
- JWT Bearer Authentication
- SignalR for real-time WebSocket

---

## Project Structure

```
Sales_CRM_Backend/
├── SalesCRM.API/
│   ├── Program.cs                      # DI setup, middleware pipeline
│   ├── appsettings.json                # DB credentials, JWT config
│   ├── Controllers/
│   │   ├── BaseApiController.cs        # UserId / UserRole helpers from JWT claims
│   │   ├── TrackingController.cs       # GPS tracking endpoints
│   │   ├── AuthController.cs           # Login/register
│   │   ├── ActivitiesController.cs     # Field visit activities
│   │   ├── DealsController.cs          # Deal pipeline
│   │   ├── LeadsController.cs          # Lead management
│   │   ├── DashboardController.cs      # Summary stats
│   │   ├── NotificationsController.cs  # Push notifications
│   │   └── TargetsController.cs        # Sales targets
│   ├── Hubs/
│   │   ├── TrackingHub.cs              # SignalR hub (role-based rooms)
│   │   └── TrackingHubNotifier.cs      # Broadcast helper
│   └── Services/
│       ├── MidnightResetService.cs     # Closes stale sessions at midnight IST
│       └── FollowUpReminderService.cs  # Follow-up reminder notifications
│
├── SalesCRM.Core/
│   ├── Entities/                       # EF Core models
│   ├── DTOs/                           # Request/Response contracts
│   ├── Interfaces/                     # Service & repository interfaces
│   └── Enums/                          # Status enums
│
└── SalesCRM.Infrastructure/
    ├── Data/
    │   ├── AppDbContext.cs             # EF Core DbContext
    │   └── DbSeeder.cs                 # Initial seed data
    ├── Repositories/                   # Generic + specific repos
    ├── Services/                       # Business logic implementations
    └── Migrations/                     # EF Core migration history
```

---

## Configuration

**File:** `SalesCRM.API/appsettings.json`

```json
{
  "Database": {
    "DB_HOST": "34.47.218.197",
    "DB_DATABASE": "stagingsalescrm",
    "DB_USERNAME": "postgres",
    "DB_PASSWORD": "...",
    "DB_PORT": "5432"
  },
  "Jwt": {
    "Key": "EduCRM-Super-Secret-Key-2026-Must-Be-At-Least-32-Chars!",
    "Issuer": "SalesCRM.API",
    "Audience": "SalesCRM.Client"
  }
}
```

The connection string is assembled in `Program.cs` line 20.

---

## Authentication

- **Type:** JWT Bearer
- **Login endpoint:** `POST /api/auth/login`
- **Token placement:** `Authorization: Bearer <token>` header
- **JWT Claims used in controllers:**
  - `UserId` — extracted in `BaseApiController`
  - `UserRole` — extracted in `BaseApiController`
- **SignalR:** Token passed as `?access_token=<token>` query param for WebSocket upgrade

**Roles:**
| Role | Code | Scope |
|------|------|-------|
| Sales Head | `SH` | National — sees everything |
| Region Head | `RH` | Regional |
| Zone Head | `ZH` | Zone |
| Field Officer | `FO` | Own data only |

---

## API Endpoints

### Auth — `/api/auth`
| Method | Path | Description |
|--------|------|-------------|
| POST | `/login` | Login, returns JWT token |
| POST | `/register` | Register new user |

### Tracking — `/api/tracking`
| Method | Path | Description |
|--------|------|-------------|
| POST | `/start-day` | Start the tracking session for today |
| POST | `/end-day` | End session, calculate allowance + fraud score |
| GET | `/session/today` | Get today's session state (button UI) |
| POST | `/ping` | Record a single GPS ping (every 20-30s) |
| POST | `/ping/batch` | **Offline sync** — send accumulated pings at once |
| GET | `/live-locations` | Get live locations of all users in scope |
| GET | `/route/{userId}/{date}` | Get raw + reconstructed route for a user on a date |
| GET | `/allowances?from=&to=` | Get allowance summaries for date range |
| PATCH | `/allowances/{id}` | Approve or reject a daily allowance |
| GET | `/fraud-reports?from=&to=` | Get suspicious sessions with fraud analysis |

### Leads — `/api/leads`
| Method | Path | Description |
|--------|------|-------------|
| GET | `/` | List leads (role-scoped) |
| POST | `/` | Create lead |
| GET | `/{id}` | Get lead details |
| PUT | `/{id}` | Update lead |
| DELETE | `/{id}` | Delete lead |

### Deals — `/api/deals`
| Method | Path | Description |
|--------|------|-------------|
| GET | `/` | List deals |
| POST | `/` | Create deal |
| GET | `/{id}` | Get deal |
| PATCH | `/{id}/stage` | Move deal stage |

### Activities — `/api/activities`
| Method | Path | Description |
|--------|------|-------------|
| GET | `/` | List activities |
| POST | `/` | Log activity (visit, call, etc.) |
| GET | `/{id}` | Get activity detail |

### Dashboard — `/api/dashboard`
| Method | Path | Description |
|--------|------|-------------|
| GET | `/` | Role-scoped summary stats |

### Targets — `/api/targets`
| Method | Path | Description |
|--------|------|-------------|
| GET | `/` | List target assignments |
| POST | `/` | Assign target |
| PATCH | `/{id}` | Update target status |

### Notifications — `/api/notifications`
| Method | Path | Description |
|--------|------|-------------|
| GET | `/` | Get user's notifications |
| PATCH | `/{id}/read` | Mark notification as read |

### Health Check
```
GET /health  →  { "status": "healthy", "timestamp": "..." }
```

---

## Tracking System

### How the Mobile App Uses It

```
App launches
    └─ GET /session/today           → show correct button state (Start / End / ---)

User taps "Start My Day"
    └─ POST /start-day              → creates TrackingSession for today (status=Active)

Every 20-30 seconds while active
    └─ POST /ping                   → record GPS ping, update running distance

App goes offline (no internet)
    └─ queues pings locally

App comes back online
    └─ POST /ping/batch             → sends all queued pings at once (THIS WAS THE 500 ERROR)

User taps "End My Day"
    └─ POST /end-day                → session status=Ended, fraud score calculated,
                                     DailyAllowance record created
```

### Ping Validation Pipeline (single ping)

Every ping goes through these steps in `TrackingService.RecordPingAsync`:

1. **Check active session** — must have status=Active for today
2. **ValidatePing** — checks:
   - Mock location (`IsMocked == true`) → invalid
   - GPS accuracy > 100m → invalid
   - Speed > 200 km/h → invalid
   - Coordinates out of range → invalid
3. **Teleport detection** — if > 3km moved in < 30 seconds → invalid
4. **Noise filter** — if movement < 15m (GPS jitter) → filtered (saved but not counted)
5. **Distance calculation** — Haversine formula, cumulative running total
6. **Session update** — `TotalDistanceKm` and `AllowanceAmount` updated live
7. **SignalR broadcast** — if valid and not filtered, emit `live_location` event

### Batch Ping (Offline Sync)

`POST /api/tracking/ping/batch` sends `{ pings: [...] }`. The server:
1. Sorts pings by `recordedAt` timestamp
2. Calls `RecordPingAsync` for each one sequentially
3. Returns counts: `{ accepted, rejected, filtered, cumulativeDistanceKm, allowanceAmount }`

### End-of-Day Processing (`EndDayAsync`)

When the user taps "End My Day":
1. Session status set to `Ended`
2. `CalculateFraudScoreAsync` runs — 6-factor engine:
   - Mocked pings detected → +40 score
   - Teleport jumps (> 2 occurrences) → +20
   - Constant-speed pattern (variance < 0.5) → +15
   - High invalid ping ratio (> 30%) → +10
   - High stationary ratio (> 80% filtered) → +10
   - Provider switching (> 1/3 pings) → +5
   - Score ≥ 50 → `IsSuspicious = true`
3. `ReconstructPathAsync` runs — Ramer-Douglas-Peucker algorithm (10m tolerance) to simplify route and remove back-and-forth jitter
4. `DailyAllowance` record created with final km and amount

### Allowance Calculation

```
AllowanceAmount = TotalDistanceKm × AllowanceRatePerKm
```
- Default rate: **₹10.00 / km**
- Rate stored per-session in `TrackingSession.AllowanceRatePerKm`

### Key Entities

**TrackingSession**
```
Id, UserId, SessionDate, Status (NotStarted|Active|Ended)
StartedAt, EndedAt
RawDistanceKm, FilteredDistanceKm, ReconstructedDistanceKm, TotalDistanceKm
AllowanceRatePerKm, AllowanceAmount
FraudScore (0-100), IsSuspicious, FraudFlags (JSON array)
```

**LocationPing**
```
Id, SessionId, UserId
Latitude, Longitude, AccuracyMetres, SpeedKmh, AltitudeMetres
RecordedAt, Provider, IsMocked, BatteryLevel
DistanceFromPrevKm, CumulativeDistanceKm
IsValid, InvalidReason
IsFiltered, FilterReason
ClusterGroup
```

---

## Real-Time (SignalR)

**WebSocket endpoint:** `wss://singularity-learn.com/sales-crm/hubs/tracking?access_token=<jwt>`

**Room assignment on connect (based on role):**
| Role | Room |
|------|------|
| SH | `room:national` |
| RH | `room:region:{regionId}` |
| ZH | `room:zone:{zoneId}` |
| FO | `room:user:{userId}` |

**Events emitted by server:**

`live_location` — emitted on every valid, non-filtered ping:
```json
{
  "userId": 5,
  "name": "Ravi Kumar",
  "role": "FO",
  "zoneId": 2,
  "zoneName": "South Zone",
  "regionId": 1,
  "regionName": "Karnataka",
  "latitude": 12.9716,
  "longitude": 77.5946,
  "speedKmh": 35.2,
  "lastSeen": "2026-03-18T06:42:00Z",
  "totalDistanceKm": 12.5,
  "allowanceAmount": 125.0,
  "status": "active",
  "batteryLevel": 0.72,
  "fraudScore": 0,
  "isSuspicious": false
}
```

`session_ended` — emitted when End Day is called.

---

## Background Services

### MidnightResetService
- **Schedule:** Runs every hour, checks if IST hour == 0 (midnight)
- **Action:** Calls `TrackingService.CloseStaleSessionsAsync()`
- **Effect:** Any session still `Active` at midnight IST is auto-closed, fraud score calculated, path reconstructed, allowance created

### FollowUpReminderService
- Sends follow-up reminder notifications for due activities

---

## Database & Migrations

**Database:** PostgreSQL on `34.47.218.197:5432` (db: `stagingsalescrm`)

**Key Tables:**
- `Users`
- `TrackingSessions`
- `LocationPings`
- `DailyAllowances`
- `Leads`
- `Deals`
- `Activities`
- `Notifications`
- `TargetAssignments`
- `Regions`, `Zones`

**Migrations run automatically** at startup via `context.Database.Migrate()` in `Program.cs`.

**Add a new migration:**
```bash
cd Sales_CRM_Backend
dotnet ef migrations add <MigrationName> --project SalesCRM.Infrastructure --startup-project SalesCRM.API
dotnet ef database update --project SalesCRM.Infrastructure --startup-project SalesCRM.API
```

---

## How Things Work End-to-End

### Starting a Trip (Mobile App Flow)
```
1. App starts → GET /session/today
   Response: { session: { status: "not_started" }, buttonState: { startDayEnabled: true } }

2. User taps "Start My Day" → POST /start-day
   Server creates TrackingSession { status: Active, sessionDate: today IST }
   Response: { session: { status: "active", sessionId: 42 }, buttonState: { endDayEnabled: true } }

3. Background location updates every 20-30s → POST /ping
   Body: { latitude, longitude, accuracyMetres, speedKmh, recordedAt, provider, isMocked, batteryLevel }
   Response: { isValid, isFiltered, cumulativeDistanceKm, allowanceAmount, fraudScore }

4. (If offline) Pings queue locally, then → POST /ping/batch
   Body: { pings: [ ... array of PingRequest ... ] }
   Response: { accepted, rejected, filtered, cumulativeDistanceKm, allowanceAmount }

5. User taps "End My Day" → POST /end-day
   Server: closes session, scores fraud, reconstructs path, creates DailyAllowance
   Response: { session: { status: "ended", totalDistanceKm, allowanceAmount, fraudScore } }
```

### Manager Watching Live Map
```
1. Connect WebSocket: wss://.../hubs/tracking?access_token=<jwt>
2. Server assigns to room based on role (zone/region/national)
3. Listen for "live_location" events → plot on map
4. GET /live-locations → initial state of all active users
5. GET /route/{userId}/{date} → show historical route
```

### Approving Allowances
```
1. Manager: GET /allowances?from=2026-03-01&to=2026-03-18
2. Reviews list → PATCH /allowances/{id} with { approved: true, remarks: "..." }
```

---

## Known Fixes

### Bug Fix — 500 Error on `/ping/batch` (Fixed 2026-03-18)

**Root cause:** `TimeZoneInfo.FindSystemTimeZoneById("India Standard Time")` — this is a **Windows-only** timezone ID. The production server runs **Linux** (nginx), where this throws `TimeZoneNotFoundException` → unhandled exception → 500.

**Files fixed:**
- `SalesCRM.Infrastructure/Services/TrackingService.cs` — `GetTodayIst()` method
- `SalesCRM.API/Services/MidnightResetService.cs` — `DoWork()` method

**Change:**
```csharp
// Before (Windows-only, fails on Linux)
TimeZoneInfo.FindSystemTimeZoneById("India Standard Time")

// After (cross-platform IANA ID)
TimeZoneInfo.FindSystemTimeZoneById("Asia/Kolkata")
```

This affected **every** tracking endpoint that checked today's IST date: `start-day`, `end-day`, `session/today`, `ping`, and `ping/batch`.
