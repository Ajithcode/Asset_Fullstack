/**
 * Backend service URLs. Each microservice runs on its own port and each frontend API call uses the URL of the
 * service it talks to. Every service checks the sign-in token and the caller's role permissions itself.
 */
const HOST = 'http://localhost';

export const environment = {
  production: false,
  apiUrl: `${HOST}:5039/`,
  reportsApiUrl: `${HOST}:5480/`,

  // UserAccount service: sign-in through the gateway (5296); its own port 5018 is not published by docker-compose.local.yml
  authApiUrl: `${HOST}:5296/`,
  userAccountApiUrl: `${HOST}:5296/`,
  usersApiUrl: `${HOST}:5296/`,
  // Media service (5300): file uploads
  mediaApiUrl: `${HOST}:5300/`,
  // Asset service (5401)
  assetApiUrl: `${HOST}:5401/`,
  assetRegistrationApiUrl: `${HOST}:5401/`,
  // Master-data dropdowns (api/master-data/...) are served by Master Data (5500), not the Asset service
  assetMasterDataApiUrl: `${HOST}:5500/`,
  assetDashboardApiUrl: `${HOST}:5401/`,
  // Inspection service (5410)
  inspectionApiUrl: `${HOST}:5410/`,
  // EventLog service: only reached through the gateway (5296)
  eventLogApiUrl: `${HOST}:5296/`,
  // Maintenance service (5430)
  maintenanceApiUrl: `${HOST}:5430/`,
  // WIP / Operations service (5440)
  wipApiUrl: `${HOST}:5440/`,
  // Workflows service (5450)
  workflowsApiUrl: `${HOST}:5450/`,
  // Administration (5460) / Master Data (5500)
  administrationApiUrl: `${HOST}:5460/`,
  masterDataApiUrl: `${HOST}:5500/`,
  // Dashboard service (5470)
  dashboardApiUrl: `${HOST}:5470/`,
  // Report Analytics service (5480)
  reportAnalyticsApiUrl: `${HOST}:5480/`,
  // Tracking & IoT service (5490)
  trackingApiUrl: `${HOST}:5490/`,
  // Project / site-hierarchy endpoints: gateway
  projectApiUrl: `${HOST}:5296/`,

  supportEmail: 'support@assets360.com',
  clientId: 'CLT-1001',
  tenantId: 'TENANT-DEFAULT'
};
