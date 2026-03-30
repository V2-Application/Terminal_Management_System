# FY-Close Runbook — 31st March

## Pre-requisites (Complete by 25th March)
- [ ] Upload FY2025 DLL package to Script Package Manager
- [ ] Activate package (set IsActive = true for FY_CLOSE step)
- [ ] Upload rollback package (previous year DLLs, mark IsRollbackPackage = true)
- [ ] Configure FY Job: window = 30 Mar 11:00 PM to 1 Apr 4:00 AM
- [ ] Verify all 500+ stores have ACTIVE agents (Terminal Health Dashboard)
- [ ] Alert stores with OFFLINE agents (must be resolved before go-live)

## Execution (30th March 11:00 PM)
1. Log in to HO.Web as HOAdmin
2. Navigate to FY-Close Control
3. Review pre-flight report (online store count, package integrity)
4. Set Wave Size: 50 (default)
5. Click **Start FY-Close Batch**
6. Confirm dialog: "This will start FY-Close for X stores"
7. Monitor Live Dashboard — stores should start completing

## During Execution
- Green tiles = completed (target: all stores)
- Red tiles = failed (action required — click Retry or Rollback)
- Orange = running
- Grey = offline (will auto-execute when reconnected, up to 48h)

## Failure Response
- **1-5 failed stores**: Retry individually from Failed Stores Manager
- **>5 failed stores in a region**: Pause batch, investigate, bulk retry
- **DLL-related failure**: Use Rollback button — restores previous DLLs
- **Billing stuck locked**: Do NOT manually unlock — use Rollback workflow

## Completion
- All stores show GREEN
- Summary email received by HO IT team
- Download PDF report from FY-Close Dashboard
- Verify first FY2025 invoice printed at 5 test stores
