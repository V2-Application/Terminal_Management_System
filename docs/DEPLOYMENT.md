# Deployment Guide

## Head Office Server Setup

### Prerequisites
- Windows Server 2022
- IIS 10 with ASP.NET Core Hosting Bundle (.NET 8)
- SQL Server 2022

### IIS Setup
1. Create application pool: RetailTMS (No Managed Code, .NET CLR v4)
2. Create sites:
   - `tms.company.in` → HO.Web (port 443, HTTPS)
   - `tms-api.company.in` → HO.API (port 443, HTTPS)
3. Install TLS certificate (internal CA or Let's Encrypt)

### SQL Server Setup
```sql
CREATE DATABASE RetailTMS;
GO
-- Run scripts/001_InitialSchema.sql
```

### Deploy Applications
```powershell
dotnet publish src/HO.Web -c Release -o C:\RetailTMS\Web
dotnet publish src/HO.API -c Release -o C:\RetailTMS\API
dotnet publish src/HO.Worker -c Release -o C:\RetailTMS\Worker

# Install Worker as Windows Service
sc create "RetailTMS.Worker" binpath="C:\RetailTMS\Worker\HO.Worker.exe"
sc start "RetailTMS.Worker"
```

## Store Agent Deployment

### First-time Install
```powershell
# Copy Store.Agent to store terminal
# Run as Administrator:
dotnet publish src/Store.Agent -r win-x64 -c Release -o C:\RetailTMS\Agent

sc create "RetailTMS.StoreAgent" binpath="C:\RetailTMS\Agent\Store.Agent.exe"
sc config "RetailTMS.StoreAgent" start= auto
sc start "RetailTMS.StoreAgent"
```

### Configure Agent
Edit `C:\RetailTMS\Agent\appsettings.json`:
```json
{
  "AgentConfig": {
    "HoApiBaseUrl": "https://tms-api.company.in/api/v1",
    "StoreCode": "ST042"
  }
}
```

### Verify Registration
Check HO.Web → Store Master → Find store → Terminals tab
Agent should appear as ACTIVE within 5 minutes.
