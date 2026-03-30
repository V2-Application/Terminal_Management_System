# Running RetailTMS Locally

## Prerequisites
- .NET 8 SDK
- SQL Server (LocalDB or Express is fine for dev)
- Visual Studio 2022 or VS Code

## Step 1 — Connection String
Edit `src/HO.Web/appsettings.json`:
```json
{
  "ConnectionStrings": {
    "DefaultConnection": "Server=.;Database=RetailTMS;Trusted_Connection=True;TrustServerCertificate=True;"
  }
}
```
For SQL Express: `Server=.\\SQLEXPRESS;...`
For LocalDB:     `Server=(localdb)\\mssqllocaldb;...`

## Step 2 — Run (Visual Studio)
Set **HO.Web** as startup project → F5

The app will automatically:
1. Create the RetailTMS database if it doesn't exist
2. Run EF Core migrations (create all tables)
3. Seed 50 sample stores with terminals

## Step 3 — Login
Any username + any password → you're in (dev placeholder auth)

## Step 4 — Explore
| Screen | URL | What to see |
|--------|-----|-------------|
| Dashboard | / | Live KPI tiles, store grid |
| FY-Close | /FYClose | Start batch form + step sequence |
| Store Master | /Store | All 50 sample stores with status |
| Terminals | /Terminal | All terminals with heartbeat status |
| Audit Log | /Audit | All system actions |

## Step 5 — Run the API (optional)
Set **HO.Web** + **HO.API** as multiple startup projects.
The API runs at https://localhost:7xxx/swagger — full Swagger UI with all endpoints.

## Step 6 — Set up Claude AI (optional)
```powershell
$env:ANTHROPIC_API_KEY = "sk-ant-YOUR_KEY_HERE"
```
Then restart — the AI Assistant panel on the dashboard becomes active.
Get your key at: https://console.anthropic.com

## Production Notes
- Replace cookie `SecurePolicy = SameAsRequest` with `Always` for HTTPS
- Replace placeholder login with ASP.NET Core Identity or Azure AD
- Set `ANTHROPIC_API_KEY` as a system environment variable, not in code
