/**
 * Backend service URLs. Each microservice runs on its own port (published by Asset_Fullstack/docker-compose.local.yml)
 * and each frontend API call uses the URL of the service it talks to. Every service checks the sign-in token and the
 * caller's role permissions itself, so calling a service directly is as safe as going through the API gateway.
 * To go through the gateway instead (e.g. a deployed environment with one public URL), point them all at GATEWAY.
 */
const GATEWAY = 'http://localhost:5296/';

export const environment = {
  production: false,
  apiUrl: 'http://localhost:5039/',
  reportsApiUrl: 'http://localhost:5040/',

  // UserAccount service (sign-in, tokens): through the gateway, which exposes only its sign-in routes (/api/auth);
  // its own user / role endpoints stay internal
  authApiUrl: GATEWAY,
  userAccountApiUrl: GATEWAY,
  usersApiUrl: GATEWAY,
  // Media service (5300): file uploads (the files themselves are served from /uploads)
  mediaApiUrl: 'http://localhost:5300/',
  // Asset service (5401): asset registrations, categories, models, documents, lifecycle, service requests, ...
  assetApiUrl: 'http://localhost:5401/',
  assetRegistrationApiUrl: 'http://localhost:5401/',
  // Asset dashboard endpoints (api/asset-dashboard) live in the Asset service
  assetDashboardApiUrl: 'http://localhost:5401/',
  // Inspection service (5410)
  inspectionApiUrl: 'http://localhost:5410/',
  // EventLog service: activity history. It has no sign-in check of its own, so it is only reached through the gateway.
  eventLogApiUrl: GATEWAY,
  // Maintenance service (5430): work orders, preventive / predictive maintenance, spare parts, AMC
  maintenanceApiUrl: 'http://localhost:5430/',
  // WIP / Operations service (5440)
  wipApiUrl: 'http://localhost:5440/',
  // Workflows service (5450)
  workflowsApiUrl: 'http://localhost:5450/',
  // Administration service (5460): users, roles, access policies, maps & floor plans, settings
  administrationApiUrl: 'http://localhost:5460/',
  // Dashboard service (5470): dashboards and widgets
  dashboardApiUrl: 'http://localhost:5470/',
  // Report Analytics service (5480)
  reportAnalyticsApiUrl: 'http://localhost:5480/',
  // Tracking & IoT service (5490): tags, devices, sensors, telemetry, geofences, alerts
  trackingApiUrl: 'http://localhost:5490/',
  // Master Data Management service (5500): sites, buildings, floors, departments, vendors, ...
  masterDataApiUrl: 'http://localhost:5500/',
  assetMasterDataApiUrl: 'http://localhost:5500/',
  // Project / site-hierarchy endpoints (api/projects, api/buildings, ...) aren't served by any backend service yet
  projectApiUrl: GATEWAY,

  supportEmail: 'support@assets360.com',
  clientId: 'CLT-1001',
  tenantId: 'TENANT-DEFAULT'
};
