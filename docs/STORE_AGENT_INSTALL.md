# Store Agent Installation Guide

## For Development (Same Machine)

Since the Store.Agent.exe hasn't been published yet, use the **Terminal Simulator** at:
```
http://localhost:64759/Simulator
```
This lets you test the full pipeline without installing any exe.

## To Publish Store.Agent (Once)

### Step 1: Publish from Visual Studio
Right-click `Store.Agent` project → **Publish** → Folder → select output path

Or from command line:
```powershell
cd src\Store.Agent
dotnet publish -c Release -r win-x64 --self-contained true -o C:\RetailTMS\AgentPublish
```

### Step 2: Create the zip package
```powershell
Compress-Archive -Path "C:\RetailTMS\AgentPublish\*" `
    -DestinationPath "src\HO.API\wwwroot\agent\StoreAgent.zip"
```

Create the wwwroot/agent folder first:
```powershell
New-Item -ItemType Directory -Force "src\HO.API\wwwroot\agent"
```

### Step 3: Restart HO.API
The download endpoint will now serve the package at:
```
GET /api/v1/agent/download
```

---

## Store PC Installation (After Package is Published)

```powershell
# Run as Administrator on Store PC
$dest = "C:\RetailTMS"
$storeCode = "ST001"   # change to your store code
$apiBase = "http://192.168.x.x:PORT/api/v1"   # HO server IP

New-Item -ItemType Directory -Force -Path $dest

# Download agent
Invoke-WebRequest -Uri "$apiBase/agent/download" -OutFile "$dest\StoreAgent.zip" -UseBasicParsing
Expand-Archive "$dest\StoreAgent.zip" -DestinationPath $dest -Force

# Auto-configure for this store
Invoke-WebRequest -Uri "$apiBase/agent/config?storeCode=$storeCode" -OutFile "$dest\appsettings.json" -UseBasicParsing

# Install as Windows Service
sc.exe create "RetailTMS.StoreAgent" binpath="$dest\Store.Agent.exe" start=auto
sc.exe start "RetailTMS.StoreAgent"
```

---

## What the Agent Does After Install

1. Calls `POST /api/v1/terminals/register` with machine BIOS ID
2. Receives TerminalId + JWT token (saved DPAPI-encrypted)  
3. Polls `GET /api/v1/commands/pending` every 60 seconds
4. Sends heartbeat every 5 minutes → terminal shows **● ONLINE** in HO dashboard
5. Executes BAT scripts when FY-Close commands are dispatched
