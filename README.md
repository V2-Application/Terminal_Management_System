# Retail Terminal Management System (RetailTMS)

A production-ready, centralized terminal management system for 500+ retail stores — enabling Head Office to remotely manage, monitor, and automate year-end financial close operations across all POS terminals.

## Tech Stack

| Layer | Technology |
|-------|-----------|
| HO Web App | ASP.NET Core 8 MVC (Razor + AJAX) |
| HO API | ASP.NET Core 8 Web API |
| Real-Time | SignalR (DashboardHub) |
| Background Jobs | Hangfire + SQL Server backend |
| Database | SQL Server 2022 |
| Store Agent | .NET 8 Windows Service |

## Solution Structure

```
RetailTMS.sln
├── src/
│   ├── HO.Web/            ← MVC frontend (dashboards, store management)
│   ├── HO.API/            ← REST API (agent communication)
│   ├── HO.Application/    ← Use cases, CQRS handlers, services
│   ├── HO.Domain/         ← Core entities, enums, domain events
│   ├── HO.Infrastructure/ ← EF Core, SQL Server, Hangfire jobs
│   ├── HO.Contracts/      ← Shared DTOs between API and Agent
│   ├── HO.Worker/         ← Hangfire background job host
│   └── Store.Agent/       ← .NET Windows Service for store terminals
└── tests/
    ├── HO.Application.Tests/
    ├── HO.Infrastructure.Tests/
    └── Store.Agent.Tests/
```

## Key Features

- **Centralized Year-End Close** — Trigger FY-close batch for all 500+ stores from HO with one click
- **Wave Dispatch** — Configurable wave size (default: 50 stores/wave) to control rollout pace
- **Real-Time Dashboard** — Live store status updates via SignalR (no page refresh needed)
- **Offline Resilience** — Stores reconnect and execute pending commands automatically
- **Rollback Safety** — Automatic DLL backup before changes; one-click rollback from dashboard
- **Billing Lock** — POS billing locked during execution, never released automatically on failure
- **Full Audit Trail** — Immutable audit log of every action with user, timestamp, and result
- **Secure Agent** — JWT auth, RSA package signing, DPAPI credential storage, TLS pinning

## Architecture

```
HEAD OFFICE
┌─────────────────────────────────────────────┐
│  IIS: HO.Web (MVC) + HO.API (Web API)       │
│  Windows Service: HO.Worker (Hangfire)       │
│  SQL Server 2022 (app data + hangfire schema)│
└──────────────────┬──────────────────────────┘
                   │ HTTPS / TLS 1.3
        ┌──────────┼──────────┐
        ▼          ▼          ▼
   STORE A      STORE B    STORE N
 Store.Agent  Store.Agent  Store.Agent
 (polls HO every 60s)
```

## Quick Start

### Prerequisites
- .NET 8 SDK
- SQL Server 2022 (or LocalDB for dev)
- Visual Studio 2022 or VS Code

### Development Setup

```bash
# Clone
git clone https://github.com/V2-Application/Terminal_Management_System.git
cd Terminal_Management_System

# Restore packages
dotnet restore RetailTMS.sln

# Update connection strings in appsettings.Development.json
# then apply migrations
dotnet ef database update --project src/HO.Infrastructure --startup-project src/HO.API

# Run API
dotnet run --project src/HO.API

# Run Web (separate terminal)
dotnet run --project src/HO.Web

# Run Worker (separate terminal)
dotnet run --project src/HO.Worker
```

### Agent Installation (Store Terminal)

```powershell
# Run as Administrator on store terminal
msiexec /i RetailTMS.Agent.msi /quiet HOAPI_URL="https://ho-tms.company.in/api/v1" STORE_CODE="ST042"
```

## Configuration

### HO.API / HO.Web (appsettings.json)
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=RetailTMS;Trusted_Connection=true;"
  },
  "JwtSettings": {
    "Issuer": "RetailTMS",
    "Audience": "RetailTMS.Agents",
    "SecretKeyPath": "certs/jwt-signing.key"
  },
  "PackageSettings": {
    "StoragePath": "\\\\ho-server\\RetailTMS\\Packages",
    "SigningCertThumbprint": "A3F8..."
  }
}
```

### Store.Agent (appsettings.json)
```json
{
  "AgentConfig": {
    "HoApiBaseUrl": "https://ho-tms.company.in/api/v1",
    "StoreCode": "ST042",
    "PollIntervalSeconds": 60,
    "HeartbeatIntervalSeconds": 300,
    "PosExecutablePath": "C:\\Program Files (x86)\\Microsoft Dynamics AX\\60\\Retail POS\\POS.exe"
  }
}
```

## Security

- All agent credentials stored via Windows DPAPI (machine-bound encryption)
- Script packages signed with RSA-2048; agents verify signature before execution
- TLS 1.3 only; agents pin HO server certificate thumbprint
- No plaintext passwords in any config file or script
- Billing lock never auto-released on failure

## Roles

| Role | Access |
|------|--------|
| SuperAdmin | Full access including user management and Hangfire dashboard |
| HOAdmin | Start/pause FY jobs, upload packages, retry/rollback |
| HOOperator | Monitor, cancel queued commands, manual retry |
| HOViewer | Read-only access to all screens |
| StoreIT | Read-only access to own store's data only |

## Year-End Close Workflow (31st March)

1. HO Admin uploads new DLL package → system signs it
2. HO Admin creates FY2025 job configuration
3. HO Admin clicks **Start FY-Close Batch**
4. Hangfire dispatches Wave 1 (50 stores) → creates Command records
5. Store agents poll, receive FY_CLOSE command
6. Agent: pre-flight checks → billing lock → download & verify package → execute → report
7. Dashboard updates live via SignalR
8. Failed stores surfaced immediately for retry/rollback
9. Offline stores queued for up to 48h catch-up window
10. Final summary report generated when all stores complete

## License
MIT
