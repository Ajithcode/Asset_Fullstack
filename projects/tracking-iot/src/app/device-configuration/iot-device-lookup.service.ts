import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, map } from 'rxjs';
import { IoTDeviceRecord, iotDeviceRecord } from '../shared/iot-device';
import { apiError, getAllPages, trackingUrl } from '../shared/tracking-api';

/**
 * A device as GET /api/iot-devices returns it, flattened (battery as batteryLevel). Read-only here: the IoT Device
 * Management page owns create / update / delete.
 */
export type IoTDeviceDto = IoTDeviceRecord;

/** Device type the API gives gateways (the devices other devices report through). */
export const GATEWAY_TYPE = 'Gateway';

/** Whether an IoT device runs the given firmware-inventory model ("EM300-TH" matches "Milesight EM300-TH" either way round). */
export function sameModel(deviceModel: string | null | undefined, firmwareModel: string | null | undefined): boolean {
  const a = (deviceModel ?? '').trim().toLowerCase();
  const b = (firmwareModel ?? '').trim().toLowerCase();
  if (!a || !b) return false;
  return a === b || a.endsWith(' ' + b) || b.endsWith(' ' + a);
}

/** Read-only access to the IoT device registry (GET /api/iot-devices). */
@Injectable({ providedIn: 'root' })
export class IoTDeviceLookupService {
  private readonly http = inject(HttpClient);

  /** Every registered device (all pages of the list). */
  list(): Observable<IoTDeviceDto[]> {
    return getAllPages<unknown>(this.http, trackingUrl('iot-devices')).pipe(
      map((rows) => rows.map(iotDeviceRecord)),
      catchError((e) => apiError(e, 'Could not load IoT devices.')),
    );
  }
}
