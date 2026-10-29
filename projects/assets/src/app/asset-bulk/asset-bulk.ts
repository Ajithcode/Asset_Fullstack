import { Component, HostListener, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import * as XLSX from 'xlsx';
import {
  LucideAngularModule,
  ArrowLeft,
  ArrowRight,
  Calendar,
  Check,
  ChevronDown,
  CircleAlert,
  CircleCheck,
  CircleX,
  CloudUpload,
  Download,
  EllipsisVertical,
  FileDown,
  FileSpreadsheet,
  FileUp,
  History,
  Info,
  ListChecks,
  Plus,
  Table2,
  Trash2,
  Wand2,
  X,
} from 'lucide-angular';
import { ASSET_RECORDS, AssetIconKey, AssetRecord, AssetStatus, Criticality, assetLocation } from '../asset-data';
import { readUserRelationships } from '../asset-relationship-list/user-relationships';

type ImportType = 'create' | 'update' | 'upsert';
type ExportType = 'master' | 'related' | 'custom';
type FieldSet = 'standard' | 'all' | 'custom';
type FileFormat = 'xlsx' | 'csv';
type DataType = 'Text' | 'Lookup' | 'Date';
type FieldKey =
  | 'tag' | 'name' | 'category' | 'type' | 'manufacturer' | 'model' | 'serialNumber' | 'purchaseDate'
  | 'site' | 'area' | 'custodian' | 'department' | 'status' | 'criticality' | 'nextMaintenance';

interface FieldDef {
  key: FieldKey;
  label: string;
  type: DataType;
  mandatory: boolean;
  /** Other column headings that map to this field automatically. */
  aliases: string[];
}

interface TemplateDef {
  name: string;
  fields: FieldKey[];
}

/** One column of the uploaded file and the Asset360 field it fills. */
interface ColumnMap {
  header: string;
  field: FieldKey | '';
  sample: string;
}

/** A field given one value for every row ("Add More Fields"). */
interface FixedValue {
  field: FieldKey | '';
  value: string;
}

interface RowIssue {
  row: number;
  tag: string;
  kind: 'Invalid' | 'Duplicate';
  message: string;
}

interface CleanRow {
  row: number;
  values: Partial<Record<FieldKey, string>>;
  /** True when the Asset ID is already in the register. */
  exists: boolean;
}

interface HistoryEntry {
  fileName: string;
  type: 'Import' | 'Export';
  records: number;
  status: 'Completed' | 'Failed';
  /** Local date-time, yyyy-MM-ddTHH:mm. */
  at: string;
  by: string;
}

const CURRENT_USER = 'Rex Kumar';
const MAX_FILE_BYTES = 10 * 1024 * 1024;
const STATUSES: AssetStatus[] = ['Active', 'Under Maintenance', 'In Repair', 'Disposed', 'In Storage', 'Checked Out'];
const CRITICALITIES: Criticality[] = ['Critical', 'High', 'Medium', 'Low'];

const FIELDS: FieldDef[] = [
  { key: 'tag', label: 'Asset ID', type: 'Text', mandatory: true, aliases: ['asset tag', 'tag', 'asset code', 'id'] },
  { key: 'name', label: 'Asset Name', type: 'Text', mandatory: true, aliases: ['name', 'description'] },
  { key: 'category', label: 'Category', type: 'Lookup', mandatory: true, aliases: ['asset category'] },
  { key: 'type', label: 'Asset Type', type: 'Lookup', mandatory: true, aliases: ['type'] },
  { key: 'manufacturer', label: 'Manufacturer', type: 'Text', mandatory: false, aliases: ['make', 'brand'] },
  { key: 'model', label: 'Model', type: 'Text', mandatory: false, aliases: ['model number'] },
  { key: 'serialNumber', label: 'Serial Number', type: 'Text', mandatory: false, aliases: ['serial', 'serial no', 's/n'] },
  { key: 'purchaseDate', label: 'Purchase Date', type: 'Date', mandatory: false, aliases: ['purchased', 'date of purchase'] },
  { key: 'site', label: 'Site', type: 'Lookup', mandatory: false, aliases: ['location', 'site name'] },
  { key: 'area', label: 'Area', type: 'Text', mandatory: false, aliases: ['sub location', 'building', 'room'] },
  { key: 'custodian', label: 'Custodian', type: 'Text', mandatory: false, aliases: ['owner', 'assigned to'] },
  { key: 'department', label: 'Department', type: 'Text', mandatory: false, aliases: ['dept'] },
  { key: 'status', label: 'Status', type: 'Lookup', mandatory: false, aliases: ['asset status'] },
  { key: 'criticality', label: 'Criticality', type: 'Lookup', mandatory: false, aliases: ['priority'] },
  { key: 'nextMaintenance', label: 'Next Maintenance', type: 'Date', mandatory: false, aliases: ['next service', 'maintenance due'] },
];

const TEMPLATES: TemplateDef[] = [
  { name: 'Asset Master Template (Basic)', fields: ['tag', 'name', 'category', 'type', 'manufacturer', 'model', 'serialNumber', 'purchaseDate', 'site', 'area', 'status'] },
  { name: 'Asset Master Template (Full)', fields: FIELDS.map((f) => f.key) },
  { name: 'Asset Assignment Template', fields: ['tag', 'name', 'category', 'type', 'custodian', 'department', 'site', 'area'] },
];

const STANDARD_EXPORT: FieldKey[] = ['tag', 'name', 'category', 'type', 'manufacturer', 'model', 'site', 'area', 'custodian', 'status', 'criticality'];

/** DEMO MODE: kept in memory until the Assets API is wired up. */
const HISTORY: HistoryEntry[] = [
  { fileName: 'Assets_Jan2026.xlsx', type: 'Import', records: 253, status: 'Completed', at: '2026-01-01T14:30', by: 'Rex Kumar' },
  { fileName: 'Assets_Update.xlsx', type: 'Import', records: 120, status: 'Completed', at: '2025-12-15T09:20', by: 'Sara Ali' },
  { fileName: 'Assets.csv', type: 'Import', records: 78, status: 'Failed', at: '2025-12-10T11:45', by: 'Rex Kumar' },
  { fileName: 'Asset_Export.xlsx', type: 'Export', records: 1245, status: 'Completed', at: '2025-12-05T16:10', by: 'Omar Khan' },
  { fileName: 'Assets_Decom.xlsx', type: 'Import', records: 45, status: 'Completed', at: '2025-11-20T10:05', by: 'Rex Kumar' },
];

const norm = (s: string) => s.toLowerCase().replace(/[^a-z0-9/]+/g, ' ').trim();
const pad = (n: number) => String(n).padStart(2, '0');

function nowLocal(): string {
  const d = new Date();
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

/** yyyy-mm-dd, dd/mm/yyyy or dd-mm-yyyy → yyyy-mm-dd; null when it is not a real date. */
function parseDate(text: string): string | null {
  let y: number, m: number, d: number;
  let match = /^(\d{4})-(\d{1,2})-(\d{1,2})$/.exec(text);
  if (match) [y, m, d] = [Number(match[1]), Number(match[2]), Number(match[3])];
  else if ((match = /^(\d{1,2})[/.-](\d{1,2})[/.-](\d{4})$/.exec(text))) [d, m, y] = [Number(match[1]), Number(match[2]), Number(match[3])];
  else return null;
  const date = new Date(y, m - 1, d);
  if (date.getFullYear() !== y || date.getMonth() !== m - 1 || date.getDate() !== d) return null;
  return `${y}-${pad(m)}-${pad(d)}`;
}

/** Cell value as text; Excel dates come through as Date objects. */
function cellText(v: unknown): string {
  if (v instanceof Date) return `${v.getFullYear()}-${pad(v.getMonth() + 1)}-${pad(v.getDate())}`;
  return v === null || v === undefined ? '' : String(v).trim();
}

@Component({
  selector: 'app-asset-bulk',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule],
  templateUrl: './asset-bulk.html',
  // Split to keep each stylesheet under the anyComponentStyle budget
  styleUrls: ['./asset-bulk.css', './asset-bulk-panels.css'],
})
export class AssetBulk implements OnDestroy {
  readonly icons = {
    ArrowLeft, ArrowRight, Calendar, Check, ChevronDown, CircleAlert, CircleCheck, CircleX, CloudUpload, Download, EllipsisVertical,
    FileDown, FileSpreadsheet, FileUp, History, Info, ListChecks, Plus, Table2, Trash2, Wand2, X,
  };
  readonly fields = FIELDS;
  readonly templates = TEMPLATES;
  readonly steps = ['Download Template', 'Upload File', 'Map Fields', 'Validate', 'Import'];
  readonly categories = [...new Set(ASSET_RECORDS.map((a) => a.category))].sort();
  readonly sites = [...new Set(ASSET_RECORDS.map((a) => a.site))].sort();
  readonly statuses = STATUSES;
  readonly criticalities = CRITICALITIES;

  // ===== Import =====
  step = 1;
  importType: ImportType = 'create';
  templateName = TEMPLATES[0].name;
  isDragging = false;
  file: File | null = null;
  fileError = '';
  /** Data rows of the uploaded sheet (without the heading row). */
  private rows: string[][] = [];
  columns: ColumnMap[] = [];
  fixed: FixedValue[] = [];
  mappingError = '';
  mapMenuOpen = false;

  validated = false;
  clean: CleanRow[] = [];
  issues: RowIssue[] = [];

  // ===== Export =====
  exportType: ExportType = 'master';
  fieldSet: FieldSet = 'standard';
  exportFields = new Set<FieldKey>(STANDARD_EXPORT);
  filterCategory = '';
  filterSite = '';
  filterStatus = '';
  dateFrom = '';
  dateTo = '';
  advancedOpen = false;
  filterCriticality = '';
  filterDepartment = '';
  exportFormat: FileFormat = 'xlsx';

  // ===== Dialogs =====
  historyOpen = false;
  issuesOpen = false;
  get history(): HistoryEntry[] {
    return HISTORY;
  }

  toast = '';
  toastError = false;
  private toastTimer?: ReturnType<typeof setTimeout>;

  ngOnDestroy(): void {
    clearTimeout(this.toastTimer);
  }

  // ===== Lookups =====

  field(key: FieldKey | ''): FieldDef | undefined {
    return FIELDS.find((f) => f.key === key);
  }

  readonly fieldLabel = (f: FieldDef): string => f.label;

  get template(): TemplateDef {
    return TEMPLATES.find((t) => t.name === this.templateName) ?? TEMPLATES[0];
  }

  get departments(): string[] {
    return [...new Set(ASSET_RECORDS.map((a) => a.department))].sort();
  }

  // ===== Step 1: template =====

  downloadTemplate(): void {
    const keys = this.template.fields;
    const sample = ASSET_RECORDS.slice(0, 3).map((a) =>
      keys.map((k) => {
        if (k === 'serialNumber') return a.serialNumber ?? `SN-${a.tag}`;
        if (k === 'purchaseDate') return a.purchaseDate ?? '2026-01-15';
        const v = (a as unknown as Record<string, unknown>)[k];
        return v === null || v === undefined ? '' : String(v);
      }),
    );
    const sheet = XLSX.utils.aoa_to_sheet([keys.map((k) => this.field(k)!.label + (this.field(k)!.mandatory ? ' *' : '')), ...sample]);
    sheet['!cols'] = keys.map(() => ({ wch: 20 }));
    const book = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(book, sheet, 'Assets');
    const notes = XLSX.utils.aoa_to_sheet([
      ['Field', 'Data Type', 'Mandatory', 'Allowed values / format'],
      ...keys.map((k) => {
        const f = this.field(k)!;
        const allowed = k === 'status' ? STATUSES.join(', ') : k === 'criticality' ? CRITICALITIES.join(', ') : k === 'category' ? this.categories.join(', ')
          : f.type === 'Date' ? 'yyyy-mm-dd or dd/mm/yyyy' : '';
        return [f.label, f.type, f.mandatory ? 'Yes' : 'No', allowed];
      }),
    ]);
    XLSX.utils.book_append_sheet(book, notes, 'Instructions');
    XLSX.writeFile(book, 'Assets_Import_Template.xlsx');
    if (this.step < 2) this.step = 2;
    this.showToast('Template downloaded. Fill it in and upload it here.');
  }

  // ===== Step 2: upload =====

  onFileInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.readFile(input.files?.[0]);
    input.value = '';
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    this.isDragging = false;
    this.readFile(event.dataTransfer?.files?.[0]);
  }

  private readFile(file: File | undefined): void {
    if (!file) return;
    if (!/\.(xlsx|xls|csv)$/i.test(file.name)) {
      this.fileError = 'Choose an Excel (.xlsx) or CSV (.csv) file.';
      return;
    }
    if (file.size > MAX_FILE_BYTES) {
      this.fileError = 'Files can be up to 10 MB.';
      return;
    }
    this.fileError = '';
    file.arrayBuffer().then(
      (buffer) => this.parse(file, buffer),
      () => (this.fileError = 'The file could not be read.'),
    );
  }

  private parse(file: File, buffer: ArrayBuffer): void {
    try {
      const book = XLSX.read(buffer, { type: 'array', cellDates: true });
      const sheet = book.Sheets[book.SheetNames[0]];
      const grid = XLSX.utils.sheet_to_json<unknown[]>(sheet, { header: 1, raw: true, defval: '' }).map((r) => r.map(cellText));
      const [head = [], ...body] = grid;
      const rows = body.filter((r) => r.some((c) => c));
      if (!head.some((h) => h) || !rows.length) {
        this.fileError = 'The first sheet needs a heading row and at least one data row.';
        return;
      }
      this.file = file;
      this.rows = rows;
      this.columns = head.map((h, i) => ({ header: h || `Column ${i + 1}`, field: '', sample: rows.find((r) => r[i])?.[i] ?? '' }));
      this.fixed = [];
      this.autoMap(false);
      this.resetValidation();
      this.step = 3;
      this.showToast(`${rows.length} rows read from ${file.name}.`);
    } catch {
      this.fileError = 'The file could not be read as a spreadsheet.';
    }
  }

  /** Builds a small sample file (with a few deliberate errors) for trying the import. */
  useSample(): void {
    const head = ['Asset ID *', 'Asset Name *', 'Category *', 'Asset Type *', 'Model', 'Serial Number', 'Purchase Date', 'Site', 'Status'];
    const rows = [
      ['GEN-00120', 'Diesel Generator 150 kVA', 'Power Generation', 'Generator', 'Perkins 1106A', 'PK1106-7781', '2026-02-10', 'Head Office', 'Active'],
      ['UPS-00090', 'UPS 250 kVA', 'Electrical', 'UPS', 'Galaxy VX 250', 'GVX-25-0090', '2026-03-04', 'Data Center', 'Active'],
      ['HVAC-00125', 'Air Handling Unit 2', 'HVAC', 'AHU', 'Trane CSAA 012', 'TR-CS-1250', '2026-03-18', 'Head Office', 'Active'],
      ['FP-00070', 'Jockey Pump', 'Safety & Security', 'Fire System', 'Grundfos CR 5', 'CR5-26-0070', '15/04/2026', 'Head Office', 'Active'],
      ['IT-00230', 'Laptop - Lenovo ThinkPad', 'IT Equipment', 'Laptop', 'T14 Gen 4', 'PF4XK2Q1', '2026-05-02', 'Head Office', 'In Storage'],
      ['IT-00230', 'Laptop - Lenovo ThinkPad', 'IT Equipment', 'Laptop', 'T14 Gen 4', 'PF4XK2Q1', '2026-05-02', 'Head Office', 'In Storage'],
      ['CCTV-00250', 'CCTV Camera - PTZ', 'Security', 'CCTV', 'Axis Q6075', 'AX-Q6075-250', '2026-13-40', 'Warehouse', 'Active'],
      ['', 'Solar Inverter 20 kW', 'Renewable Energy', 'Inverter', 'SMA STP 20', 'SMA-20-0011', '2026-06-01', 'Building B', 'Active'],
      ['TRF-00080', 'Transformer 250 kVA', 'Power Equipment', 'Transformer', 'ABB 250', 'ABB-250-80', '2026-06-20', 'Main Substation', 'Operational'],
      ['GEN-00045', 'Diesel Generator 250 kVA', 'Power Generation', 'Generator', 'C9.3', 'CAT2026G45001', '2023-01-10', 'Head Office', 'Active'],
    ];
    const sheet = XLSX.utils.aoa_to_sheet([head, ...rows]);
    const book = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(book, sheet, 'Assets');
    const out = XLSX.write(book, { type: 'array', bookType: 'xlsx' }) as ArrayBuffer;
    this.parse(new File([out], 'Assets_Sample.xlsx'), out);
  }

  removeFile(): void {
    this.file = null;
    this.rows = [];
    this.columns = [];
    this.fixed = [];
    this.mappingError = '';
    this.resetValidation();
    this.step = 2;
  }

  formatSize(bytes: number): string {
    return bytes >= 1024 * 1024 ? `${(bytes / 1024 / 1024).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`;
  }

  importNext(): void {
    if (this.step === 1) {
      this.step = 2;
      return;
    }
    if (!this.file) {
      this.fileError = 'Upload a file first.';
      return;
    }
    this.step = Math.max(this.step, 3);
    document.getElementById('bulk-mapping')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  // ===== Step 3: mapping =====

  /** Matches each column heading to a field by its label, key or a known alias. */
  autoMap(announce = true): void {
    this.mapMenuOpen = false;
    const taken = new Set<FieldKey>();
    for (const col of this.columns) {
      const h = norm(col.header.replace('*', ''));
      const f = FIELDS.find((x) => !taken.has(x.key) && (norm(x.label) === h || norm(x.key) === h || x.aliases.includes(h)));
      col.field = f?.key ?? '';
      if (f) taken.add(f.key);
    }
    this.resetValidation();
    if (announce) this.showToast(`${taken.size} of ${this.columns.length} columns mapped automatically.`);
  }

  clearMapping(): void {
    this.mapMenuOpen = false;
    this.columns.forEach((c) => (c.field = ''));
    this.fixed = [];
    this.resetValidation();
  }

  /** Fields chosen more than once. */
  get duplicateFields(): Set<FieldKey> {
    const seen = new Set<FieldKey>();
    const dup = new Set<FieldKey>();
    for (const key of [...this.columns.map((c) => c.field), ...this.fixed.map((f) => f.field)]) {
      if (!key) continue;
      if (seen.has(key)) dup.add(key);
      seen.add(key);
    }
    return dup;
  }

  mapStatus(field: FieldKey | ''): 'Mapped' | 'Not Mapped' | 'Duplicate' {
    if (!field) return 'Not Mapped';
    return this.duplicateFields.has(field) ? 'Duplicate' : 'Mapped';
  }

  get mappedCount(): number {
    return this.columns.filter((c) => c.field).length;
  }

  get missingMandatory(): FieldDef[] {
    const mapped = new Set([...this.columns.map((c) => c.field), ...this.fixed.filter((f) => f.value.trim()).map((f) => f.field)]);
    // Updating existing assets only needs the Asset ID; the rest keep their values
    return FIELDS.filter((f) => f.mandatory && !mapped.has(f.key) && (this.importType !== 'update' || f.key === 'tag'));
  }

  addFixed(): void {
    this.fixed = [...this.fixed, { field: '', value: '' }];
  }

  removeFixed(i: number): void {
    this.fixed = this.fixed.filter((_, n) => n !== i);
    this.resetValidation();
  }

  onMappingChange(): void {
    this.resetValidation();
  }

  mappingBack(): void {
    this.step = 2;
    document.getElementById('bulk-import')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  // ===== Step 4: validate =====

  private resetValidation(): void {
    this.validated = false;
    this.clean = [];
    this.issues = [];
    this.mappingError = '';
    if (this.step > 3) this.step = 3;
  }

  validate(): void {
    if (!this.file) return;
    if (this.duplicateFields.size) {
      this.mappingError = `Each field can be mapped once: ${[...this.duplicateFields].map((k) => this.field(k)!.label).join(', ')}.`;
      return;
    }
    const missing = this.missingMandatory;
    if (missing.length) {
      this.mappingError = `Map the mandatory field${missing.length > 1 ? 's' : ''}: ${missing.map((f) => f.label).join(', ')}.`;
      return;
    }
    this.mappingError = '';
    const clean: CleanRow[] = [];
    const issues: RowIssue[] = [];
    const seenTags = new Set<string>();
    this.rows.forEach((cells, i) => {
      const rowNo = i + 2; // heading is row 1
      const values: Partial<Record<FieldKey, string>> = {};
      this.columns.forEach((c, n) => c.field && (values[c.field] = cells[n] ?? ''));
      this.fixed.forEach((f) => f.field && f.value.trim() && !values[f.field] && (values[f.field] = f.value.trim()));
      const tag = (values.tag ?? '').trim().toUpperCase();
      const problem = this.rowProblem(values, tag);
      if (problem) {
        issues.push({ row: rowNo, tag: tag || '-', kind: 'Invalid', message: problem });
        return;
      }
      if (seenTags.has(tag)) {
        issues.push({ row: rowNo, tag, kind: 'Duplicate', message: 'Asset ID appears earlier in the file.' });
        return;
      }
      seenTags.add(tag);
      values.tag = tag;
      for (const key of ['purchaseDate', 'nextMaintenance'] as const) if (values[key]) values[key] = parseDate(values[key]!) ?? '';
      clean.push({ row: rowNo, values, exists: ASSET_RECORDS.some((a) => a.tag === tag) });
    });
    this.clean = clean;
    this.issues = issues;
    this.validated = true;
    this.step = 4;
    document.getElementById('bulk-results')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  /** First problem with a row, or '' when it is fine. */
  private rowProblem(v: Partial<Record<FieldKey, string>>, tag: string): string {
    const exists = ASSET_RECORDS.some((a) => a.tag === tag);
    for (const f of FIELDS) {
      const value = (v[f.key] ?? '').trim();
      const needed = f.mandatory && (this.importType !== 'update' || f.key === 'tag') && !(this.importType === 'upsert' && exists && f.key !== 'tag');
      if (needed && !value) return `${f.label} is missing.`;
      if (!value) continue;
      if (f.type === 'Date' && !parseDate(value)) return `${f.label} "${value}" is not a valid date.`;
      if (f.key === 'status' && !STATUSES.includes(value as AssetStatus)) return `Status "${value}" is not one of: ${STATUSES.join(', ')}.`;
      if (f.key === 'criticality' && !CRITICALITIES.includes(value as Criticality)) return `Criticality "${value}" is not one of: ${CRITICALITIES.join(', ')}.`;
      if (f.key === 'category' && !this.categories.includes(value)) return `Category "${value}" does not exist.`;
    }
    if (!/^[A-Z0-9][A-Z0-9-]*$/.test(tag)) return `Asset ID "${tag}" may only use letters, digits and dashes.`;
    if (this.importType === 'create' && exists) return `Asset ID ${tag} already exists.`;
    if (this.importType === 'update' && !exists) return `Asset ID ${tag} was not found.`;
    return '';
  }

  get invalidCount(): number {
    return this.issues.filter((i) => i.kind === 'Invalid').length;
  }

  get duplicateCount(): number {
    return this.issues.filter((i) => i.kind === 'Duplicate').length;
  }

  // ===== Step 5: import =====

  runImport(): void {
    if (!this.validated || !this.clean.length || !this.file) return;
    let created = 0;
    let updated = 0;
    for (const { values } of this.clean) {
      const existing = ASSET_RECORDS.find((a) => a.tag === values.tag);
      if (existing) {
        this.apply(existing, values);
        updated++;
      } else {
        ASSET_RECORDS.push(this.newRecord(values));
        created++;
      }
    }
    HISTORY.unshift({ fileName: this.file.name, type: 'Import', records: this.clean.length, status: 'Completed', at: nowLocal(), by: CURRENT_USER });
    this.step = 5;
    const skipped = this.issues.length;
    this.showToast(`Import complete: ${created} created, ${updated} updated${skipped ? `, ${skipped} rows skipped` : ''}. See the Asset List.`);
    // Start again for the next file
    const name = this.file.name;
    this.file = null;
    this.rows = [];
    this.columns = [];
    this.fixed = [];
    this.validated = false;
    this.clean = [];
    this.issues = [];
    this.lastImport = name;
  }

  /** File name of the last completed import, shown on the import card. */
  lastImport = '';

  private apply(record: AssetRecord, v: Partial<Record<FieldKey, string>>): void {
    const set = <K extends keyof AssetRecord>(key: K, value: AssetRecord[K] | undefined) => {
      if (value !== undefined && value !== '') record[key] = value;
    };
    set('name', v.name);
    set('category', v.category);
    set('type', v.type);
    set('manufacturer', v.manufacturer);
    set('model', v.model);
    set('serialNumber', v.serialNumber);
    set('purchaseDate', v.purchaseDate);
    set('site', v.site);
    set('area', v.area);
    set('custodian', v.custodian);
    set('department', v.department);
    set('status', v.status as AssetStatus | undefined);
    set('criticality', v.criticality as Criticality | undefined);
    if (v.nextMaintenance) record.nextMaintenance = v.nextMaintenance;
  }

  private newRecord(v: Partial<Record<FieldKey, string>>): AssetRecord {
    // Borrow the icon of a similar asset already in the register
    const like = ASSET_RECORDS.find((a) => a.type === v.type) ?? ASSET_RECORDS.find((a) => a.category === v.category);
    const icon: AssetIconKey = like?.icon ?? 'transformer';
    const record: AssetRecord = {
      id: Math.max(0, ...ASSET_RECORDS.map((a) => a.id)) + 1,
      tag: v.tag!, name: v.name ?? v.tag!, category: v.category ?? '', type: v.type ?? '',
      site: v.site ?? '', area: v.area ?? '', custodian: v.custodian ?? '', department: v.department ?? '',
      status: (v.status as AssetStatus) || 'Active', criticality: (v.criticality as Criticality) || 'Medium',
      manufacturer: v.manufacturer ?? '', model: v.model ?? '', nextMaintenance: v.nextMaintenance || null,
      icon, image: like?.image ?? '',
    };
    if (v.serialNumber) record.serialNumber = v.serialNumber;
    if (v.purchaseDate) record.purchaseDate = v.purchaseDate;
    return record;
  }

  /** First valid rows, as they will be imported. */
  get previewRows(): CleanRow[] {
    return this.clean.slice(0, 5);
  }

  // ===== Export =====

  setFieldSet(set: FieldSet): void {
    this.fieldSet = set;
    if (set === 'standard') this.exportFields = new Set(STANDARD_EXPORT);
    if (set === 'all') this.exportFields = new Set(FIELDS.map((f) => f.key));
  }

  setExportType(type: ExportType): void {
    this.exportType = type;
    if (type === 'custom') this.fieldSet = 'custom';
    else if (this.fieldSet === 'custom') this.setFieldSet('standard');
  }

  toggleExportField(key: FieldKey, on: boolean): void {
    const next = new Set(this.exportFields);
    if (on) next.add(key);
    else next.delete(key);
    this.exportFields = next;
  }

  get exportRows(): AssetRecord[] {
    return ASSET_RECORDS.filter(
      (a) =>
        (!this.filterCategory || a.category === this.filterCategory) &&
        (!this.filterSite || a.site === this.filterSite) &&
        (!this.filterStatus || a.status === this.filterStatus) &&
        (!this.filterCriticality || a.criticality === this.filterCriticality) &&
        (!this.filterDepartment || a.department === this.filterDepartment) &&
        (!this.dateFrom || (!!a.nextMaintenance && a.nextMaintenance >= this.dateFrom)) &&
        (!this.dateTo || (!!a.nextMaintenance && a.nextMaintenance <= this.dateTo)),
    );
  }

  get hasExportFilters(): boolean {
    return !!(this.filterCategory || this.filterSite || this.filterStatus || this.filterCriticality || this.filterDepartment || this.dateFrom || this.dateTo);
  }

  clearExportFilters(): void {
    this.filterCategory = this.filterSite = this.filterStatus = this.filterCriticality = this.filterDepartment = this.dateFrom = this.dateTo = '';
  }

  runExport(): void {
    const keys = FIELDS.map((f) => f.key).filter((k) => this.exportFields.has(k));
    if (!keys.length) {
      this.showToast('Choose at least one field to export.', true);
      return;
    }
    if (this.dateFrom && this.dateTo && this.dateTo < this.dateFrom) {
      this.showToast('The end date is before the start date.', true);
      return;
    }
    const rows = this.exportRows;
    if (!rows.length) {
      this.showToast('No assets match the filters.', true);
      return;
    }
    const related = this.exportType === 'related';
    const links = related ? readUserRelationships() : [];
    const head = [...keys.map((k) => this.field(k)!.label), ...(related ? ['Location', 'Parent Asset', 'Sub Assets'] : [])];
    const body = rows.map((a) => [
      ...keys.map((k) => {
        const v = (a as unknown as Record<string, unknown>)[k];
        return v === null || v === undefined ? '' : String(v);
      }),
      ...(related
        ? [assetLocation(a), links.filter((l) => l.subTag === a.tag).map((l) => l.mainTag).join(', '), links.filter((l) => l.mainTag === a.tag).map((l) => l.subTag).join(', ')]
        : []),
    ]);
    const stamp = nowLocal().slice(0, 10);
    const name = `Asset_Export_${stamp}.${this.exportFormat}`;
    const sheet = XLSX.utils.aoa_to_sheet([head, ...body]);
    sheet['!cols'] = head.map(() => ({ wch: 20 }));
    const book = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(book, sheet, 'Assets');
    XLSX.writeFile(book, name, { bookType: this.exportFormat });
    HISTORY.unshift({ fileName: name, type: 'Export', records: rows.length, status: 'Completed', at: nowLocal(), by: CURRENT_USER });
    this.showToast(`${rows.length} assets exported to ${name}.`);
  }

  // ===== Misc =====

  @HostListener('document:click')
  closeMenus(): void {
    this.mapMenuOpen = false;
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closeMenus();
    this.historyOpen = this.issuesOpen = false;
  }

  toggleMapMenu(event: Event): void {
    event.stopPropagation();
    this.mapMenuOpen = !this.mapMenuOpen;
  }

  private showToast(message: string, error = false): void {
    this.toast = message;
    this.toastError = error;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 4000);
  }
}
