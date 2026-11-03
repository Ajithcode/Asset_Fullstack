import { CustomField, FieldScope, FieldType } from './custom-fields.model';

const ALL_COMPONENTS = 'All Components';

const G_GENERAL = 'General Information';
const G_PURCHASE = 'Purchase & Warranty';
const G_TECH = 'Technical Specifications';
const G_LOCATION = 'Location & Assignment';
const G_COMPLIANCE = 'Compliance & Certification';
const G_OPERATIONAL = 'Operational Details';
const G_FINANCIAL = 'Financial Information';
const G_MAINTENANCE = 'Maintenance Information';

type Seed = [
  scope: FieldScope,
  name: string,
  label: string,
  type: FieldType,
  group: string,
  required: boolean,
  extra?: Partial<Pick<CustomField, 'appliesTo' | 'options' | 'parts' | 'lookupSource' | 'description' | 'tooltip' | 'showInDetails' | 'searchable' | 'status' | 'defaultValue'>>,
];

const SEEDS: Seed[] = [
  ['asset', 'warranty_expiry', 'Warranty Expiry Date', 'Date', G_PURCHASE, true, { description: 'Warranty expiry date provided by manufacturer', searchable: true }],
  ['asset', 'asset_condition', 'Current Condition', 'Dropdown', G_GENERAL, true, { options: ['New', 'Good', 'Fair', 'Poor'], defaultValue: 'Good', showInDetails: true }],
  ['asset', 'installation_date', 'Installation Date', 'Date', G_GENERAL, false, { showInDetails: true }],
  ['asset', 'power_rating', 'Power Rating (kVA)', 'Number', G_TECH, true, { showInDetails: true, searchable: true }],
  ['asset', 'voltage', 'Voltage (V)', 'Number', G_TECH, false],
  ['asset', 'serial_number', 'Serial Number', 'Text', G_GENERAL, true, { showInDetails: true, searchable: true }],
  ['asset', 'cost_center', 'Cost Center', 'Dropdown', G_FINANCIAL, false, { options: ['CC-100 Operations', 'CC-200 Facilities', 'CC-300 IT'] }],
  ['asset', 'criticality', 'Criticality', 'Dropdown', G_GENERAL, false, { options: ['Critical', 'High', 'Medium', 'Low'], defaultValue: 'Medium', searchable: true }],
  ['asset', 'location_zone', 'Location Zone', 'Text', G_LOCATION, false],
  ['asset', 'last_calibration', 'Last Calibration Date', 'Date', G_COMPLIANCE, false],
  ['asset', 'next_calibration', 'Next Calibration Date', 'Date', G_COMPLIANCE, false],
  ['asset', 'frequency', 'Frequency (Hz)', 'Number', G_TECH, false, { defaultValue: '50' }],
  ['asset', 'manufacturer', 'Manufacturer', 'Text', G_GENERAL, false, { searchable: true, showInDetails: true }],
  ['asset', 'model', 'Model', 'Text', G_GENERAL, false, { searchable: true, showInDetails: true }],
  ['asset', 'engine_type', 'Engine Type', 'Text', G_TECH, false],
  ['asset', 'cooling_system', 'Cooling System', 'Dropdown', G_TECH, false, { options: ['Liquid Cooled', 'Air Cooled', 'Radiator', 'Heat Exchanger'] }],
  ['asset', 'fuel_type', 'Fuel Type', 'Dropdown', G_TECH, false, { options: ['Diesel', 'Natural Gas', 'Petrol', 'LPG', 'Bi-Fuel'], defaultValue: 'Diesel' }],
  ['asset', 'emissions_standard', 'Emissions Standard', 'Text', G_TECH, false],
  ['asset', 'efficiency', 'Efficiency (%)', 'Number', G_TECH, false],
  ['asset', 'purchase_date', 'Purchase Date', 'Date', G_PURCHASE, false, { showInDetails: true }],
  ['asset', 'warranty_period', 'Warranty Period (Months)', 'Number', G_PURCHASE, false, { defaultValue: '12' }],
  ['asset', 'warranty_provider', 'Warranty Provider', 'Text', G_PURCHASE, false],
  ['asset', 'supplier', 'Supplier', 'Lookup', G_PURCHASE, false, { lookupSource: 'Suppliers', searchable: true }],
  ['asset', 'gps_coordinates', 'GPS Coordinates', 'Compound', G_LOCATION, false, { parts: ['Latitude', 'Longitude'] }],
  ['asset', 'assigned_department', 'Assigned Department', 'Dropdown', G_LOCATION, false, { options: ['Operations', 'Facilities', 'IT', 'Logistics', 'Security'] }],
  ['asset', 'custodian', 'Custodian', 'Lookup', G_LOCATION, false, { lookupSource: 'Users' }],
  ['asset', 'building_floor', 'Building / Floor', 'Text', G_LOCATION, false],
  ['asset', 'certification_no', 'Certification No.', 'Text', G_COMPLIANCE, false, { searchable: true }],
  ['asset', 'purchase_order_no', 'Purchase Order No.', 'Text', G_FINANCIAL, false, { searchable: true }],
  ['asset', 'insured', 'Insured', 'Checkbox', G_FINANCIAL, false, { defaultValue: 'false', status: 'Inactive' }],
  ['asset', 'depreciation_method', 'Depreciation Method', 'Dropdown', G_FINANCIAL, false, { options: ['Straight Line', 'Declining Balance', 'Units of Production'] }],
  ['asset', 'purchase_cost', 'Purchase Cost (AED)', 'Number', G_FINANCIAL, false],
  ['asset', 'maintenance_frequency', 'Maintenance Frequency', 'Dropdown', G_MAINTENANCE, false, { options: ['Weekly', 'Monthly', 'Quarterly', 'Half-Yearly', 'Yearly'] }],
  ['asset', 'last_service_date', 'Last Service Date', 'Date', G_MAINTENANCE, false],
  ['asset', 'next_service_date', 'Next Service Date', 'Date', G_MAINTENANCE, false],
  ['asset', 'service_provider', 'Service Provider', 'Lookup', G_MAINTENANCE, false, { lookupSource: 'Suppliers' }],
  ['asset', 'amc_contract_no', 'AMC Contract No.', 'Text', G_MAINTENANCE, false],
  ['asset', 'runtime_hours', 'Runtime Hours', 'Number', G_MAINTENANCE, false],
  ['asset', 'maintenance_notes', 'Maintenance Notes', 'Text Area', G_MAINTENANCE, false],
  // Component fields have their own form and aren't part of the asset field groups.
  ['component', 'serial_number', 'Serial Number', 'Text', '', true, { showInDetails: true, tooltip: 'Enter the manufacturer serial number of the component.' }],
  ['component', 'manufacturing_date', 'Manufacturing Date', 'Date', '', false, { showInDetails: true }],
  ['component', 'warranty_period', 'Warranty Period (Months)', 'Number', '', false, { showInDetails: true, defaultValue: '12' }],
  ['component', 'material', 'Material', 'Dropdown', '', false, { options: ['Cast Iron', 'Steel', 'Stainless Steel', 'Aluminium', 'Copper', 'Composite'] }],
  ['component', 'dimensions', 'Dimensions (L x W x H)', 'Compound', '', false, { parts: ['Length (mm)', 'Width (mm)', 'Height (mm)'], showInDetails: true }],
  ['component', 'weight', 'Weight (kg)', 'Number', '', false, { showInDetails: true }],
  ['component', 'operating_temperature', 'Operating Temperature', 'Text', '', false, { tooltip: 'Range in °C, e.g. -20 to 55' }],
  ['component', 'ip_rating', 'IP Rating', 'Dropdown', '', false, { options: ['IP44', 'IP54', 'IP55', 'IP65', 'IP66', 'IP67'] }],
  ['component', 'supplier', 'Supplier', 'Lookup', '', false, { lookupSource: 'Suppliers', searchable: true }],
  ['component', 'installation_position', 'Installation Position', 'Dropdown', '', false, { options: ['Front', 'Rear', 'Left', 'Right', 'Top', 'Bottom', 'Internal'] }],
  ['category', 'fuel_tank_capacity', 'Fuel Tank Capacity (L)', 'Number', G_OPERATIONAL, true, { appliesTo: 'Power Generation' }],
  ['category', 'rated_output', 'Rated Output', 'Compound', G_OPERATIONAL, true, { appliesTo: 'Power Generation', parts: ['kVA', 'kW', 'Power Factor'] }],
  ['category', 'cooling_capacity_tr', 'Cooling Capacity (TR)', 'Number', G_OPERATIONAL, true, { appliesTo: 'HVAC' }],
  ['category', 'ram_gb', 'RAM (GB)', 'Number', G_OPERATIONAL, false, { appliesTo: 'IT Equipment' }],
  ['category', 'ip_address', 'IP Address', 'Text', G_OPERATIONAL, false, { appliesTo: 'IT Equipment', searchable: true }],
  ['category', 'ip_rating', 'IP Rating', 'Dropdown', G_COMPLIANCE, false, { appliesTo: 'Electrical Equipment', options: ['IP54', 'IP55', 'IP65', 'IP66'] }],
  ['category', 'load_capacity_kg', 'Load Capacity (kg)', 'Number', G_OPERATIONAL, true, { appliesTo: 'Material Handling' }],
];

/**
 * Sample field definitions used by the asset detail Custom Fields tab (outside this module) for its sample values.
 * The Custom Fields settings page itself loads its fields from the Asset API.
 */
export function buildMockFields(): CustomField[] {
  const counters = new Map<string, number>();
  return SEEDS.map(([scope, name, label, type, group, required, extra = {}], i) => {
    const key = `${scope}|${group}`;
    const order = counters.get(key) ?? 0;
    counters.set(key, order + 1);
    return {
      id: `mock-${i + 1}`,
      scope,
      appliesTo: extra.appliesTo ?? (scope === 'component' ? ALL_COMPONENTS : ''),
      appliesToId: '',
      name,
      label,
      type,
      group,
      groupId: '',
      required,
      showInDetails: extra.showInDetails ?? false,
      searchable: extra.searchable ?? false,
      defaultValue: extra.defaultValue ?? '',
      options: extra.options ?? [],
      parts: extra.parts ?? [],
      partDefs: (extra.parts ?? []).map((label, k) => ({ key: `part_${k + 1}`, label, unit: '' })),
      lookupSource: extra.lookupSource ?? '',
      description: extra.description ?? '',
      tooltip: extra.tooltip ?? '',
      status: extra.status ?? 'Active',
      order,
    };
  });
}

