/**
 * Detail data for each register asset (DEMO MODE): one JSON file per asset, named by its tag.
 * The files are loaded on demand, so only the asset being viewed is downloaded.
 * The pages copy what they load, so edits stay in the page and never change these files.
 */

// ===== Documents =====

/** One uploaded file of a document; the last entry is the current version. */
export interface DocVersion {
  version: string;
  /** Local date-time, yyyy-MM-ddTHH:mm. */
  uploadedAt: string;
  uploadedBy: string;
  /** Bytes. */
  size: number;
  note: string;
  /** Present for files uploaded in this session; the sample records have none. */
  file?: File;
}

export interface AssetDocument {
  id: number;
  name: string;
  type: string;
  stage: string;
  description: string;
  tags: string[];
  /** yyyy-MM-dd, or '' when the document does not expire. */
  expiryDate: string;
  draft: boolean;
  /** Empty for a draft that has no file yet. */
  versions: DocVersion[];
  /** Sample preview only: page count and the cover wording. */
  pages: number;
  issuer: string;
  coverTitle: string;
  coverSubtitle: string;
  showAssetImage: boolean;
  /** Asset the document belongs to; set when loaded, so documents of several assets can be listed together. */
  assetTag?: string;
}

// ===== Images =====

export interface ImageEvent {
  action: string;
  by: string;
  /** Local date-time, yyyy-MM-ddTHH:mm. */
  at: string;
}

export interface AssetImage {
  id: number;
  title: string;
  description: string;
  type: string;
  tags: string[];
  uploadedBy: string;
  /** Local date-time, yyyy-MM-ddTHH:mm. */
  uploadedAt: string;
  location: string;
  fileName: string;
  /** Bytes. */
  size: number;
  width: number;
  height: number;
  remarks: string;
  /** Public path of a sample image, or a blob: URL of a file uploaded in this session. */
  src: string;
  file?: File;
  history: ImageEvent[];
  /** Asset the image belongs to; set when loaded, so images of several assets can be listed together. */
  assetTag?: string;
  /** Upload the image came in with; images uploaded together share it. */
  batch?: string;
}

// ===== Status & condition =====

export type Condition = 'Excellent' | 'Good' | 'Fair' | 'Poor' | 'Critical';
export type AlertSeverity = 'Critical' | 'Warning';
export type AlertStatus = 'Open' | 'Monitoring' | 'Acknowledged' | 'Resolved';

export interface Assessment {
  id: number;
  /** yyyy-MM-dd */
  date: string;
  assessedBy: string;
  overall: Condition;
  healthScore: number;
  physical: Condition;
  performance: Condition;
  safety: Condition;
  remarks: string;
}

export interface TimelineEvent {
  date: string;
  title: string;
  by?: string;
  status?: string;
  condition?: Condition;
}

export interface Reading {
  label: string;
  value: number;
  unit: string;
  /** Normal operating band; outside it the reading is a warning. */
  min?: number;
  max?: number;
  /** Gauge scale for the bar; readings without one show no bar. */
  scaleMax?: number;
  decimals?: number;
}

export interface AlertItem {
  id: number;
  severity: AlertSeverity;
  message: string;
  raisedAt: string;
  status: AlertStatus;
}

export interface EnvReading {
  parameter: string;
  value: number;
  unit: string;
  limit: number;
  /** 'max': value must stay at or below the limit; 'min': at or above. */
  limitType: 'max' | 'min';
  measuredOn: string;
}

export interface MonthMetric {
  month: string;
  availability: number;
  utilization: number;
  runningHours: number;
  /** Fuel, energy or output for the month; the column heading is StatusData.consumptionLabel. */
  consumption: number;
  downtime: number;
  failures: number;
}

export interface OperationalDetails {
  status: string;
  statusSince: string;
  operatingMode: string;
  /** What the load figure measures for this kind of asset, e.g. "Current Load" (kW) or "Supply Airflow" (m³/h). */
  loadLabel: string;
  currentLoad: number;
  loadUnit: string;
  capacityLabel: string;
  ratedCapacity: number;
  ratedUnit: string;
  /**
   * Converts the rated capacity into load units for the "% of rated" figure (0.8 turns a generator's kVA into kW).
   * Null when the two cannot be compared, e.g. a laptop's battery charge against its capacity in Wh.
   */
  capacityFactor: number | null;
  runningHours: number;
  lastStart: string;
  lastStop: string;
  location: string;
  controlledBy: string;
  remarks: string;
}

export interface LifecycleInfo {
  stage: string;
  commissionedOn: string;
  expectedLifeYears: number;
  /** yyyy-MM-dd, or '' when none is planned. */
  nextAssessment: string;
  nextMaintenance: string;
}

export interface StatusData {
  availability: number;
  utilization: number;
  lastMaintenance: string;
  operational: OperationalDetails;
  operatingModes: string[];
  lifecycle: LifecycleInfo;
  readingsUpdatedAt: string;
  readings: Reading[];
  alerts: AlertItem[];
  environment: EnvReading[];
  consumptionLabel: string;
  performance: MonthMetric[];
  assessments: Assessment[];
  timeline: TimelineEvent[];
}

// ===== Relationships =====

/** Equipment linked to the asset that is not itself in the register (its location, parts, connected plant). */
export interface RelatedPart {
  tag: string;
  name: string;
  category: string;
  location: string;
  condition: 'Good' | 'Fair' | 'Poor' | '';
  /** Icon name from the Relationships page's icon set. */
  icon: string;
}

export interface RelationshipLink {
  id: number;
  /** Tag of a RelatedPart or of another register asset. */
  tag: string;
  type: 'Parent Asset' | 'Connected Asset' | 'Supporting Asset' | 'Component';
  direction: 'Parent' | 'Child' | 'Sibling';
  /** ISO dates (yyyy-mm-dd); `to` is empty while the link is open. */
  since: string;
  to: string;
  status: 'Active' | 'Inactive';
  description: string;
  reference: string;
  remarks: string;
}

// ===== Criticality =====

export type CriticalityLevel = 'Critical' | 'High' | 'Medium' | 'Low';
export type MitigationStatus = 'Open' | 'In Progress' | 'Completed';

/** One weighted criterion. Weighted score = weight × score ÷ 5, so the weights (summing to 100) give a total out of 100. */
export interface CriticalityCriterion {
  key: string;
  name: string;
  /** Percent. */
  weight: number;
  /** 1-5 in half steps. */
  score: number;
  justification: string;
}

export interface CriticalityImpact {
  downtimeCostPerHour: number;
  currency: string;
  /** Mean time to repair. */
  mttrHours: number;
  redundancy: string;
  spares: string;
  failureScenario: string;
  affectedServices: string[];
}

export interface Mitigation {
  id: number;
  action: string;
  owner: string;
  /** yyyy-MM-dd */
  dueDate: string;
  status: MitigationStatus;
  /** Criticality expected once the action is in place. */
  reducesTo: CriticalityLevel;
}

export interface CriticalityReview {
  date: string;
  score: number;
  level: CriticalityLevel;
  likelihood: number;
  consequence: number;
  assessedBy: string;
  approvedBy: string;
  remarks: string;
}

export interface CriticalityData {
  level: CriticalityLevel;
  /** Out of 100. */
  score: number;
  assessmentDate: string;
  validUntil: string;
  /** yyyy-MM-dd, or '' when no review is planned (e.g. a disposed asset). */
  nextReview: string;
  assessedBy: string;
  approvedBy: string;
  method: string;
  reviewFrequency: string;
  department: string;
  assetGroup: string;
  remarks: string;
  /** Position on the 5 × 5 risk matrix, 1 (very low) to 5 (very high). */
  likelihood: number;
  consequence: number;
  criteria: CriticalityCriterion[];
  impact: CriticalityImpact;
  mitigations: Mitigation[];
  /** Earlier assessments, newest first. */
  history: CriticalityReview[];
}

// ===== Health score =====

export type HealthStatus = 'Healthy' | 'Good' | 'Fair' | 'Poor' | 'Critical';

/** One part of the health score. The score is the weighted average of the parts (weights sum to 100). */
export interface HealthComponent {
  key: string;
  name: string;
  weight: number;
  /** Percent. */
  score: number;
}

export interface HealthRecord {
  /** yyyy-MM-dd */
  date: string;
  score: number;
  status: HealthStatus;
  trigger: 'Scheduled' | 'Event' | 'Manual';
  comments: string;
  assessedBy: string;
}

export interface HealthInsight {
  type: 'success' | 'info' | 'warning';
  text: string;
}

export interface MaintenanceTask {
  task: string;
  frequency: string;
  lastDone: string;
  /** yyyy-MM-dd, or '' when nothing is scheduled (asset stored or disposed). */
  nextDue: string;
  owner: string;
}

export interface HealthData {
  /** Out of 100. */
  score: number;
  status: HealthStatus;
  grade: string;
  assessmentDate: string;
  /** yyyy-MM-dd, or '' when none is planned. */
  nextAssessment: string;
  assessedBy: string;
  method: string;
  remarks: string;
  breakdown: HealthComponent[];
  /** Monthly assessments, newest first. */
  history: HealthRecord[];
  insights: HealthInsight[];
  maintenancePlan: MaintenanceTask[];
}

// ===== The file =====

export interface AssetDetail {
  tag: string;
  documents: AssetDocument[];
  /** Document opened in the preview pane on arrival. */
  previewDocumentId: number | null;
  images: AssetImage[];
  /** Image used as the asset photo in the Images page header. */
  headerImageId: number;
  status: StatusData;
  relationships: { parts: RelatedPart[]; links: RelationshipLink[] };
  criticality: CriticalityData;
  health: HealthData;
}

type Loader = () => Promise<{ default: unknown }>;

const LOADERS: Record<string, Loader> = {
  'AC-00311': () => import('./AC-00311.json'),
  'AC-00318': () => import('./AC-00318.json'),
  'CCTV-00234': () => import('./CCTV-00234.json'),
  'CCTV-00241': () => import('./CCTV-00241.json'),
  'FLT-00021': () => import('./FLT-00021.json'),
  'FLT-00033': () => import('./FLT-00033.json'),
  'FP-00056': () => import('./FP-00056.json'),
  'FP-00061': () => import('./FP-00061.json'),
  'GEN-00045': () => import('./GEN-00045.json'),
  'GEN-00052': () => import('./GEN-00052.json'),
  'HVAC-00112': () => import('./HVAC-00112.json'),
  'HVAC-00118': () => import('./HVAC-00118.json'),
  'IT-00189': () => import('./IT-00189.json'),
  'IT-00204': () => import('./IT-00204.json'),
  'SOL-00456': () => import('./SOL-00456.json'),
  'SOL-00462': () => import('./SOL-00462.json'),
  'TRF-00067': () => import('./TRF-00067.json'),
  'TRF-00072': () => import('./TRF-00072.json'),
  'UPS-00078': () => import('./UPS-00078.json'),
  'UPS-00081': () => import('./UPS-00081.json'),
};

const cache = new Map<string, Promise<AssetDetail | null>>();

/** Loads an asset's detail data; resolves to null when the asset has no data file. */
export function loadAssetDetail(tag: string): Promise<AssetDetail | null> {
  let pending = cache.get(tag);
  if (!pending) {
    const loader = LOADERS[tag];
    // Wrapped in the global Promise so the result is seen by Angular's change detection
    pending = loader
      ? Promise.resolve(loader()).then(
          (m) => m.default as AssetDetail,
          () => {
            cache.delete(tag); // let a later visit retry a failed download
            return null;
          },
        )
      : Promise.resolve(null);
    cache.set(tag, pending);
  }
  return pending;
}

/** A deep copy the pages can edit freely. */
export function cloneDetail<T>(value: T): T {
  return structuredClone(value);
}
