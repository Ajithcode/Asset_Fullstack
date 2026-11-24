/* Types and option defaults for Geofence & Alerts. Zones, rules and alert events are loaded from the Tracking API. */

export type ZoneType = 'Allowed' | 'Warning' | 'Restricted';
export type ZoneStatus = 'Active' | 'Disabled';
export type ZoneShape = 'Polygon' | 'Rectangle' | 'Circle';
export type Trigger = 'enter-exit' | 'enter' | 'exit' | 'dwell' | 'unauthorized' | 'lost';
export type Severity = 'Informational' | 'Low' | 'Medium' | 'High' | 'Critical';
export type RuleStatus = 'Draft' | 'Active' | 'Disabled';
/** Alert lifecycle: New → Acknowledged → Assigned → Investigating → Resolved → Closed (API: Open … In Progress …). */
export type EventStatus = 'New' | 'Acknowledged' | 'Assigned' | 'Investigating' | 'Resolved' | 'Closed';

/* Option defaults; the geofence lists are replaced by GET /api/geofences/options once loaded. Alert rule options have
   no endpoint, so these match the Tracking service's AlertRule* constants. */
export const ASSET_GROUPS = ['Forklifts', 'Mobile Tools', 'Vehicles', 'All assets'];
export const SCHEDULES = ['Always active', 'During working hours', 'Outside working hours'];
export const ZONE_TYPES: ZoneType[] = ['Allowed', 'Warning', 'Restricted'];
export const ZONE_SHAPES: ZoneShape[] = ['Polygon', 'Rectangle', 'Circle'];
export const ZONE_COLORS = ['#2563EB', '#F5B40B', '#EF4444', '#0D9488', '#7C3AED', '#F97316', '#EC4899', '#0891B2'];
export const CUSTOM_SCHEDULE = 'Custom schedule';
export const CONDITIONS = ['During all hours', 'During working hours', 'Outside working hours', CUSTOM_SCHEDULE];
export const WEEKDAYS = ['Mon', 'Tue', 'Wed', 'Thu', 'Fri', 'Sat', 'Sun'];
export const SEVERITIES: Severity[] = ['Informational', 'Low', 'Medium', 'High', 'Critical'];
export const RULE_STATUSES: RuleStatus[] = ['Draft', 'Active', 'Disabled'];
export const TRIGGERS: { value: Trigger; label: string }[] = [
  { value: 'enter', label: 'Asset enters zone' },
  { value: 'exit', label: 'Asset exits zone' },
  { value: 'enter-exit', label: 'Asset enters or exits zone' },
  { value: 'dwell', label: 'Asset dwells too long' },
  { value: 'unauthorized', label: 'Unauthorized zone entry' },
  { value: 'lost', label: 'Location lost in zone' },
];
export const EVENT_STATUSES: EventStatus[] = ['New', 'Acknowledged', 'Assigned', 'Investigating', 'Resolved', 'Closed'];
/** Statuses before Resolved: the alert still needs someone. */
export const OPEN_STATUSES: EventStatus[] = ['New', 'Acknowledged', 'Assigned', 'Investigating'];
export const DEBOUNCE_OPTIONS: { value: number; label: string }[] = [
  { value: 0, label: 'None (immediate)' }, { value: 15, label: '15 seconds' }, { value: 30, label: '30 seconds' }, { value: 60, label: '1 minute' },
  { value: 120, label: '2 minutes' }, { value: 300, label: '5 minutes' },
];
export const COOLDOWN_OPTIONS: { value: number; label: string }[] = [
  { value: 0, label: 'None' }, { value: 5, label: '5 minutes' }, { value: 15, label: '15 minutes' }, { value: 30, label: '30 minutes' },
  { value: 60, label: '1 hour' }, { value: 240, label: '4 hours' },
];
export const ESCALATE_OPTIONS: { value: number; label: string }[] = [
  { value: 0, label: 'No escalation' }, { value: 5, label: 'After 5 minutes' }, { value: 15, label: 'After 15 minutes' },
  { value: 30, label: 'After 30 minutes' }, { value: 60, label: 'After 1 hour' },
];

export interface Zone {
  id: string;
  name: string;
  type: ZoneType;
  site: string;
  siteId: string | null;
  status: ZoneStatus;
  shape: ZoneShape;
  /** Corner count for polygons / rectangles. */
  points: number;
  color: string;
  groups: string[];
  schedule: string;
  monitorEntry: boolean;
  monitorExit: boolean;
  /** Dwell alert threshold, minutes (null when not set). */
  dwellMin: number | null;
  /** Monitored asset count as stored by the API. */
  monitoredCount: number;
  lastEventAt: Date | null;
  /** Days since the zone was created (null when the API has no creation date). */
  createdDaysAgo: number | null;
  /** Polygon / rectangle outline (WGS84); undefined until drawn on the map. */
  latlngs?: { lat: number; lng: number }[];
  /** Circle zones. */
  center?: { lat: number; lng: number };
  radiusM?: number;
  /** Bumped whenever the geometry changes, so the map redraws the zone. */
  geoRev?: number;
  /** Business category (Storage, Production, …). */
  category?: string;
  createdBy?: string;
  modifiedAt?: Date | null;
  /** Why the zone was last enabled / disabled, by whom and when. */
  statusReason?: string;
  statusChangedBy?: string;
  statusChangedAt?: Date | null;
}

export interface Rule {
  id: string;
  name: string;
  zoneId: string;
  trigger: Trigger;
  dwellMin: number;
  severity: Severity;
  group: string;
  condition: string;
  createAlert: boolean;
  notify: boolean;
  team: string;
  workflow: boolean;
  workflowName: string;
  inApp: boolean;
  email: boolean;
  push: boolean;
  enabled: boolean;
  /** Seconds between desktop notifications of one alert. */
  notifyEvery: number;
  /** Notifications per alert; 0 = until acknowledged. */
  notifyRepeat: number;
  notifySound: boolean;
  /** Name the API stored for the rule's geofence / floor zone (shown when it is not an outdoor geofence). */
  zoneLabel?: string;
  /** Draft rules are saved but not evaluated; enabled === (status === 'Active'). */
  status: RuleStatus;
  description: string;
  /** Specific assets (registration id / tag); empty = the whole asset group. */
  assetIds: string[];
  /** Unauthorized entry: groups that may enter. */
  authorizedGroups: string[];
  /** Custom schedule. */
  scheduleDays: string[];
  scheduleFrom: string;
  scheduleTo: string;
  /** Debounce / hysteresis, seconds. */
  debounceSec: number;
  /** Minimum minutes between duplicate alerts. */
  cooldownMin: number;
  /** Escalate when unacknowledged after N minutes; 0 = no escalation. */
  escalateAfterMin: number;
  escalateTo: string;
  autoResolve: boolean;
  /** yyyy-mm-dd or ''. */
  effectiveFrom: string;
  effectiveTo: string;
  updatedAt?: Date | null;
  createdBy?: string;
}

/** Floor plan zones are rule targets as "zone:{maps zone id}"; outdoor geofences use their own id. */
export const MAP_ZONE_PREFIX = 'zone:';

export const NOTIFY_EVERY: { value: number; label: string }[] = [
  { value: 5, label: '5 seconds' }, { value: 15, label: '15 seconds' }, { value: 30, label: '30 seconds' },
  { value: 60, label: '1 minute' }, { value: 300, label: '5 minutes' }, { value: 900, label: '15 minutes' },
];
export const NOTIFY_REPEAT: { value: number; label: string }[] = [
  { value: 1, label: 'One time' }, { value: 3, label: '3 times' }, { value: 5, label: '5 times' }, { value: 0, label: 'Until acknowledged' },
];

export interface AlertHistory { at: Date; by: string; action: string; note: string }

/** An IoT alert shown in the event lists. */
export interface AlertEvent {
  /** Alert id, e.g. AL000001. */
  id: string;
  ts: Date;
  /** Asset registration id, or the asset name when there is none. */
  assetCode: string;
  /** Secondary asset label (the name when the code is a registration id). */
  assetSub: string;
  /** Geofence of the rule that raised the alert ('' when unknown). */
  zoneId: string;
  zoneName: string;
  /** API event type, e.g. "Geofence Breach". */
  type: string;
  severity: string;
  message: string;
  /** Assignee, else who acknowledged / resolved it ('—' when nobody has). */
  owner: string;
  status: EventStatus;
  ruleId?: string;
  ruleName?: string;
  /** Dwell / stay length, seconds. */
  durationSec?: number | null;
  assignedTo?: string;
  /** Who acknowledged the alert and when (null until acknowledged). */
  acknowledgedBy?: string;
  acknowledgedAt?: Date;
  escalatedTo?: string;
  deviceId?: string;
  site?: string;
  history?: AlertHistory[];
}

export function startOfToday(): Date {
  const d = new Date();
  d.setHours(0, 0, 0, 0);
  return d;
}
