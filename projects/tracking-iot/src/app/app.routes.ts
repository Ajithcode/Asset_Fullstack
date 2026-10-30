import { Routes } from '@angular/router';
import { ModulePlaceholder } from 'shared-ui';
import { AnalyticsInsights } from './analytics-insights/analytics-insights';
import { TagManagement } from './tag-management/tag-management';
import { FirmwareConnectivity } from './firmware-connectivity/firmware-connectivity';
import { GeofenceAlerts } from './geofence-alerts/geofence-alerts';
import { SensorManagement } from './sensor-management/sensor-management';
import { TrackingTelemetry } from './tracking-telemetry/tracking-telemetry';
import { RealTimeLocation } from './real-time-location/real-time-location';
import { DeviceManagement } from './device-management/device-management';
import { DeviceConfiguration } from './device-configuration/device-configuration';
import { DeviceHealth } from './device-health/device-health';
import { DeviceMaintenance } from './device-maintenance/device-maintenance';
import { DeviceTypeModel } from './device-type-model/device-type-model';
import { DeviceReplacement } from './device-replacement/device-replacement';

// Mounted by the shell at /tracking-iot; paths match the Tracking & IoT menu in the shell's sidebar.
// /tracking-iot/locating-classic (the previous floor-plan Locating remote) is routed by the shell.
export const routes: Routes = [
  { path: '', component: ModulePlaceholder, data: { title: 'Tracking & IoT' } },
  { path: 'analytics-insights', component: AnalyticsInsights },
  // Tag IDs was merged into Tag Management; the old link opens its Tag IDs tab
  { path: 'tag-ids', component: TagManagement, data: { view: 'ids' } },
  { path: 'firmware-connectivity', component: FirmwareConnectivity },
  { path: 'geofence-alerts', component: GeofenceAlerts },
  { path: 'device-configuration', component: DeviceConfiguration },
  { path: 'device-health', component: DeviceHealth },
  { path: 'device-maintenance', component: DeviceMaintenance },
  { path: 'device-type-model', component: DeviceTypeModel },
  { path: 'device-replacement', component: DeviceReplacement },
  { path: 'real-time-location', component: RealTimeLocation },
  { path: 'tag-management', component: TagManagement },
  { path: 'device-management', component: DeviceManagement },
  { path: 'gateways', component: ModulePlaceholder, data: { title: 'Gateways' } },
  { path: 'sensor-management', component: SensorManagement },
  { path: 'tracking-telemetry', component: TrackingTelemetry }
];
