/**
 * Shapes returned by the Maintenance service (backend/Services/Maintenance, camelCase JSON).
 * Field names match the *Response records in A360.Maintenance.Api/Contracts.
 */

/** Audit fields every Maintenance resource carries. */
export interface MaintenanceAudit {
  id: string;
  createdBy?: string | null;
  createdAt?: string | null;
  updatedBy?: string | null;
  updatedAt?: string | null;
  clientId?: string | null;
  tenantId?: string | null;
  status?: string | null;
  isDeleted?: boolean;
}

export interface WorkOrderTask {
  title: string;
  assignedTo?: string | null;
  isDone: boolean;
  completedAt?: string | null;
}

export interface WorkOrderPart {
  partNumber?: string | null;
  partName: string;
  quantity: number;
  unitCost: number;
}

export interface WorkOrderLabour {
  technician: string;
  role?: string | null;
  hours: number;
  hourlyRate: number;
}

export interface WorkOrderDocument {
  name: string;
  url?: string | null;
  fileType?: string | null;
  uploadedBy?: string | null;
  uploadedAt?: string | null;
}

export interface WorkOrderHistoryEntry {
  at: string;
  by?: string | null;
  action: string;
  details?: string | null;
}

/** /api/work-orders */
export interface WorkOrder extends MaintenanceAudit {
  workOrderId: string;
  assetId: string;
  assetName: string;
  /** Maintenance type. */
  workType: string;
  title: string;
  description: string | null;
  priority: string;
  scheduledDate: string | null;
  assignedTechnician: string | null;
  category: string | null;
  dueDate: string | null;
  plannedStartDate: string | null;
  plannedEndDate: string | null;
  actualStartDate: string | null;
  actualEndDate: string | null;
  estimatedHours: number | null;
  source: string | null;
  sourceReference: string | null;
  site: string | null;
  building: string | null;
  area: string | null;
  department: string | null;
  team: string | null;
  vendor: string | null;
  notes: string | null;
  progress: number;
  tasks: WorkOrderTask[];
  parts: WorkOrderPart[];
  labour: WorkOrderLabour[];
  additionalCost: number;
  documents: WorkOrderDocument[];
  /** Newest first; maintained by the server. */
  history: WorkOrderHistoryEntry[];
  partsCost: number;
  labourCost: number;
  totalCost: number;
}

/** /api/issue-reports — shown in the UI as Service Requests. */
export interface ServiceRequest extends MaintenanceAudit {
  issueId: string;
  assetId: string;
  assetName: string;
  reportedBy: string | null;
  /** Request type. */
  issueType: string;
  /** Priority. */
  severity: string;
  description: string | null;
  category: string | null;
  location: string | null;
  requestedDate: string | null;
  targetDate: string | null;
  assignedTo: string | null;
  workOrderId: string | null;
  resolvedDate: string | null;
  resolutionNotes: string | null;
}

/** /api/preventive-maintenance — PM schedule. */
export interface PreventiveMaintenance extends MaintenanceAudit {
  pmScheduleId: string;
  scheduleName: string | null;
  assetId: string;
  assetName: string;
  assetType: string | null;
  location: string | null;
  frequency: string;
  /** Maintenance type: Time / Usage / Condition / Calendar Based. */
  triggerType: string;
  lastMaintenanceDate: string | null;
  nextDueDate: string | null;
  autoCreateWorkOrder: boolean;
  priority: string | null;
  assignedTo: string | null;
  estimatedHours: number | null;
  description: string | null;
  checklist: string[];
}

export interface PredictiveAnomaly {
  detectedAt: string;
  metric: string;
  value: number;
  expected?: string | null;
  severity?: string | null;
  note?: string | null;
}

/** /api/predictive-maintenance — monitored asset with its rule and latest prediction. */
export interface PredictiveMaintenance extends MaintenanceAudit {
  assetId: string;
  assetName: string;
  assetType: string | null;
  location: string | null;
  sensorType: string;
  thresholdValue: number;
  alertCondition: string | null;
  dataSourceDeviceId: string | null;
  lastReading: number | null;
  lastReadingAt: string | null;
  predictionModelOutput: string | null;
  modelName: string | null;
  riskLevel: string;
  healthScore: number | null;
  predictedIssue: string | null;
  predictedFailureDate: string | null;
  confidence: number | null;
  recommendedAction: string | null;
  actualFailureDate: string | null;
  workOrderId: string | null;
  anomalies: PredictiveAnomaly[];
}

// ----- Pick lists (the API stores free text; these keep the UI consistent) -----

export const PRIORITIES = ['High', 'Medium', 'Low'];

export const WORK_TYPES = ['Preventive', 'Corrective', 'Predictive', 'Inspection', 'Service'];
export const WO_CATEGORIES = ['Routine', 'Breakdown', 'Emergency', 'Improvement', 'Statutory'];
/** Stored statuses. "Overdue" is derived from the due date, never stored. */
export const WO_STATUSES = ['Open', 'Scheduled', 'In Progress', 'On Hold', 'Completed', 'Cancelled'];
export const WO_SOURCES = ['Manual', 'PM Schedule', 'Service Request', 'Predictive Alert', 'Inspection'];

export const SR_TYPES = ['Repair Request', 'Service Request', 'Spare Part Request', 'Inspection Request'];
export const SR_CATEGORIES = ['Equipment Issue', 'Facility Issue', 'Spare Part Request', 'Inspection Request', 'Other'];
export const SR_STATUSES = ['Open', 'In Progress', 'Resolved', 'Closed', 'Rejected'];

export const PM_TYPES = ['Time Based', 'Usage Based', 'Condition Based', 'Calendar Based'];
export const PM_FREQUENCIES = ['Weekly', 'Monthly', 'Quarterly', 'Bi-Annual', 'Yearly', 'Every 250 Hours', 'Every 500 Hours', 'Every 1000 Hours'];
export const PM_STATUSES = ['Active', 'Paused', 'Inactive'];

export const RISK_LEVELS = ['High', 'At Risk', 'Healthy'];
export const SENSOR_TYPES = ['Vibration', 'Temperature', 'Pressure', 'Current', 'Voltage', 'Humidity', 'Oil Quality', 'Runtime Hours'];
export const ALERT_CONDITIONS = ['Above Threshold', 'Below Threshold', 'Rate of Change'];
export const PDM_STATUSES = ['Monitoring', 'Alert', 'Work Order Raised', 'Resolved'];
