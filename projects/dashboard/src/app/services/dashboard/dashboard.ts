import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';
import { environment } from '../../../../../../src/environments/environment';
import type { AssetRegistrationDetail, AssetRegistrationList } from '../../shared/asset-quick-view/quick-view';

/** One KPI tile from GET api/asset-dashboard/kpis */
export interface KpiMetric {
  value: number;
  percent: number | null;
  changePercent: number | null;
  note: string;
}

/** A connected asset listed under the "Connected Assets" KPI. */
export interface ConnectedAssetDetail {
  assetRegistrationId: string;
  assetTag: string;
  assetName: string;
  assetType: string;
  siteName: string;
  assetStatus: string;
  networkStatus: string;
  gatewayId: string;
  firstSeenAt: string;
  lastSeenAt: string;
  readingCount: number;
}

/** Response of GET api/asset-dashboard/kpis */
export interface AssetDashboardKpis {
  days: number;
  from: string;
  to: string;
  totalAssets: number;
  connectedAssets: KpiMetric;
  onlineNow: KpiMetric;
  alerts: KpiMetric;
  coverage: KpiMetric;
  dataFreshness: KpiMetric;
  connectedAssetDetails: ConnectedAssetDetail[];
}

/** One donut band from GET api/asset-dashboard/device-connectivity */
export interface ConnectivityBand {
  name: string;
  count: number;
  percent: number;
}

/** Response of GET api/asset-dashboard/device-connectivity */
export interface DeviceConnectivity {
  total: number;
  centrePercent: number;
  bands: ConnectivityBand[];
}

/** A site row in "Top Active Sites" from GET api/asset-dashboard/locations */
export interface TopSite {
  siteId: string;
  siteName: string;
  siteCode: string | null;
  totalAssets: number;
  onlinePercent: number;
  online: number;
  idle: number;
  offline: number;
  unknown: number;
}

/** Response of GET api/asset-dashboard/locations */
export interface AssetDashboardLocations {
  pins: unknown[];
  legend: Record<string, number>;
  assetsWithoutCoordinates: number;
  topSites: TopSite[];
  totalSites: number;
}

/** An asset listed under a status in GET api/asset-dashboard/sites/{siteId} */
export interface SiteAsset {
  assetRegistrationId: string;
  assetTag: string;
  assetName: string;
  assetType: string;
  assetStatus: string;
  buildingName: string | null;
  floorName: string | null;
  zoneName: string | null;
  locationPath: string | null;
  latitude: number | null;
  longitude: number | null;
  networkStatus: string;
  gatewayId: string | null;
  lastSeenAt: string | null;
}

/** Response of GET api/asset-dashboard/sites/{siteId} */
export interface SiteDetail {
  siteId: string;
  siteName: string;
  siteCode: string | null;
  siteType: string | null;
  city: string | null;
  country: string | null;
  totalAssets: number;
  onlinePercent: number;
  statuses: { status: string; count: number; assets: SiteAsset[] }[];
}

/** One row of GET api/master-data/asset-types (fields the dashboard uses). */
export interface AssetTypeOption {
  id: string;
  assetTypeName: string;
  code: string | null;
  category: { id: string; name: string } | null;
  status: string;
  isDeleted: boolean;
}

/** One row of GET api/master-data/sites (fields the dashboard uses). */
export interface SiteOption {
  id: string;
  siteName: string;
  code: string | null;
  siteType: string | null;
  city: string | null;
  country: string | null;
  status: string;
  isDeleted: boolean;
}

/** One category row of GET api/asset-dashboard/categories */
export interface CategoryCount {
  category: string;
  count: number;
  percent: number;
  subCategories: { subCategory: string; count: number }[];
}

/** Response of GET api/asset-dashboard/categories */
export interface AssetDashboardCategories {
  totalAssets: number;
  categories: CategoryCount[];
}

/** An asset row of the Quick Insights lists (critical-assets, assets-without-custodian, assets-without-documents). */
export interface InsightAsset {
  assetRegistrationId: string;
  assetTag: string;
  assetName: string;
  category: string | null;
  subCategory: string | null;
  assetType: string | null;
  criticality: string | null;
  assetStatus: string | null;
  siteId: string | null;
  siteName: string | null;
  locationPath: string | null;
  custodianName: string | null;
  custodianDepartment: string | null;
  documentCount: number;
}

/** Response of the Quick Insights list endpoints. */
export interface InsightAssetList {
  count: number;
  totalAssets: number;
  assets: InsightAsset[];
}

/** Quick Insights endpoints under api/asset-dashboard. */
export type InsightEndpoint = 'critical-assets' | 'assets-without-custodian' | 'assets-without-documents';

@Injectable({
  providedIn: 'root',
})
export class Dashboard {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${environment.assetDashboardApiUrl}api/asset-dashboard`;

  /** GET api/asset-dashboard/kpis?days=7|30|90 */
  getKpis(days: number): Observable<AssetDashboardKpis> {
    const params = new HttpParams().set('days', days);
    return this.http.get<AssetDashboardKpis>(`${this.baseUrl}/kpis`, { params });
  }

  /** GET api/master-data/asset-types */
  getAssetTypes(): Observable<AssetTypeOption[]> {
    return this.http.get<AssetTypeOption[]>(`${environment.masterDataApiUrl}api/master-data/asset-types`);
  }

  /** GET api/master-data/sites?clientId= (defaults to the environment's client) */
  getSites(clientId: string = environment.clientId): Observable<SiteOption[]> {
    const params = new HttpParams().set('clientId', clientId);
    return this.http.get<SiteOption[]>(`${environment.masterDataApiUrl}api/master-data/sites`, { params });
  }

  /** GET api/asset-dashboard/locations?top=n (top omitted: every site) */
  getLocations(top?: number): Observable<AssetDashboardLocations> {
    let params = new HttpParams();
    if (top) params = params.set('top', top);
    return this.http.get<AssetDashboardLocations>(`${this.baseUrl}/locations`, { params });
  }

  /** GET api/asset-dashboard/sites/{siteId} */
  getSite(siteId: string): Observable<SiteDetail> {
    return this.http.get<SiteDetail>(`${this.baseUrl}/sites/${encodeURIComponent(siteId)}`);
  }

  /** GET api/asset-dashboard/critical-assets | assets-without-custodian | assets-without-documents */
  getInsightAssets(endpoint: InsightEndpoint): Observable<InsightAssetList> {
    return this.http.get<InsightAssetList>(`${this.baseUrl}/${endpoint}`);
  }

  /** GET api/asset-dashboard/critical-assets (Asset service, 5401); feeds the "Critical" health band and Quick Insights. */
  getCriticalAssets(): Observable<InsightAssetList> {
    return this.getInsightAssets('critical-assets');
  }

  /** GET api/asset-dashboard/categories */
  getCategories(): Observable<AssetDashboardCategories> {
    return this.http.get<AssetDashboardCategories>(`${this.baseUrl}/categories`);
  }

  /** GET api/asset-dashboard/device-connectivity?days=7|30|90 */
  getDeviceConnectivity(days: number): Observable<DeviceConnectivity> {
    const params = new HttpParams().set('days', days);
    return this.http.get<DeviceConnectivity>(`${this.baseUrl}/device-connectivity`, { params });
  }

  /** GET api/asset-registrations: registered assets, newest first (pageSize 1-200). */
  getAssetRegistrations(pageSize = 200, search?: string): Observable<AssetRegistrationList> {
    let params = new HttpParams().set('page', 1).set('pageSize', pageSize);
    if (search) params = params.set('search', search);
    return this.http.get<AssetRegistrationList>(`${environment.assetRegistrationApiUrl}api/asset-registrations`, { params });
  }

  /** GET api/asset-registrations/{assetRegistrationId}: the full registration. */
  getAssetRegistration(assetRegistrationId: string): Observable<AssetRegistrationDetail> {
    return this.http.get<AssetRegistrationDetail>(`${environment.assetRegistrationApiUrl}api/asset-registrations/${encodeURIComponent(assetRegistrationId)}`);
  }
}
