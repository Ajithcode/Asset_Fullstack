import { AssetTypeRecord, CustomFieldType, MaintenanceType } from './asset-type.model';

export const ASSET_CLASSES = [
  'Movable Asset',
  'Fixed Asset',
  'Infrastructure',
  'Plant & Machinery',
  'Rolling Stock',
  'Building Services',
  'Tools & Equipment',
  'Furniture & Fixtures',
  'Leased Asset',
  'Intangible Asset',
  'Consumable',
  'Leasehold Improvement'
];

export const MAINTENANCE_TYPES: MaintenanceType[] = ['Preventive + Corrective', 'Preventive + Predictive', 'Preventive', 'Corrective', 'Condition Based'];
export const CUSTOM_FIELD_TYPES: CustomFieldType[] = ['Text', 'Number', 'Date', 'Dropdown', 'Yes / No'];

// Cost center, service provider and capitalization threshold (AED) for each category
const CATEGORY_DEFAULTS: Record<string, [string, string, number]> = {
  'IT Equipment': ['IT-001', 'IT Vendor (Default)', 5000],
  'Network Equipment': ['NET-001', 'Network Integrator (Default)', 5000],
  Security: ['SEC-001', 'Security Systems Vendor', 3000],
  'Safety & Security': ['HSE-001', 'Fire & Safety Contractor', 1000],
  Machinery: ['MFG-001', 'OEM Service (Default)', 25000],
  Vehicles: ['FLT-001', 'Fleet Service Center', 20000],
  HVAC: ['FM-001', 'HVAC Contractor (Default)', 10000],
  Electrical: ['ELE-001', 'Electrical Contractor', 10000],
  Facilities: ['FM-002', 'FM Contractor (Default)', 10000],
  Plumbing: ['FM-002', 'FM Contractor (Default)', 5000],
  Tools: ['TLS-001', 'In-house Workshop', 2000],
  Furniture: ['ADM-001', 'Office Supplies Vendor', 2000],
  'Office Equipment': ['ADM-001', 'Office Supplies Vendor', 2000],
  'Laboratory Equipment': ['QA-001', 'Lab Equipment Vendor', 5000],
  'Medical Equipment': ['HSE-001', 'Biomedical Service Vendor', 5000],
  Software: ['IT-001', 'IT Vendor (Default)', 10000]
};

export const TYPE_COST_CENTERS = [...new Set(Object.values(CATEGORY_DEFAULTS).map((d) => d[0]))];
export const SERVICE_PROVIDERS = [...new Set(Object.values(CATEGORY_DEFAULTS).map((d) => d[1]))];

export function blankAssetType(): AssetTypeRecord {
  return {
    code: '',
    name: '',
    category: '',
    assetClass: '',
    icon: 'conveyor',
    image: null,
    description: '',
    status: 'Active',
    subcategories: [],
    taggingFormat: '',
    lifecycleYears: 5,
    maintenanceType: 'Preventive',
    maintenancePlan: '',
    warrantyMonths: 12,
    serviceProvider: '',
    depreciationMethod: 'Straight Line',
    usefulLifeMonths: 60,
    residualPercent: 5,
    costCenter: '',
    currency: 'AED - UAE Dirham',
    capitalizationThreshold: 5000,
    procurementType: 'CAPEX',
    defaultCondition: 'New',
    trackingMethod: 'RFID',
    serialRequired: true,
    assignable: false,
    customFields: [],
    inspectionRequired: false,
    allowTransfers: true,
    iotTracking: false,
    disposalApproval: true,
    openWorkOrders: 0,
    documents: 0,
    createdOn: '',
    createdBy: '',
    updatedOn: ''
  };
}

/** Cost center and service provider usually used for a category. */
export function categoryDefaults(category: string): { costCenter: string; serviceProvider: string; threshold: number } | null {
  const d = CATEGORY_DEFAULTS[category];
  return d ? { costCenter: d[0], serviceProvider: d[1], threshold: d[2] } : null;
}

