export type FieldScope = 'asset' | 'component' | 'category';
export type FieldType = 'Text' | 'Text Area' | 'Number' | 'Date' | 'Dropdown' | 'Checkbox' | 'Compound' | 'Lookup';
export type FieldStatus = 'Active' | 'Inactive';

export interface CompoundPart {
  key: string;
  label: string;
  unit: string;
}

export interface CustomField {
  /** API id (Mongo ObjectId); built-in asset fields use "builtin:<name>". */
  id: string;
  scope: FieldScope;
  /** Asset category name (scope 'category') or component type (scope 'component'); empty for asset fields. */
  appliesTo: string;
  /** Asset category id for category fields; empty otherwise. */
  appliesToId: string;
  /** Machine name: lowercase letters, digits and underscores. */
  name: string;
  label: string;
  type: FieldType;
  /** Field group name (shown as "Category" in the field lists); empty when unassigned. */
  group: string;
  /** Field group id; empty when unassigned. */
  groupId: string;
  required: boolean;
  showInDetails: boolean;
  searchable: boolean;
  defaultValue: string;
  /** Choices for Dropdown fields. */
  options: string[];
  /** Sub-field labels for Compound fields, e.g. Length / Width / Height. */
  parts: string[];
  /** Stored sub-field definitions (keys and units are kept across edits). */
  partDefs: CompoundPart[];
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
  id: string;
  name: string;
  description: string;
  status: FieldStatus;
  /** Position in the groups list. */
  order: number;
}

export interface LayoutSection {
  /** Field group id the section is for. */
  groupId: string;
  /** Field group name the section is for. */
  group: string;
  fieldIds: string[];
}

export interface FieldLayout {
  id: string;
  name: string;
  /** Asset type id; empty for a layout that applies to all assets. */
  assetTypeId: string;
  /** Asset type name, or "All Assets". */
  assetType: string;
  description: string;
  status: FieldStatus;
  sections: LayoutSection[];
}

export const ALL_ASSETS = 'All Assets';

/** Lookup sources a field can read from; their records are empty until a master data API serves them. */
export const LOOKUP_SOURCES: Record<string, string[]> = {
  Suppliers: [],
  Manufacturers: [],
  Locations: [],
  Users: [],
};

export const BUILT_IN_PREFIX = 'builtin:';

export function isBuiltIn(f: Pick<CustomField, 'id'>): boolean {
  return f.id.startsWith(BUILT_IN_PREFIX);
}

/** A standard (built-in) asset field as a CustomField, so layouts can place it like any other. */
export function builtInField(name: string, label: string, type: FieldType = 'Text', required = false): CustomField {
  return {
    id: BUILT_IN_PREFIX + name, scope: 'asset', appliesTo: '', appliesToId: '', name, label, type, group: '', groupId: '', required,
    showInDetails: true, searchable: true, defaultValue: '', options: [], parts: [], partDefs: [], lookupSource: '',
    description: 'Built-in asset field', tooltip: '', status: 'Active', order: 0,
  };
}

/** Built-in asset fields every layout must include. */
export const SYSTEM_FIELDS: CustomField[] = [builtInField('asset_name', 'Asset Name', 'Text', true), builtInField('asset_tag', 'Asset Tag / ID', 'Text', true)];

/** Standard asset fields layouts can place (the API's list; replaced by GET available-fields once loaded). */
export const BUILT_IN_FIELDS: CustomField[] = [
  ...SYSTEM_FIELDS,
  builtInField('barcode', 'Barcode / QR Code'),
  builtInField('rfid_tag_id', 'RFID Tag ID'),
  builtInField('model', 'Model'),
  builtInField('manufacturer', 'Manufacturer'),
];

export function typeClass(type: FieldType): string {
  return 'type--' + type.toLowerCase().replace(/\s+/g, '-');
}
