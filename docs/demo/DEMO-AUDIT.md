# Demo audit – mock data removal

Branches: frontend `demo/customer`, backend `demo/customer-api`. Dev stack only (a360demo.purpleiq.ai, DB Asset360Dev).

Legend: **REAL** = API data shown · **REAL-EMPTY** = wired to a real API, which returns nothing on Asset360Dev today (shows "No data available") · **EMPTY-STATE MISSING-API** = no API exists; empty state shown, no numbers.

## Dashboard (2026-11-18)

Menu: Dashboard > Reports hidden in `src/app/components/shared/sidebar/sidebar.ts` (`// DEMO-HIDDEN`); route `/dashboard/reports`, component and files kept. `/dashboard/widgets` (old widget page) is not in the menu and was not changed.

### IoT & Tracking Overview (`/dashboard/iot-tracking-overview`)

| Widget | Before (mock) | Now | Endpoint |
|---|---|---|---|
| KPI: Connected Assets / Online Now / Alerts / Coverage / Data Freshness | real (already wired) | REAL-EMPTY (0, no IoT readings in dev) | GET /api/asset-dashboard/kpis?days= |
| KPI: Alerts "n Critical" | counted from empty local `ALERTS` array | REAL-EMPTY | GET /api/asset-dashboard/alerts?days=&limit=500 |
| KPI drill: Coverage / Data Freshness | built from empty local `DEVICES` | REAL (connected assets' last-seen) | GET /api/asset-dashboard/kpis (connectedAssetDetails) |
| Real-time Asset Location (map markers, legend, status drill) | empty local `DEVICES` + `SITES` zones | REAL (8 assets; none has coordinates → note on map) | GET /api/asset-dashboard/sites |
| Top Active Sites | real (already wired) | REAL (Site North 4, Site South 4) | GET /api/asset-dashboard/locations |
| Device Connectivity donut | real (already wired) | REAL-EMPTY | GET /api/asset-dashboard/device-connectivity?days= |
| Firmware Updates | empty local `DEVICES` / `LATEST_FIRMWARE` | REAL-EMPTY | GET /api/asset-dashboard/firmware |
| Asset Health Score donut | local `DEVICES` + critical-assets count (criticality, not health) | REAL (all 8 Unknown → "No data available") | GET /api/asset-dashboard/health-scores |
| Utilization Rate (by Asset Type) | empty local `ASSET_TYPES` / `DEVICES` | REAL-EMPTY | GET /api/asset-dashboard/utilization?days= |
| IoT Events Timeline | location updates only | REAL-EMPTY (location + telemetry / geofence / alerts series) | GET /api/asset-dashboard/location-updates?days=k, GET /api/asset-dashboard/events-timeline?days= |
| Sensors Requiring Attention | empty local `DEVICES` | REAL-EMPTY | GET /api/asset-dashboard/sensors-attention |
| Recent Alerts & Exceptions (+ Acknowledge / Start work / Resolve) | empty local `ALERTS`, status change only in memory | REAL-EMPTY; status actions persist | GET /api/asset-dashboard/alerts, PATCH /api/iot-alerts/{id}/status |
| Asset Quick View | real (already wired) | REAL | GET /api/asset-registrations(/{id}) |
| Site / Asset Type / Status filters | master data | REAL (+ sites from the locations API) | GET /api/master-data/sites, /asset-types |

### Asset Overview (`/dashboard/asset-overview`)

| Widget | Before (mock) | Now | Endpoint |
|---|---|---|---|
| KPI: Total / Active / Under Maintenance | real | REAL (8 / 6 / 1) | GET /api/asset-dashboard/statuses |
| KPI: Critical Assets | real | REAL (2) | GET /api/asset-dashboard/critical-assets |
| KPI: Avg Asset Health Score | real | REAL-EMPTY (no condition assessments) | GET /api/asset-performance/summary |
| KPI: Lifecycle Value | "No API yet" | EMPTY-STATE MISSING-API | – |
| Asset Portfolio by Category | real | REAL (HVAC 2, Power 2, IT/Safety/Security/Vehicles 1) | GET /api/asset-dashboard/categories |
| Category filter options | hardcoded `CATEGORIES` names | REAL | categories API + GET /api/master-data/asset-types |
| Asset Health Distribution | "No data" placeholder | REAL (8 Unknown, never assessed) | GET /api/asset-dashboard/health-scores |
| Asset Status Overview | empty local `PORTFOLIO` (all 0) | REAL (Active 6, Under Maintenance 1, Inactive 1) | GET /api/asset-dashboard/statuses?includeAssets=true |
| Quick Insights: critical / no custodian / missing documents | real | REAL (2 / 0 / 8) | /critical-assets, /assets-without-custodian, /assets-without-documents |
| Quick Insights: due for maintenance | empty local `PORTFOLIO` | REAL-EMPTY (new endpoint; no dates / open WOs in dev) | **new** GET /api/asset-dashboard/assets-due-maintenance?days=30 |
| Asset Distribution by Site | real | REAL (sites have no coordinates → table via View All Sites) | /locations, /location-updates, /api/master-data/sites |
| Asset Lifecycle Aging | "No data" placeholder | REAL (8 New) | **new** GET /api/asset-dashboard/aging |
| Assets table (default + per insight) | critical from API, others from empty `PORTFOLIO` | REAL | insight endpoints above |
| "As of 30 / 90 days ago" | replayed mock history | EMPTY-STATE MISSING-API (options disabled) | – (no asset history API) |
| Asset Quick View | real | REAL | GET /api/asset-registrations(/{id}) |

### MISSING-API
- Lifecycle / book value per asset (Lifecycle Value KPI).
- Portfolio history snapshot ("As of 30 / 90 days ago").

### Files changed
- Frontend: `projects/dashboard/src/app/{asset-overview,iot-tracking-overview}/*`, `projects/dashboard/src/app/services/dashboard/dashboard.ts`, shared: `src/app/components/shared/sidebar/sidebar.ts` (1 line).
- Backend (Asset service): `AssetDashboardEndpoints.cs`, `AssetDashboardContracts.cs`, `AssetDashboardRepository.cs`, `IAssetDashboardRepository.cs`, `AssetDashboardReadModels.cs`. No gateway change (routes under /api/asset-dashboard already exist).

## 2026-11-19 MDM > Organization save (backend 53b9cba3, demo/customer-api)
- Cause: API Organization contract lacked code/registrationNumber/website/description/address/contact/settings that the form uses; records loaded without code -> edit sent POST -> 409; extra fields dropped.
- Fix: fields added to entity/requests/response (MDM service only). Seeded org given code ORG-001 (dev DB backfilled via PUT). Verified UI create/edit/reload, no console errors.
