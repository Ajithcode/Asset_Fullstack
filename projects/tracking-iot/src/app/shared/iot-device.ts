/**
 * One IoT device of the Tracking service's /api/iot-devices, flattened.
 *
 * The list (GET /api/iot-devices) returns flat rows (IoTDeviceListItemResponse) with live status, battery as
 * batteryPercent and, on newer backends, serial / firmware / id. GET /api/iot-devices/{deviceId} and the POST / PUT
 * replies return the nested IoTDeviceDetailsResponse { id, identity{…}, association{…}, status{…} }.
 * iotDeviceRecord() turns either into this one shape so every page reads the same field names.
 */
export interface IoTDeviceRecord {
  /** Database id (details; list rows on newer backends). '' when not given. */
  id: string;
  deviceId: string;
  deviceName: string;
  /** e.g. BLE Tracker, Temperature Sensor, Gateway. */
  deviceType: string;
  model: string;
  modelDisplayName: string | null;
  serialNumber: string | null;
  /** The protocol the device talks, e.g. BLE 5.0, LoRaWAN 1.0.3 (not online / offline). */
  connectivity: string;
  protocol: string | null;
  siteId: string | null;
  siteName: string | null;
  assetRegistrationId: string | null;
  assetCode: string | null;
  assetName: string | null;
  gateway: string | null;
  powerSource: string;
  /** Battery percent from the latest reading (the API's batteryPercent); null when never reported. */
  batteryLevel: number | null;
  batteryPercent: number | null;
  lowBattery: boolean | null;
  signalDbm: number | null;
  lastSeenAt: string | null;
  /** Good | Warning | Critical */
  health: string | null;
  /** Online | Offline (live connection). */
  status: string | null;
  firmwareVersion: string | null;
  pendingFirmwareVersion: string | null;
  firmwareUpdateScheduledAt: string | null;
  latitude: number | null;
  longitude: number | null;
  createdBy: string | null;
  createdAt: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
  clientId: string | null;
  tenantId: string | null;
}

type Raw = Record<string, unknown>;

const obj = (v: unknown): Raw | null => (v && typeof v === 'object' && !Array.isArray(v) ? (v as Raw) : null);
const str = (v: unknown): string | null => (typeof v === 'string' && v !== '' ? v : null);
const num = (v: unknown): number | null => (typeof v === 'number' && Number.isFinite(v) ? v : null);
const bool = (v: unknown): boolean | null => (typeof v === 'boolean' ? v : null);

/** A list row or a details response (or an older flat record) as an IoTDeviceRecord. */
export function iotDeviceRecord(input: unknown): IoTDeviceRecord {
  const raw = obj(input) ?? {};
  const identity = obj(raw['identity']);
  const association = obj(raw['association']);
  const statusBlock = obj(raw['status']);
  // field from the nested block when the response has one, else from the flat row
  const pick = (block: Raw | null, key: string): unknown => (block && key in block ? block[key] : raw[key]);
  const id = (k: string) => pick(identity, k);
  const as = (k: string) => pick(association, k);
  const st = (k: string) => pick(statusBlock, k);
  const battery = num(st('batteryPercent')) ?? num(st('batteryLevel'));
  return {
    id: str(raw['id']) ?? '',
    deviceId: str(id('deviceId')) ?? '',
    deviceName: str(id('deviceName')) ?? '',
    deviceType: str(id('deviceType')) ?? '',
    model: str(id('model')) ?? '',
    modelDisplayName: str(id('modelDisplayName')),
    serialNumber: str(id('serialNumber')),
    connectivity: str(id('connectivity')) ?? '',
    protocol: str(id('protocol')),
    siteId: str(as('siteId')),
    siteName: str(as('siteName')),
    assetRegistrationId: str(as('assetRegistrationId')),
    assetCode: str(as('assetCode')),
    assetName: str(as('assetName')),
    gateway: str(as('gateway')),
    powerSource: str(st('powerSource')) ?? '',
    batteryLevel: battery,
    batteryPercent: battery,
    lowBattery: bool(st('lowBattery')) ?? (str(st('batteryLabel')) ? st('batteryLabel') === 'Low' : null),
    signalDbm: num(st('signalDbm')),
    lastSeenAt: str(as('lastSeenAt')),
    health: str(st('health')),
    status: statusBlock ? str(statusBlock['connection']) : str(raw['status']),
    firmwareVersion: str(id('firmwareVersion')),
    pendingFirmwareVersion: str(id('pendingFirmwareVersion')),
    firmwareUpdateScheduledAt: str(id('firmwareUpdateScheduledAt')),
    latitude: num(st('latitude')),
    longitude: num(st('longitude')),
    createdBy: str(raw['createdBy']),
    createdAt: str(raw['createdAt']),
    updatedBy: str(raw['updatedBy']),
    updatedAt: str(raw['updatedAt']),
    clientId: str(raw['clientId']),
    tenantId: str(raw['tenantId']),
  };
}
