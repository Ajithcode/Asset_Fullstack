export type MasterFieldType = 'text' | 'number' | 'date' | 'time' | 'email' | 'color' | 'select' | 'boolean' | 'textarea';

export interface MasterField {
  key: string;
  label: string;
  type?: MasterFieldType;
  // Choices for a 'select' field
  options?: string[];
  required?: boolean;
  // Keep the field in the form but out of the table (long text, notes)
  hideInTable?: boolean;
}

export type MasterValue = string | number | boolean | null;

export interface MasterRecord {
  code: string;
  status: 'Active' | 'Inactive';
  [key: string]: MasterValue;
}

/**
 * Describes one master data page. Every page gets a Code column (auto-generated from
 * `codePrefix` when left blank) and a Status column; `fields` are the columns in between.
 */
export interface MasterTableConfig {
  // Unique store key, e.g. 'organization/site'
  key: string;
  title: string;
  // Used in buttons and dialogs, e.g. 'Site' in "Add Site"
  singular: string;
  codePrefix: string;
  fields: MasterField[];
  // Demo rows the page starts with until the API is wired up
  rows: MasterRecord[];
}
