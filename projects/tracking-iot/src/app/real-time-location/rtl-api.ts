import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError } from 'rxjs';
import { apiError, tenantParams, trackingUrl } from '../shared/tracking-api';
import { AssetLocation, Gateway } from '../tracking-telemetry/telemetry-overview-api';

/*
 * Real-Time Location page data (Tracking service, GET /api/real-time-location/...). Same location engine as
 * Tracking & Telemetry: BLE proximity, location = zone of the gateway hearing the tag strongest.
 */

export type Freshness = 'Live' | 'Stale' | 'Offline' | 'Unknown';
export type Movement = 'Moving' | 'Stationary' | 'Unknown';
export type TrailRange = '30m' | '1h' | '24h' | '7d';

/** Moving + Stationary + Unknown = assetsInView. */
export interface RtlKpis { assetsInView: number; moving: number; stationary: number; unknown: number; locationAlerts: number }

/** The location registered on the asset (Asset Registration), i.e. where it is expected to be. */
export interface ExpectedLocation { zoneId: string | null; zoneName: string | null; floorId: string | null; floorName: string | null; buildingName: string | null; siteName: string | null; locationPath: string | null }

export interface RtlAsset {
  assetId: string;
  assetTag: string | null;
  name: string;
  assetType: string | null;
  operationalStatus: string | null;
  technology: string;
  tracker: string | null;
  tags: string[];
  location: AssetLocation | null;
  freshness: Freshness;
  movement: Movement;
  lastZoneChangeAt: string | null;
  lastValidLocationAt: string | null;
  /** e.g. BLE Proximity. */
  locationSource: string;
  /** Zone level / Gateway range / Unknown (no metre value for BLE proximity). */
  accuracy: string;
  confidence: 'High' | 'Medium' | 'Low' | 'Unknown';
  confidenceBasis: string;
  gatewaysHearing: number;
  signalMarginDb: number | null;
  rssi: number | null;
  activeLocationAlerts: number;
  expected: ExpectedLocation | null;
  /** Match, Mismatch, Unknown or Not set. */
  comparisonStatus: string;
  comparisonNote: string;
}

export interface RtlOverview { generatedAt: string; kpis: RtlKpis; assets: RtlAsset[]; gateways: Gateway[] }

export interface TrailSegment { gatewayId: string; zoneName: string | null; floorName: string | null; from: string; to: string; minutes: number; initial: boolean }
export interface RtlTrail { assetId: string; name: string; range: TrailRange; from: string; to: string; segments: TrailSegment[]; zoneChanges: number; timeInZones: { zone: string; minutes: number }[]; note: string }

export interface RtlEvent { eventId: string; at: string; assetId: string | null; assetName: string | null; eventType: string; zone: string | null; floor: string | null; details: string | null; severity: string; source: string | null; status: string }
export interface RtlEventPage { items: RtlEvent[]; total: number; page: number; pageSize: number; totalPages: number; rangeLabel: string; eventTypes: string[] }

@Injectable({ providedIn: 'root' })
export class RtlApi {
  private readonly http = inject(HttpClient);

  overview(siteId: string, floorPlanId: string): Observable<RtlOverview> {
    return this.http.get<RtlOverview>(trackingUrl('real-time-location/overview'), { params: tenantParams({ siteId, floorPlanId }) })
      .pipe(catchError((err) => apiError(err, 'Could not load asset locations.')));
  }

  trail(assetId: string, range: TrailRange): Observable<RtlTrail> {
    return this.http.get<RtlTrail>(trackingUrl(`real-time-location/assets/${encodeURIComponent(assetId)}/trail`), { params: tenantParams({ range }) })
      .pipe(catchError((err) => apiError(err, 'Could not load the location trail.')));
  }

  events(range: string, siteId: string, page: number, pageSize: number, assetId = '', eventType = ''): Observable<RtlEventPage> {
    return this.http.get<RtlEventPage>(trackingUrl('real-time-location/events'), { params: tenantParams({ range, siteId, page, pageSize, assetId, eventType }) })
      .pipe(catchError((err) => apiError(err, 'Could not load location events.')));
  }
}
