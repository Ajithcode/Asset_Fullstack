import { Observable } from 'rxjs';
import { findCatalogAsset } from './asset-catalog';
import { MaintenanceApi, nextCode } from './maintenance-api';
import { WorkOrder } from './maintenance.models';

/** Fields a source page (service request, PM schedule, predictive alert) passes when raising a work order. */
export type WorkOrderSeed = Partial<WorkOrder> & Pick<WorkOrder, 'assetId' | 'assetName' | 'workType' | 'title' | 'priority'>;

/** Copies site / building / area / department from the asset catalog when the seed has none. */
export function withAssetLocation(seed: Partial<WorkOrder>): Partial<WorkOrder> {
  const a = findCatalogAsset(seed.assetId);
  if (!a) return seed;
  return { site: a.site, building: a.building, area: a.area, department: a.department, ...stripEmpty(seed) };
}

function stripEmpty<T extends object>(o: T): Partial<T> {
  return Object.fromEntries(Object.entries(o).filter(([, v]) => v !== null && v !== undefined && v !== '')) as Partial<T>;
}

/**
 * Creates a work order with the next WO code (status Scheduled when it has a due date, else Open).
 * Callers load `api.workOrders` first so the running number continues from the existing codes.
 */
export function raiseWorkOrder(api: MaintenanceApi, seed: WorkOrderSeed): Observable<WorkOrder> {
  return api.workOrders.save({
    workOrderId: nextCode('WO', api.workOrders.items().map((w) => w.workOrderId)),
    status: seed.dueDate ? 'Scheduled' : 'Open',
    category: 'Routine',
    ...withAssetLocation(seed)
  });
}

/** Copy of a work order for "Duplicate": same job details, fresh code, status, progress and history. */
export function duplicateSeed(w: WorkOrder): Partial<WorkOrder> {
  const { id, workOrderId, history, createdAt, createdBy, updatedAt, updatedBy, actualStartDate, actualEndDate, ...rest } = w;
  return { ...rest, status: 'Open', progress: 0, tasks: w.tasks.map((t) => ({ ...t, isDone: false, completedAt: null })) };
}
