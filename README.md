# EduCRM — Backend API

ASP.NET Core 8.0 REST API powering the EduCRM Sales CRM application. Connects to PostgreSQL, provides JWT authentication, and serves all data to the React frontend.

---

## How Frontend & Backend Are Connected

```
┌─────────────────────┐         HTTP (JSON)         ┌─────────────────────┐
│                     │  ◄──────────────────────►   │                     │
│   React Frontend    │    http://localhost:5097/api │   .NET Backend      │
│   (localhost:5173)  │                             │   (localhost:5097)   │
│                     │         JWT Token            │                     │
│   Axios HTTP Client │  ──── Authorization ────►   │   Controllers       │
│                     │        Bearer <token>        │     → Services      │
│   src/api/*.js      │                             │       → EF Core     │
│                     │  ◄── JSON Response ────     │         → PostgreSQL│
└─────────────────────┘                             └─────────────────────┘
```

### The Flow (step by step):

1. **User clicks "Sign In"** on React frontend
2. Frontend calls `POST http://localhost:5097/api/auth/login` with `{ email, password }`
3. Backend `AuthController` receives request → `AuthService` checks password hash
4. If valid → returns `{ success: true, data: { token, user } }`
5. Frontend stores `token` in localStorage
6. **Every subsequent API call** includes `Authorization: Bearer <token>` header
7. Backend validates the JWT token → extracts userId from claims → returns data

---

## Tech Stack

| Technology | Purpose |
|---|---|
| ASP.NET Core 8.0 | Web API framework |
| Entity Framework Core 8.0 | ORM (Object-Relational Mapper) |
| Npgsql | PostgreSQL database driver |
| JWT Bearer | Authentication & authorization |
| PostgreSQL | Database (hosted at 34.47.218.197) |

---

## Project Structure (Clean Architecture)

The backend follows **Clean Architecture** with 3 projects:

```
salescrmbackend/
├── SalesCRM.API/                  ← Entry point (HTTP layer)
│   ├── Controllers/               ← API endpoints
│   │   ├── AuthController.cs      ← POST /api/auth/login
│   │   ├── LeadsController.cs     ← CRUD /api/leads
│   │   ├── ActivitiesController.cs ← GET/POST /api/activities
│   │   ├── DealsController.cs     ← CRUD /api/deals + approvals
│   │   ├── DashboardController.cs ← GET /api/dashboard/{role}
│   │   ├── NotificationsController.cs ← GET/PUT /api/notifications
│   │   └── BaseApiController.cs   ← Base class (extracts UserId from JWT)
│   ├── Program.cs                 ← App startup, DI, middleware
│   ├── appsettings.json           ← DB connection, JWT config
│   └── appsettings.Development.json
│
├── SalesCRM.Core/                 ← Domain layer (no dependencies)
│   ├── Entities/                  ← Database models
│   │   ├── User.cs                ← Users (FO, ZH, RH, SH)
│   │   ├── Lead.cs                ← School leads
│   │   ├── Activity.cs            ← Visit/call/demo logs
│   │   ├── Deal.cs                ← Commercial deals
│   │   ├── Region.cs              ← Geographic regions
│   │   ├── Zone.cs                ← Zones within regions
│   │   ├── Notification.cs        ← User notifications
│   │   ├── TaskItem.cs            ← Scheduled tasks
│   │   └── BaseEntity.cs          ← Common Id + timestamps
│   ├── DTOs/                      ← Data Transfer Objects
│   │   ├── Auth/                  ← LoginRequest, LoginResponse, UserDto
│   │   ├── Common/                ← ApiResponse<T> wrapper
│   │   ├── DashboardDto.cs        ← FO/Zone/Region/National dashboard DTOs
│   │   ├── LeadListDto.cs         ← Lead list & detail DTOs
│   │   ├── ActivityDto.cs
│   │   └── DealDto.cs
│   ├── Enums/                     ← LeadStage, UserRole, ApprovalStatus, etc.
│   └── Interfaces/                ← Service & repository contracts
│       ├── IAuthService.cs
│       ├── ILeadService.cs
│       ├── IActivityService.cs
│       ├── IDealService.cs
│       ├── IDashboardService.cs
│       ├── INotificationService.cs
│       ├── IRepository.cs
│       └── IUnitOfWork.cs
│
└── SalesCRM.Infrastructure/       ← Data & external services
    ├── Data/
    │   ├── AppDbContext.cs         ← EF Core DbContext
    │   ├── DbSeeder.cs            ← Seed data (users, leads, deals, etc.)
    │   └── Migrations/            ← EF Core migrations
    ├── Repositories/
    │   ├── Repository.cs           ← Generic repository
    │   └── UnitOfWork.cs           ← Unit of Work pattern
    └── Services/                   ← Business logic implementations
        ├── AuthService.cs          ← Login, JWT generation, password hashing
        ├── LeadService.cs          ← Lead CRUD + pipeline
        ├── ActivityService.cs      ← Activity logging
        ├── DealService.cs          ← Deal creation + approval workflow
        ├── DashboardService.cs     ← Dashboard data aggregation
        └── NotificationService.cs  ← Notification CRUD
```

### How the 3 layers connect:

```
Controller (API)  →  receives HTTP request, calls Service
     ↓
Service (Infra)   →  contains business logic, uses UnitOfWork
     ↓
Repository (Infra) →  talks to database via EF Core
     ↓
Database (PostgreSQL) →  stores all data
```

**Rule:** API depends on Core + Infrastructure. Infrastructure depends on Core. Core depends on nothing.

---

## API Endpoints

All endpoints return JSON wrapped in `{ success: bool, message: string?, data: T }`.

### Authentication (no token required)
| Method | Endpoint | Description |
|---|---|---|
| POST | `/api/auth/login` | Login with email & password, returns JWT token |

### Leads (token required)
| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/leads` | List leads (supports `?search=`, `?stage=`, `?page=`, `?pageSize=`) |
| GET | `/api/leads/{id}` | Get lead details with activity timeline |
| POST | `/api/leads` | Create new lead |
| PUT | `/api/leads/{id}` | Update lead |
| DELETE | `/api/leads/{id}` | Delete lead |
| GET | `/api/leads/pipeline` | Get leads grouped for kanban view |
| GET | `/api/leads/check-duplicate?school=` | Check if school already exists |

### Activities (token required)
| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/activities` | List activities (supports `?page=`, `?pageSize=`) |
| POST | `/api/activities` | Log new activity (visit, call, demo, etc.) |

### Deals (token required)
| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/deals` | List deals |
| POST | `/api/deals` | Create new deal |
| PUT | `/api/deals/{id}/approve` | Approve or reject a deal (ZH only) |
| GET | `/api/deals/pending-approvals` | Get deals pending ZH approval |

### Dashboard (token required)
| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/dashboard/fo` | FO dashboard (revenue, tasks, hot leads) |
| GET | `/api/dashboard/zone` | Zone dashboard (KPIs, FO leaderboard, pending deals) |
| GET | `/api/dashboard/region` | Region dashboard (zone comparison, territory) |
| GET | `/api/dashboard/national` | National dashboard (all regions, loss reasons) |
| GET | `/api/dashboard/team-performance` | FO performance cards for team management |

### Notifications (token required)
| Method | Endpoint | Description |
|---|---|---|
| GET | `/api/notifications` | Get user's notifications |
| PUT | `/api/notifications/{id}/read` | Mark one notification as read |
| PUT | `/api/notifications/read-all` | Mark all notifications as read |

---

## Database Schema

```
┌──────────┐     ┌──────────┐     ┌──────────┐
│  Region   │────►│   Zone   │────►│   User   │
│  (5 rows) │ 1:N │ (5 rows) │ 1:N │ (6 rows) │
└──────────┘     └──────────┘     └────┬─────┘
                                       │ 1:N
                              ┌────────┼────────┐
                              ▼        ▼        ▼
                         ┌────────┐ ┌──────┐ ┌──────────────┐
                         │  Lead  │ │ Task │ │ Notification │
                         │(8 rows)│ │(4)   │ │ (5 rows)     │
                         └───┬────┘ └──────┘ └──────────────┘
                             │ 1:N
                      ┌──────┼──────┐
                      ▼             ▼
                 ┌──────────┐  ┌────────┐
                 │ Activity │  │  Deal  │
                 │ (13 rows)│  │(3 rows)│
                 └──────────┘  └────────┘
```

### Key Relationships:
- **Region** → has many **Zones**
- **Zone** → has many **Users** (FOs, ZH)
- **User** → has many **Leads** (FO creates leads)
- **Lead** → has many **Activities** (visit, call, demo history)
- **Lead** → has many **Deals** (commercial agreements)
- **User** → has many **Tasks** (scheduled work items)
- **User** → has many **Notifications**

---

## Authentication Flow

```
1. POST /api/auth/login  →  { email, password }
2. Server finds user by email
3. Server verifies password using HMAC-SHA256 (salt.hash format)
4. Server generates JWT token with claims:
   - NameIdentifier = userId
   - Email = user email
   - Name = user name
   - Role = FO/ZH/RH/SH
5. Token expires in 7 days
6. Client stores token in localStorage
7. Client sends "Authorization: Bearer <token>" with every request
8. Server extracts userId from token claims for data filtering
```

---

## Getting Started

### Prerequisites
- .NET 8.0 SDK
- PostgreSQL database (already configured at 34.47.218.197)

### Run the Backend

```bash
cd salescrmbackend

# Restore packages
dotnet restore

# Build
dotnet build

# Run (starts on http://localhost:5097)
dotnet run --project SalesCRM.API
```

### First Run
On first startup, the app will:
1. Apply EF Core migrations (create tables)
2. Run `DbSeeder.SeedAsync()` to populate sample data
3. Start listening on `http://localhost:5097`

### Swagger
Visit `http://localhost:5097/swagger` in development mode to explore and test all APIs interactively.

---

## Configuration

### appsettings.json
```json
{
  "Database": {
    "DB_HOST": "34.47.218.197",
    "DB_DATABASE": "stagingsalescrm",
    "DB_USERNAME": "postgres",
    "DB_PASSWORD": "****",
    "DB_PORT": "5432"
  },
  "Jwt": {
    "Key": "EduCRM-Super-Secret-Key-...",
    "Issuer": "SalesCRM.API",
    "Audience": "SalesCRM.Client"
  }
}
```

### CORS
The backend allows requests from:
- `http://localhost:5173` (Vite dev server)
- `http://localhost:3000` (alternative)

---

## Seed Data (Test Accounts)

| Name | Email | Password | Role | Zone |
|---|---|---|---|---|
| Arjun Mehta | arjun@educrm.in | fo123 | FO | Mumbai West |
| Sunita Reddy | sunita@educrm.in | fo123 | FO | Mumbai West |
| Vikram Nair | vikram@educrm.in | fo123 | FO | Pune Central |
| Priya Singh | priya@educrm.in | zh123 | ZH | Mumbai West |
| Rajesh Kumar | rajesh@educrm.in | rh123 | RH | West Region |
| Anita Sharma | anita@educrm.in | sh123 | SH | National |
