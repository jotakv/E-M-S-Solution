# Employee Management System (EMS)

> A full-stack, production-grade HR platform built with **Blazor WebAssembly**, **ASP.NET Core 8**, **ML.NET**, and a structured **Clean Architecture** design.

---

## Table of Contents

1. [Overview](#1-overview)
2. [Architecture](#2-architecture)
   - [Layer Map](#layer-map)
   - [Dependency Rule](#dependency-rule)
   - [Project Responsibilities](#project-responsibilities)
3. [Feature Set](#3-feature-set)
4. [Technology Stack](#4-technology-stack)
5. [Getting Started](#5-getting-started)
6. [Ports & Services](#6-ports--services)
7. [Default Credentials](#7-default-credentials)
8. [Configuration Reference](#8-configuration-reference)
9. [API Endpoints](#9-api-endpoints)
10. [Testing](#10-testing)
11. [Design Decisions & Patterns](#11-design-decisions--patterns)
12. [Troubleshooting](#12-troubleshooting)

---

## 1. Overview

EMS is a complete employee-lifecycle management system. Beyond standard CRUD, it incorporates an **AI-powered HR Intelligence layer** that classifies HR notes with ML.NET sentiment analysis, surfaces real-time morale trends, and scores employee risk from five independent signals (overtime frequency, sick leave, sanctions, negative and positive notes).

**Key differentiators:**
- Every HR note is automatically labelled Positive / Neutral / Negative by a trained ML.NET binary classifier with a 50-keyword fallback.
- The Risk Manager calculates composite scores across the entire employee roster in **five batched SQL queries** (not N+1).
- All significant actions produce **structured Serilog audit events** and are published to a **RabbitMQ** exchange for downstream consumption.
- The ML.NET model is pre-warmed at startup by a hosted service so the first note submission is instant.

---

## 2. Architecture

### Layer Map

```
┌─────────────────────────────────────────────────────────────┐
│  Presentation                                               │
│  ┌─────────────────────────┐  ┌──────────────────────────┐ │
│  │  Client (Blazor WASM)   │  │  Server (ASP.NET Core)   │ │
│  │  Blazor pages/layouts   │  │  REST controllers        │ │
│  │  AllState SPA routing   │  │  Middleware pipeline     │ │
│  │  Syncfusion UI          │  │  Serilog + RabbitMQ      │ │
│  └────────────┬────────────┘  └────────────┬─────────────┘ │
│               │  HTTP/JWT                  │               │
└───────────────┼────────────────────────────┼───────────────┘
                │                            │
┌───────────────┼────────────────────────────┼───────────────┐
│  Application  │                            │               │
│  ┌────────────▼────────────────────────────▼─────────────┐ │
│  │  ClientLibrary            ServerLibrary/Features       │ │
│  │  IGenericServiceInterface IHRRiskService               │ │
│  │  IUserAccountService      IHRAnalyticsService          │ │
│  │  GetHttpClient helper     IHRIntelligenceCacheService  │ │
│  │                           IEmployeeNoteRepository      │ │
│  └────────────────────────────────┬──────────────────────┘ │
└───────────────────────────────────┼───────────────────────┘
                                    │
┌───────────────────────────────────┼───────────────────────┐
│  Infrastructure                   │                       │
│  ┌────────────────────────────────▼──────────────────────┐│
│  │  ServerLibrary/Data          AppDbContext (EF Core)   ││
│  │  ServerLibrary/Repositories  Concrete repositories    ││
│  │  ServerLibrary/Services      RabbitMqEventBus         ││
│  │  Server/Services             SentimentService (ML.NET)││
│  │  Server/BackgroundServices   EmsAuditConsumer         ││
│  └───────────────────────────────────────────────────────┘│
└───────────────────────────────────────────────────────────┘
                                    │
┌───────────────────────────────────┼───────────────────────┐
│  Domain                           │                       │
│  ┌────────────────────────────────▼──────────────────────┐│
│  │  BaseLibrary/Entities    Employee, EmployeeNote,       ││
│  │                          Branch, Department,           ││
│  │                          Country, City, Town,          ││
│  │                          Overtime, Vacation, Sanction, ││
│  │                          Doctor, ApplicationUser       ││
│  │  BaseLibrary/DTOs        All transfer objects          ││
│  │  BaseLibrary/Responses   GeneralResponse, etc.         ││
│  └───────────────────────────────────────────────────────┘│
└───────────────────────────────────────────────────────────┘
```

### Dependency Rule

> **Inner layers know nothing about outer layers.** `BaseLibrary` references no other project. `ServerLibrary` references only `BaseLibrary`. `Server` references `ServerLibrary` and `BaseLibrary`. `Client` depends on `ClientLibrary` and `BaseLibrary` only.

```
BaseLibrary
    ▲
ServerLibrary ──────► BaseLibrary
    ▲
Server ──────────────► ServerLibrary, BaseLibrary
Client ──────────────► ClientLibrary, BaseLibrary
ClientLibrary ───────► BaseLibrary
```

No circular dependencies exist. This is verified by the .NET SDK project references.

### Project Responsibilities

| Project | Layer | Responsibility |
|---------|-------|----------------|
| `BaseLibrary` | **Domain** | Core entities (14), all DTOs, shared response types. Zero framework dependencies. |
| `ServerLibrary` | **Application + Infrastructure** | EF Core DbContext, all repository implementations, HR Intelligence feature services (`HRRiskService`, `HRAnalyticsService`), repository contracts (`IEmployeeNoteRepository`), RabbitMQ event bus, REST-Countries sync services. |
| `Server` | **Presentation (API)** | ASP.NET Core controllers, ML.NET `SentimentService` (singleton), `SentimentWarmupService` (startup pre-warming), `EmsAuditConsumer` (RabbitMQ background consumer), Serilog structured logging, JWT middleware, Swagger. |
| `Client` | **Presentation (UI)** | Blazor WASM pages, `AllState` SPA navigation state, Syncfusion chart/grid/dialog components, `localStorage` preference persistence. |
| `ClientLibrary` | **Application (Client-side)** | HTTP service abstractions (`IGenericServiceInterface<T>`), `GetHttpClient` (JWT-aware), `UserAccountService`, `CountrySyncClientService`, route constants. |
| `Tests` | **Quality** | xUnit tests with EF InMemory provider, 23 tests covering seeder, repositories, and services. |

---

## 3. Feature Set

### Core HR Modules

| Module | Description |
|--------|-------------|
| **Employee Management** | Full CRUD with photo upload, civil ID, file number, Syncfusion grid with Excel/PDF export, audit trail |
| **Organizational Structure** | General Departments → Departments → Branches — three-level hierarchy, full CRUD |
| **Location Cascade** | Countries (250+ from REST Countries API) → Cities → Towns — server-side cascade with client filtering |
| **Overtime** | Record and categorize overtime by type; integrated into risk scoring |
| **Vacations** | Vacation requests with type tracking (Annual, Medical, etc.) |
| **Sanctions** | Formal warning and disciplinary records; weighted heavily in risk score |
| **Health / Doctor** | Employee medical / sick-leave records |
| **User Management** | Admin / User roles, JWT-secured registration and login |

### HR Intelligence (AI Layer)

| Feature | Implementation |
|---------|---------------|
| **Sentiment Analysis** | ML.NET binary classifier (SDCA Logistic Regression) trained on a curated HR TSV dataset; keyword fallback for ambiguous mid-range scores |
| **HR Notes** | Authorized HR users create timestamped observations per employee; author resolved from JWT server-side |
| **Risk Manager** | Composite score (overtime×5 + sick leave×4 + sanctions×6 + neg. notes×10 − pos. notes×3, clamped 0–100); all employees scored in 5 batch DB queries |
| **Risk Levels** | High ≥ 61 · Medium 31–60 · Low ≤ 30; expandable per-row breakdown with formula display |
| **Sentiment Trend** | Time-series chart grouped by week (≤30 d), month (≤365 d), or year (all-time) |
| **Department Morale** | 100% stacked bar chart showing Positive/Neutral/Negative distribution per department |
| **Analytics Panel** | Dashboard overlay with 6 KPI cards; open/closed state persisted to `localStorage` |
| **Time Window** | Configurable 7 / 30 / 90 / 180 / 365 days; preference saved to `localStorage` |

### Security & Audit

| Feature | Detail |
|---------|--------|
| **JWT Bearer Auth** | Access + refresh token pair; `[Authorize]` on every sensitive endpoint |
| **Role-based access** | `Admin` role required for country sync, user management |
| **Author spoofing prevention** | `CreatedByUserId` on HR notes resolved from `ClaimTypes.Name` JWT claim, not client-supplied field |
| **Strong password** | Validated on registration: min 8 chars, uppercase, digit, special char |
| **Structured audit log** | Every export, print, image upload, and HR note creation emits a `logger.LogInformation(...)` with structured fields queryable in Seq |
| **RabbitMQ audit events** | Same events published to `ems.audit.*` queues; `EmsAuditConsumer` persists them to the `AuditLogs` table |
| **CORS** | Locked to the Blazor WASM origin (`https://localhost:7201`) |

### Performance & Reliability

| Concern | Solution |
|---------|---------|
| **Dashboard first-load latency** | All 4 HR Intelligence endpoints cache their results for 3 minutes in `IMemoryCache`; cache is busted immediately when a new HR note is created |
| **Country list cache** | `CountryController.GetAll()` uses a 5-min sliding / 1-hour absolute `IMemoryCache` entry; invalidated on add/update/delete/sync |
| **ML.NET model startup** | `SentimentWarmupService` (IHostedService) fires a background prediction call at startup; the `Lazy<T>` singleton is warmed before the first real request |
| **N+1 query elimination** | `HRRiskService` loads all employees once, then makes 4 additional batch-aggregation queries (GroupBy → ToDictionary), processing entirely in-memory |
| **SfDialog lifecycle** | All Syncfusion dialog components are mounted inside their parent `@if (allState.ShowXxx)` guards so they fully unmount on navigation, preventing `ObjectDisposedException` |
| **Error boundaries** | `ErrorBoundary` wraps `@Body` in `MainLayout.razor`; `OnAfterRenderAsync` is wrapped in try/catch throughout |

---

## 4. Technology Stack

| Concern | Technology | Notes |
|---------|-----------|-------|
| Frontend | **Blazor WebAssembly** (.NET 8) | SPA, no server-side rendering required |
| Backend | **ASP.NET Core 8 Web API** | REST, JWT, Serilog, Swagger |
| Database | **SQL Server** (LocalDB in dev) | EF Core 8, Code-First migrations |
| ORM | **Entity Framework Core 8** | Repository pattern over DbContext |
| AI / ML | **ML.NET** | Binary sentiment classifier; SDCA Logistic Regression |
| Messaging | **RabbitMQ** | Audit event bus; app starts normally if broker is offline |
| Logging | **Serilog** | Console + rolling file + Seq sinks; structured properties |
| UI Components | **Syncfusion Blazor** | Grid, Charts (accumulation, line, stacked bar), Dialog |
| Toast notifications | **Blazored.Toast** | Success/error toasts with slide animation |
| Auth | **JWT Bearer** + **BCrypt.Net** | Access + refresh tokens; bcrypt password hashing |
| Testing | **xUnit** + **EF InMemory** | 23 unit tests; zero external dependencies |
| External API | **REST Countries** | Country + capital sync (admin-only) |

---

## 5. Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8)
- **SQL Server LocalDB** — included with Visual Studio 2022 (any edition)
  Or full SQL Server; update `DefaultConnection` in `Server/appsettings.json`
- **RabbitMQ** — optional. Audit events fall back to Serilog-only if the broker is unreachable.

### Fresh clone → running in 3 steps

```bash
# 1. Clone
git clone <repo-url>
cd E-M-S-Solution-Try3

# 2. Start the API (applies migrations + seeds demo data automatically)
dotnet run --project Server

# 3. Start the Blazor client (separate terminal)
dotnet run --project Client
```

Open `http://localhost:5049` in your browser.

> **No port editing required.** `Client/wwwroot/appsettings.json` points to `https://localhost:7012` which matches the Server's HTTPS launch profile. The dev seeder runs automatically on first start if `SeedDemoDataOnStartup: true` is set (default in Development).

### Manual migration (optional)

If you prefer to run migrations before starting the server:

```bash
dotnet ef database update --project ServerLibrary --startup-project Server
```

---

## 6. Ports & Services

| Service | URL | Notes |
|---------|-----|-------|
| Blazor WASM client | `http://localhost:5049` | Dev profile |
| ASP.NET Core API (HTTP) | `http://localhost:5094` | |
| ASP.NET Core API (HTTPS) | `https://localhost:7012` | Client connects here |
| Swagger UI | `https://localhost:7012/swagger` | JWT Authorize button in top-right |
| RabbitMQ Management | `http://localhost:15672` | guest / guest |
| Seq log viewer | `http://localhost:5341` | Optional; configure in `appsettings.json` |

---

## 7. Default Credentials

| Role | Email | Password |
|------|-------|----------|
| Admin | admin@ems.local | Admin123! |
| HR Manager | hrmanager@ems.local | HRManager123! |
| Employee | employee@ems.local | Employee123! |

---

## 8. Configuration Reference

**`Server/appsettings.json`** (key sections):

```jsonc
{
  "ConnectionStrings": {
    // LocalDB default — change to your SQL Server instance
    "DefaultConnection": "Server=(localdb)\\MSSQLLocalDB;Database=EmployeeDB;..."
  },
  "JwtSection": {
    "Key": "<256-bit secret>",
    "Issuer": "https://localhost:7012",
    "Audience": "https://localhost:5049"
  },
  "RabbitMQ": {
    "HostName": "localhost",
    "UserName": "guest",
    "Password": "guest",
    "VirtualHost": "/",
    "Port": 5672
  },
  "SeedDemoDataOnStartup": true   // set false in production
}
```

**`Client/wwwroot/appsettings.json`**:

```jsonc
{
  "BackendApiUrl": "https://localhost:7012"   // must match Server HTTPS port
}
```

---

## 9. API Endpoints

### Authentication
| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/api/authentication/register` | — | Register new user |
| POST | `/api/authentication/login` | — | Login, returns JWT + refresh |
| POST | `/api/authentication/refresh-token` | — | Refresh access token |

### HR Intelligence
| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/api/hrintelligence/summary?days=30` | Bearer | Sentiment summary (cached 3 min) |
| GET | `/api/hrintelligence/trend?days=30` | Bearer | Sentiment trend by period (cached 3 min) |
| GET | `/api/hrintelligence/departments?days=30` | Bearer | Department morale (cached 3 min) |
| GET | `/api/hrintelligence/risks?top=10&days=90&includeAll=false` | Bearer | Risk scores (cached 3 min); `includeAll=true` returns all employees |

### HR Notes
| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/api/hrnotes` | Bearer | Create note; returns sentiment label + score |
| GET | `/api/hrnotes?employeeId=&sentiment=&days=30&page=1&pageSize=20` | Bearer | Paginated notes with filters |

### Employees
| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/api/employee/all` | Bearer | All employees with full navigation |
| GET | `/api/employee/single/{id}` | Bearer | Single employee |
| POST | `/api/employee/add` | Bearer | Create employee |
| PUT | `/api/employee/update` | Bearer | Update employee |
| DELETE | `/api/employee/delete/{id}` | Bearer | Delete employee (cascades) |

### Countries / Cities / Towns
| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| GET | `/api/country/all` | Bearer | All countries (5-min sliding cache) |
| POST | `/api/country/sync` | Admin | Sync from REST Countries API; busts cache |
| GET | `/api/city/all` | — | All cities (CountryId for client-side cascade) |
| GET | `/api/town/all` | — | All towns (CityId for client-side cascade) |

### Audit
| Method | Route | Auth | Description |
|--------|-------|------|-------------|
| POST | `/api/audit/export` | Bearer | Log client-side Excel/PDF export |
| POST | `/api/audit/print` | Bearer | Log client-side print |
| POST | `/api/audit/image-upload` | Bearer | Log employee photo upload |

---

## 10. Testing

```bash
dotnet test Tests/ServerLibrary.UnitTests
```

**23 tests** — all passing, zero warnings:

| Test Class | Count | What it covers |
|-----------|-------|---------------|
| `DevelopmentDataSeederTests` | 5 | Seed counts (including 212 EmployeeNotes), idempotency, FK relationships, demo users, employee–branch–country graph |
| `EmployeeRepositoryTests` | 6 | Insert, update, delete, get-by-id, duplicate-name guard |
| `EmployeeRepositoryLoggingTests` | 3 | Structured Serilog output on insert and update operations |
| `EmployeeNoteRepositoryTests` | 5 | AddAsync, GetByEmployeeId filtering, date-range filter, top-N, descending-order sort |
| `CapitalSyncServiceTests` | 2 | Capital city sync matching and partial-sync idempotency |
| `CountrySyncServiceTests` | 2 | Country upsert logic and sync result reporting |

**Test infrastructure:** EF Core InMemory provider with `Guid.NewGuid()` database names (full isolation per test). `InMemoryEventId.TransactionIgnoredWarning` suppressed so tests match production transaction-aware seeder.

---

## 11. Design Decisions & Patterns

### Repository Pattern
Every entity has a concrete repository implementing `IGenericRepositoryInterface<T>`. Domain-specific operations (e.g., `ICountryRepository.GetByCode2Async`) extend the generic interface. The Application layer depends only on interfaces — infrastructure implementations are injected by the DI container.

### Feature Folders (HR Intelligence)
The `HRRiskService` and `HRAnalyticsService` live in `ServerLibrary/Features/HRIntelligence/` alongside their interfaces. This is the Vertical Slice / Feature Folder pattern applied within a Clean Architecture shell — each feature owns its use-case logic, avoiding "service bloat" in a flat `Services/` directory.

### Singleton ML.NET Model
`SentimentService` is registered as `AddSingleton` because `PredictionEngine<T>` is not thread-safe but `MLContext` and the trained model **are**. The service holds a `Lazy<PredictionEngine<...>?>` with `LazyThreadSafetyMode.ExecutionAndPublication` to guarantee exactly-once training. `SentimentWarmupService` fires a dummy prediction at startup via `Task.Run` so the `Lazy<T>` is forced before the first real HTTP request.

### Structured Audit Logging
All audit events use Serilog's **message template** syntax — e.g., `"Audit — EventName: {EventName} | Action: {Action} | ..."` — so every field is individually queryable in Seq without parsing strings. The same event is published to RabbitMQ for `EmsAuditConsumer` to persist to the `AuditLogs` table.

### Blazor SPA State
`AllState` is a scoped service injected into every page. It exposes boolean flags (`ShowEmployee`, `ShowDashboard`, …) and an `Action` event that pages subscribe to in `OnInitializedAsync` and unsubscribe from in `Dispose`. Navigation is purely state-driven — no Blazor router URL changes for in-app moves.

### HR Intelligence Caching
`HRIntelligenceController` caches all four endpoint responses in `IMemoryCache` with a 3-minute TTL. When a new HR note is created in `HRNotesController`, it explicitly removes 12 pre-defined cache keys (covering all permutations of top/days/includeAll). This is consistent with the existing `CountryController` caching pattern and avoids introducing a separate cache-service abstraction that would complicate the DI graph.

### N+1 Elimination in Risk Scoring
The original implementation issued **4 DB round-trips per employee** inside a `foreach`. The current implementation issues exactly **5 queries total** regardless of employee count:
1. Employees + Branch + Department (eager load)
2. Overtime counts — `GroupBy(o.EmployeeId) → ToDictionary`
3. Sick leave counts — same pattern
4. Sanction counts — same pattern
5. Recent notes (EmployeeId + SentimentLabel only) — grouped in-memory

### Country Cascade (Client-Side)
The client loads all cities and all towns in `LoadDefaults()` (parallel HTTP calls). Cascade filtering (`Where(c => c.CountryId == selected)`) happens entirely in-memory in the browser. Server-side: `CityRepository.GetAll()` returns cities **without** `.Include(c => c.Country)` to avoid circular-reference serialization issues and unnecessary payload weight.

---

## 12. Troubleshooting

| Symptom | Likely cause | Fix |
|---------|-------------|-----|
| `FK_EmployeeNotes_Employees_EmployeeId` error on startup | Seeder ran before employees were committed | Delete the DB and restart — the fixed seeder flushes employees with `SaveChangesAsync` before seeding dependent records |
| `An unhandled error has occurred` on home page | Syncfusion donut chart received all-zero data | Ensure the API is reachable and at least one employee with non-zero data is seeded. Guard: `!_chartData.Any(d => d.Count > 0)` |
| `ObjectDisposedException` from `SfDialog.OnAfterRenderAsync` | Dialog component was outside `@if` guard and got re-rendered after disposal | Fixed: all dialogs now live inside `@if (allState.ShowXxx)` so they unmount with their page |
| Risk Manager shows only 5–10 employees | By design — only top N by risk score | Click **"Top risks only"** toggle in the Risk Manager header to switch to **"All employees"** |
| Country dropdown shows stale list after sync | Cache not invalidated | Fixed: `CountryController.SyncCountries/SyncCapitals` now calls `cache.Remove(CountryCacheKey)` |
| City dropdown empty after country selection | Post-sync country has no seeded cities | Only the 6 seeded countries have matching cities/towns. Additional countries can be linked after adding cities via the City management page |
| Build fails with DLL locked | Server process is running | Stop `dotnet run --project Server` before rebuilding |
| RabbitMQ connection refused at startup | Broker not running | Non-fatal — audit events are Serilog-only; the app continues normally |
| Slow first HR note submission | ML.NET model lazy-loading | `SentimentWarmupService` pre-warms the model in the background at startup; the slowness only occurs if the warmup hasn't completed yet (first ~3 s after startup) |
