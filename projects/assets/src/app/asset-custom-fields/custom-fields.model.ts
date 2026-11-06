export type FieldScope = 'asset' | 'component' | 'category';
export type FieldType = 'Text' | 'Text Area' | 'Number' | 'Date' | 'Dropdown' | 'Checkbox' | 'Compound' | 'Lookup';
export type FieldStatus = 'Active' | 'Inactive';

export interface CustomField {
  id: number;
  scope: FieldScope;
  /** Asset category (scope 'category') or component type (scope 'component'); empty for asset fields. */
  appliesTo: string;
  /** Machine name: lowercase letters, digits and underscores. */
  name: string;
  label: string;
  type: FieldType;
  /** Field group name (shown as "Category" in the field lists); empty when unassigned. */
  group: string;
  required: boolean;
  showInDetails: boolean;
  searchable: boolean;
  defaultValue: string;
  /** Choices for Dropdown fields. */
  options: string[];
  /** Sub-field labels for Compound fields, e.g. Length / Width / Height. */
  parts: string[];
  /** Record list a Lookup field picks from. */
  lookupSource: string;
  description: string;
  /** Help text shown next to the input on the form. */
  tooltip: string;
  status: FieldStatus;
  /** Position within its group. */
  order: number;
}

export interface FieldGroup {
  name: string;
  description: string;
  status: FieldStatus;
  /** Position in the groups list. */
  order: number;
}

export interface LayoutSection {
  /** Field group name the section is for. */
  group: string;
  fieldIds: number[];
}

export interface FieldLayout {
  id: number;
  name: string;
  assetType: string;
  description: string;
  status: FieldStatus;
  sections: LayoutSection[];
}

export const ASSET_TYPES = ['Generator', 'Electrical', 'HVAC', 'IT Asset', 'Vehicle', 'All Assets'];

/** Asset category whose category-specific fields a layout's asset type can use. */
export const ASSET_TYPE_CATEGORY: Record<string, string> = {
  Generator: 'Power Generation',
  Electrical: 'Electrical Equipment',
  HVAC: 'HVAC',
  'IT Asset': 'IT Equipment',
  Vehicle: 'Material Handling',
  'All Assets': '',
};

/** Sample records for each lookup source, used for default values and form previews. */
export const LOOKUP_SOURCES: Record<string, string[]> = {
  Suppliers: ['Al Masaood Power', 'Cummins Arabia', 'Gulf Power Parts', 'Emirates Industrial Supply'],
  Manufacturers: ['Cummins', 'Caterpillar', 'Siemens', 'ABB', 'Schneider Electric'],
  Locations: ['Dubai HQ', 'Jebel Ali Warehouse', 'Abu Dhabi Plant', 'Sharjah Depot'],
  Users: ['Rex Kumar', 'Aisha Rahman', 'Omar Haddad', 'Priya Nair'],
};

function systemField(id: number, name: string, label: string): CustomField {
  return {
    id, scope: 'asset', appliesTo: '', name, label, type: 'Text', group: 'General Information', required: true,
    showInDetails: true, searchable: true, defaultValue: '', options: [], parts: [], lookupSource: '',
    description: 'Built-in asset field', tooltip: '', status: 'Active', order: 0,
  };
}

/** Built-in asset fields every layout must include; negative ids keep them apart from custom fields. */
export const SYSTEM_FIELDS: CustomField[] = [systemField(-1, 'asset_name', 'Asset Name'), systemField(-2, 'asset_tag', 'Asset Tag / ID')];

export function typeClass(type: FieldType): string {
  return 'type--' + type.toLowerCase().replace(/\s+/g, '-');
}
