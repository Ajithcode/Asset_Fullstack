import { Component, OnDestroy, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import * as XLSX from 'xlsx';
import {
  LucideAngularModule,
  ArrowLeft,
  ArrowRight,
  Building2,
  CalendarDays,
  Check,
  ChevronLeft,
  ChevronRight,
  CircleCheck,
  CircleX,
  CloudUpload,
  Download,
  Eye,
  FileCheck,
  FileSearch,
  FileSpreadsheet,
  FileText,
  FileUp,
  Info,
  MapPin,
  Pencil,
  RefreshCw,
  RotateCcw,
  Search,
  Settings,
  TriangleAlert,
  UserRound,
  UsersRound,
  X,
} from 'lucide-angular';
import {
  ASSIGNMENTS, Assignment, AssignmentType, CURRENT_ROLE, CURRENT_USER, DEPARTMENT_MANAGERS, DEPARTMENTS, EMPLOYEES, ENTITIES, LOCATION_DETAILS, LOCATIONS, PoolAsset,
  TARGET_LABELS, UNASSIGNED_ASSETS, assigneeChoices, assigneeDetails, isoDate, stamp,
} from '../../asset-assignment/asset-assignment-data';

type BulkField = 'tag' | 'assetName' | 'assetType' | 'type' | 'assignee' | 'department' | 'location' | 'from' | 'to' | 'reason' | 'costCenter' | 'remarks';
type RecordState = 'valid' | 'warning' | 'error';
type RecordFilter = RecordState | 'all';
type IssueTab = 'all' | 'error' | 'warning';
type PoolRow = PoolAsset & { current?: Assignment };

interface FieldDef {
  key: BulkField;
  label: string;
  dataType: 'Text' | 'Lookup' | 'Date';
  /** Shown as Required on Map Columns; each record needs a value (from the file or the common details). */
  required: boolean;
  /** The file must have a column for it. */
  mustMap: boolean;
  /** Headings auto-mapping recognises (lower case, letters only). */
  aliases: string[];
  hint: string;
}

interface ColumnMap {
  header: string;
  field: BulkField | '';
  sample: string;
}

/** One file row after mapping, defaults and validation. */
interface BulkRecord {
  /** Index in `rows`. */
  index: number;
  /** Row number in the file (heading = 1). */
  row: number;
  tag: string;
  asset?: PoolRow;
  assetName: string;
  type: AssignmentType | '';
  assignee: string;
  department: string;
  location: string;
  from: string;
  to: string;
  reason: string;
  remarks: string;
  state: RecordState;
  /** Field, short issue name and detail, for warnings and errors. */
  field: string;
  issue: string;
  detail: string;
  /** VALIDATION_RULES index the issue breaks. */
  rule: number;
}

/** Step 4: applied to every record; values in the file win. */
interface CommonDetails {
  type: AssignmentType;
  assignee: string;
  manager: string;
  site: string;
  building: string;
  floor: string;
  room: string;
  from: string;
  to: string;
  setEnd: boolean;
  reason: string;
  reference: string;
  notes: string;
  override: boolean;
  notifyManager: boolean;
  notifyAssignee: boolean;
  notifyDepartment: boolean;
}

const FIELDS: FieldDef[] = [
  { key: 'tag', label: 'Asset Tag / Serial', dataType: 'Text', required: true, mustMap: true, aliases: ['assettag', 'tag', 'assettagserial', 'serial', 'serialnumber', 'assetid'], hint: 'e.g. IT-000456' },
  { key: 'assetName', label: 'Asset Name', dataType: 'Text', required: true, mustMap: false, aliases: ['assetname', 'name', 'asset'], hint: 'Checked against the asset register' },
  { key: 'assetType', label: 'Asset Type', dataType: 'Lookup', required: true, mustMap: false, aliases: ['assettype', 'category', 'assetcategory'], hint: 'e.g. Laptop' },
  { key: 'type', label: 'Assignment Type', dataType: 'Lookup', required: true, mustMap: true, aliases: ['assignmenttype', 'type', 'assigntype'], hint: 'Employee, Department, Location or Other' },
  { key: 'assignee', label: 'Assign To', dataType: 'Lookup', required: true, mustMap: false, aliases: ['assignto', 'assigntoemployee', 'assignedto', 'assignee', 'employee', 'entity'], hint: 'Employee, department, location or entity name' },
  { key: 'department', label: 'Department', dataType: 'Lookup', required: true, mustMap: false, aliases: ['department', 'dept'], hint: DEPARTMENTS.join(', ') },
  { key: 'location', label: 'Location / Site', dataType: 'Lookup', required: true, mustMap: false, aliases: ['location', 'locationsite', 'site'], hint: LOCATIONS.join(', ') },
  { key: 'from', label: 'Start Date', dataType: 'Date', required: true, mustMap: false, aliases: ['startdate', 'fromdate', 'from', 'start'], hint: 'yyyy-mm-dd or dd/mm/yyyy' },
  { key: 'to', label: 'End Date', dataType: 'Date', required: false, mustMap: false, aliases: ['enddate', 'todate', 'to', 'end'], hint: 'Blank for open ended' },
  { key: 'reason', label: 'Purpose / Reason', dataType: 'Text', required: false, mustMap: false, aliases: ['purpose', 'reason', 'purposereason', 'assignmentreason'], hint: 'e.g. Department Use' },
  { key: 'costCenter', label: 'Cost Center', dataType: 'Lookup', required: false, mustMap: false, aliases: ['costcenter', 'costcentre', 'cc'], hint: 'e.g. IT-001' },
  { key: 'remarks', label: 'Notes / Remarks', dataType: 'Text', required: false, mustMap: false, aliases: ['notes', 'remarks', 'comment', 'comments', 'notesremarks'], hint: 'Optional' },
];

const VALIDATION_RULES = [
  'Asset Tag / Serial is mandatory',
  'Assignment Type is mandatory',
  'Assign To is mandatory',
  'Department is mandatory (if applicable)',
  'Location / Site is mandatory (if applicable)',
  'Start Date must be a valid date',
  'Asset must exist in the system',
  'Assignee must be active',
];

/** DEMO MODE: employees who have left, until the HR feed provides it. */
const INACTIVE_EMPLOYEES = new Set(['Sara Ahmed']);

const TYPES: AssignmentType[] = ['Employee', 'Department', 'Location', 'Other'];
const MAX_FILE_BYTES = 10 * 1024 * 1024;
const MONTHS = ['jan', 'feb', 'mar', 'apr', 'may', 'jun', 'jul', 'aug', 'sep', 'oct', 'nov', 'dec'];
const pad = (n: number) => String(n).padStart(2, '0');
const norm = (s: string) => s.toLowerCase().replace(/[^a-z]/g, '');
const unique = (values: string[]) => [...new Set(values)];

function cellText(v: unknown): string {
  if (v instanceof Date) return isoDate(v);
  return v === null || v === undefined ? '' : String(v).trim();
}

/** "2025-01-01", "01/01/2025" or "01 Jan 2025" → "2025-01-01"; '' when it isn't a date. */
function parseDate(s: string): string {
  let m = /^(\d{4})-(\d{1,2})-(\d{1,2})$/.exec(s);
  if (m) return valid(+m[1], +m[2], +m[3]);
  m = /^(\d{1,2})[/.-](\d{1,2})[/.-](\d{4})$/.exec(s);
  if (m) return valid(+m[3], +m[2], +m[1]);
  m = /^(\d{1,2})[\s-]([a-z]{3})[a-z]*[\s-](\d{4})$/i.exec(s);
  if (m && MONTHS.includes(m[2].toLowerCase())) return valid(+m[3], MONTHS.indexOf(m[2].toLowerCase()) + 1, +m[1]);
  return '';
}

function valid(y: number, mo: number, d: number): string {
  const date = new Date(y, mo - 1, d);
  return date.getFullYear() === y && date.getMonth() === mo - 1 && date.getDate() === d ? isoDate(date) : '';
}

/** Bulk Assignment (route assets/assignment/bulk-assignment): assigns assets from an Excel / CSV file. */
@Component({
  selector: 'app-bulk-assignment',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule],
  templateUrl: './bulk-assignment.html',
  styleUrls: [
    '../../asset-assignment/asset-assignment.css',
    '../../asset-assignment/asset-assignment-table.css',
    '../../asset-assignment/assign-asset/assign-asset.css',
    './bulk-assignment.css',
  ],
})
export class BulkAssignment implements OnDestroy {
  readonly icons = {
    ArrowLeft, ArrowRight, Building2, CalendarDays, Check, ChevronLeft, ChevronRight, CircleCheck, CircleX, CloudUpload, Download, Eye, FileCheck, FileSearch, FileSpreadsheet,
    FileText, FileUp, Info, MapPin, Pencil, RefreshCw, RotateCcw, Search, Settings, TriangleAlert, UserRound, UsersRound, X,
  };
  readonly steps = [
    { title: 'Upload File', sub: 'Upload and validate data' },
    { title: 'Map Columns', sub: 'Match file columns' },
    { title: 'Preview & Validate', sub: 'Review records' },
    { title: 'Assignment Details', sub: 'Set common details (optional)' },
    { title: 'Confirm & Assign', sub: 'Complete assignment' },
  ];
  readonly fields = FIELDS;
  readonly rules = VALIDATION_RULES;
  readonly types = TYPES;
  readonly targetLabels = TARGET_LABELS;
  readonly reasons = ['Department Use', 'Operational Use', 'New Joiner', 'Project Work', 'Replacement', 'Temporary Use', 'Relocation', 'Repair / Service', 'Other'];
  readonly managers = unique([...Object.values(DEPARTMENT_MANAGERS).map((m) => m.name), ...EMPLOYEES.map((e) => e.name)]).sort();
  readonly assigneeChoices = assigneeChoices;
  readonly pageSizes = [10, 25, 50];
  readonly notesMax = 500;
  readonly currentUser = CURRENT_USER;

  step = 1;
  file: { name: string; size: number; uploadedOn: string } | null = null;
  /** Blank rows dropped while reading the file. */
  emptyRows = 0;
  fileError = '';
  isDragging = false;
  /** Data rows of the file (heading row removed). */
  rows: string[][] = [];
  columns: ColumnMap[] = [];
  /** Cells changed with Edit Data, by row index. */
  edits = new Map<number, Partial<Record<BulkField, string>>>();
  /** Row indexes left out with Exclude. */
  excluded = new Set<number>();
  common: CommonDetails = {
    type: 'Department', assignee: '', manager: '', site: '', building: '', floor: '', room: '', from: isoDate(new Date()), to: '', setEnd: false,
    reason: 'Department Use', reference: `BULK-ASSIGN-${new Date().getFullYear()}-${pad(ASSIGNMENTS.length % 100)}`, notes: '', override: true,
    notifyManager: true, notifyAssignee: false, notifyDepartment: false,
  };
  records: BulkRecord[] = [];
  validated = false;

  // Preview & Validate
  filter: RecordFilter = 'all';
  search = '';
  page = 0;
  pageSize = 10;
  checked = new Set<number>();
  editing = false;
  issueTab: IssueTab = 'all';

  previewSearch = '';
  showAll = { file: false, preview: false, warnings: false, errors: false };
  includeWarnings = true;
  notify = true;
  saving = false;
  stepError = '';

  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;
  private readonly router = inject(Router);

  constructor() {
    // DEMO MODE: open with the sample file loaded so every step shows data, until uploads go to the API
    this.useSample(false);
  }

  ngOnDestroy(): void {
    clearTimeout(this.toastTimer);
  }

  // ===== Steps =====

  stepSub(i: number): string {
    if (i === 0 && this.file) return `${this.rows.length} rows uploaded`;
    if (i === 1 && this.file && this.step > 2) return `${this.mappedCount} of ${FIELDS.length} fields mapped`;
    if (i === 2 && this.validated && this.step > 3) return `${this.validCount} valid · ${this.errorCount} errors`;
    if (i === 3 && this.step > 4) return `Assign to ${this.common.assignee || TARGET_LABELS[this.common.type]}`;
    return this.steps[i].sub;
  }

  /** Stepper tabs switch freely; steps after Upload show a prompt until a file is loaded. */
  goStep(step: number): void {
    this.stepError = '';
    this.editing = false;
    if (step >= 3 && this.file) this.runValidation();
    this.step = step;
  }

  next(): void {
    if (this.step === 1 && !this.file) return void (this.stepError = 'Upload a file, or use the sample data, to continue.');
    if (this.step === 2) {
      const missing = FIELDS.filter((f) => f.mustMap && !this.isMapped(f.key));
      if (missing.length) return void (this.stepError = `Map the ${missing.map((f) => f.label).join(' and ')} column to continue.`);
      if (this.duplicateFields.size) return void (this.stepError = 'Each Asset360 field can be mapped to one column only.');
    }
    if (this.step === 4) {
      if (!this.common.from) return void (this.stepError = 'Choose the start date.');
      if (this.common.setEnd && !this.common.to) return void (this.stepError = 'Choose the end date, or untick Set end date.');
      if (this.common.setEnd && this.common.to < this.common.from) return void (this.stepError = 'The end date must be on or after the start date.');
      if (!this.common.reason) return void (this.stepError = 'Choose the assignment reason.');
    }
    this.goStep(this.step + 1);
  }

  back(): void {
    if (this.step === 1) return void this.router.navigate(['/assets/assignment']);
    this.goStep(this.step - 1);
  }

  // ===== Step 1: upload =====

  downloadTemplate(): void {
    const head = FIELDS.map((f) => f.label + (f.mustMap ? ' *' : ''));
    const sheet = XLSX.utils.aoa_to_sheet([head, ...this.sampleBody().slice(0, 3)]);
    sheet['!cols'] = head.map(() => ({ wch: 22 }));
    const book = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(book, sheet, 'Assignments');
    const notes = XLSX.utils.aoa_to_sheet([
      ['Field', 'Data Type', 'Mandatory', 'Allowed values / format'],
      ...FIELDS.map((f) => [f.label, f.dataType, f.mustMap ? 'Yes' : 'No', f.key === 'type' ? TYPES.join(', ') : f.hint]),
      [],
      ['Blank cells take the common details set in step 4 (Assignment Details).'],
    ]);
    XLSX.utils.book_append_sheet(book, notes, 'Instructions');
    XLSX.writeFile(book, 'Asset_Bulk_Assignment_Template.xlsx');
  }

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
    if (!/\.(xlsx|xls|csv)$/i.test(file.name)) return void (this.fileError = 'Choose an Excel (.xlsx) or CSV (.csv) file.');
    if (file.size > MAX_FILE_BYTES) return void (this.fileError = 'Files can be up to 10 MB.');
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
      this.load(file, head, body);
    } catch {
      this.fileError = 'The file could not be read as a spreadsheet.';
    }
  }

  private load(file: { name: string; size: number }, head: string[], body: string[][], announce = true): void {
    const rows = body.filter((r) => r.some((c) => c));
    if (!head.some((h) => h) || !rows.length) return void (this.fileError = 'The first sheet needs a heading row and at least one data row.');
    this.file = { name: file.name, size: file.size, uploadedOn: stamp(new Date()) };
    this.rows = rows;
    // Blank rows between data rows (trailing blanks are just the end of the sheet)
    const last = body.map((r) => r.some((c) => c)).lastIndexOf(true);
    this.emptyRows = body.slice(0, last + 1).length - rows.length;
    this.showAll.file = false;
    this.columns = head.map((h, i) => ({ header: h || `Column ${i + 1}`, field: '', sample: rows[0]?.[i] ?? '' }));
    this.edits.clear();
    this.excluded.clear();
    this.checked.clear();
    this.autoMap();
    this.validated = false;
    this.stepError = '';
    if (announce) this.showToast(`${rows.length} rows read from ${file.name}.`);
  }

  /** DEMO MODE: rows built from the asset list, with a few deliberate problems. */
  private sampleBody(): string[][] {
    const pool = this.pool.slice(0, 244);
    const active = EMPLOYEES.filter((e) => !INACTIVE_EMPLOYEES.has(e.name));
    const body = pool.map((p, i) => {
      const e = active[i % active.length];
      const kind = i % 5 === 1 ? 'Department' : i % 7 === 3 ? 'Location' : 'Employee';
      const assignee = kind === 'Employee' ? e.name : kind === 'Department' ? e.department : LOCATIONS[i % LOCATIONS.length];
      const location = kind === 'Location' ? assignee : e.location;
      const cc = LOCATION_DETAILS[location]?.costCenter.split(' ')[0] ?? '';
      return [
        p.tag, p.assetName, p.assetType, kind, assignee, kind === 'Location' ? '' : e.department, location, this.day(i % 20),
        i % 9 === 4 ? this.day(400) : '', this.reasons[i % 4], cc, i % 6 === 0 ? 'For daily operations' : '',
      ];
    });
    return body;
  }

  private day(offset: number): string {
    return isoDate(new Date(Date.now() + offset * 86_400_000));
  }

  /** DEMO MODE: 248 rows, 12 with warnings and 8 with errors, matching the design. */
  useSample(announce = true): void {
    const all = this.sampleBody();
    const body = all.slice(0, 240);
    // Spare assets (not in the first 240 rows) for rows that must fail on something other than a duplicate tag
    const [spare1, spare2, spare3, spare4] = all.slice(240);
    for (const i of [10, 30, 50, 70]) [body[i][7], body[i][8]] = [this.day(-730), ''];
    for (const i of [90, 110, 130, 150]) [body[i][7], body[i][8]] = [this.day(-90), this.day(-30)];
    for (const i of [170, 190, 210, 230]) Object.assign(body[i], { 3: 'Employee', 4: 'Sara Ahmed', 5: 'Operations', 6: 'Head Office - Floor 3' });
    Object.assign(body[6], { 3: 'Employee', 4: 'Former Employee', 5: 'Operations' });
    const today = isoDate(new Date());
    body.push(
      ['IT-009876', 'Dell Latitude 7440', 'Laptop', 'Employee', 'Arjun Mehta', 'IT Department', 'Head Office - Floor 2', today, '', 'Department Use', 'IT-001', ''],
      ['MOB-005432', 'iPhone 15', 'Mobile Phone', 'Employee', 'Priya Nair', 'Sales', 'Head Office - Floor 3', today, '', 'Department Use', '', ''],
      ['CAM-007654', 'Axis P3265-LVE', 'IP Camera', 'Location', 'Main Entrance', '', 'Main Entrance', today, '', 'Security Monitoring', '', ''],
      [...spare1.slice(0, 3), 'Department', 'Marketing', 'Marketing', 'Head Office - Floor 3', today, '', 'Department Use', '', ''],
      [...spare2.slice(0, 3), 'Location', 'Warehouse-02', '', 'Warehouse-02', today, '', 'Security Monitoring', '', ''],
      [...body[1]],
      [...spare3.slice(0, 7), '31/02/2025', '', 'Operational Use', '', ''],
      spare4,
    );
    this.load({ name: 'Asset_Bulk_Assignment.xlsx', size: 12_288 }, FIELDS.map((f) => f.label), body, announce);
  }

  removeFile(): void {
    this.file = null;
    this.rows = [];
    this.columns = [];
    this.records = [];
    this.emptyRows = 0;
    this.edits.clear();
    this.excluded.clear();
    this.validated = false;
    this.step = 1;
  }

  /** File Validations panel on the Upload step. */
  get fileChecks(): { label: string; state: 'ok' | 'warn' | 'fail' }[] {
    const missing = FIELDS.filter((f) => f.mustMap && !this.isMapped(f.key));
    return [
      { label: 'File format is valid', state: 'ok' },
      { label: missing.length ? `Missing: ${missing.map((f) => f.label).join(', ')}` : 'Required columns found', state: missing.length ? 'fail' : 'ok' },
      { label: this.emptyRows ? `${this.emptyRows} empty row${this.emptyRows === 1 ? '' : 's'} skipped` : 'No empty rows', state: this.emptyRows ? 'warn' : 'ok' },
      { label: 'Data read successfully', state: 'ok' },
    ];
  }

  /** File values (with edits) for preview tables; asset name / type fall back to the register. */
  fileRows(limit: number): Record<BulkField, string>[] {
    const pool = this.pool;
    return this.rows.slice(0, limit).map((r, i) => {
      const v = Object.fromEntries(FIELDS.map((f) => [f.key, this.cell(i, f.key)])) as Record<BulkField, string>;
      const asset = this.find(pool, v.tag);
      v.assetName ||= asset?.assetName ?? '';
      v.assetType ||= asset?.assetType ?? '';
      v.from = this.date(parseDate(v.from)) || v.from;
      v.to = this.date(parseDate(v.to)) || v.to;
      return v;
    });
  }

  /** A cell of row `i` for a field: the edited value, else the mapped column's value. */
  private cell(i: number, key: BulkField): string {
    const edited = this.edits.get(i)?.[key];
    if (edited !== undefined) return edited.trim();
    const c = this.columns.findIndex((x) => x.field === key);
    return c < 0 ? '' : (this.rows[i]?.[c] ?? '').trim();
  }

  private find(pool: PoolRow[], code: string): PoolRow | undefined {
    const c = code.toLowerCase();
    return c ? pool.find((p) => p.tag.toLowerCase() === c || p.serial.toLowerCase() === c) : undefined;
  }

  fileSize(bytes: number): string {
    return bytes < 1024 * 1024 ? `${Math.max(1, Math.round(bytes / 1024))} KB` : `${(bytes / 1024 / 1024).toFixed(1)} MB`;
  }

  // ===== Step 2: map columns =====

  autoMap(): void {
    const used = new Set<BulkField>();
    for (const c of this.columns) {
      const h = norm(c.header);
      const f = FIELDS.find((x) => !used.has(x.key) && (x.aliases.includes(h) || norm(x.label) === h));
      c.field = f?.key ?? '';
      if (f) used.add(f.key);
    }
    this.validated = false;
  }

  /** Unticking a column leaves it out; ticking it again guesses its field. */
  toggleColumn(c: ColumnMap, on: boolean): void {
    if (!on) c.field = '';
    else {
      const h = norm(c.header);
      c.field = FIELDS.find((x) => !this.isMapped(x.key) && (x.aliases.includes(h) || norm(x.label) === h))?.key ?? '';
    }
    this.onMappingChange();
  }

  onMappingChange(): void {
    this.validated = false;
    this.stepError = '';
  }

  isMapped(key: BulkField): boolean {
    return this.columns.some((c) => c.field === key);
  }

  fieldDef(key: BulkField | ''): FieldDef | undefined {
    return FIELDS.find((f) => f.key === key);
  }

  get mappedCount(): number {
    return FIELDS.filter((f) => this.isMapped(f.key)).length;
  }

  get duplicateFields(): Set<BulkField> {
    const seen = new Set<BulkField>();
    const dup = new Set<BulkField>();
    for (const c of this.columns) if (c.field) (seen.has(c.field) ? dup : seen).add(c.field);
    return dup;
  }

  // ===== Step 3: validate =====

  /** Every known asset: those in the assignment list plus unassigned stock. */
  private get pool(): PoolRow[] {
    const rows = new Map<string, PoolRow>();
    for (const a of ASSIGNMENTS) {
      const current = a.status === 'Returned' ? undefined : a;
      const known = rows.get(a.tag);
      if (!known || (!known.current && current)) rows.set(a.tag, { assetName: a.assetName, assetType: a.assetType, kind: a.kind, tag: a.tag, serial: a.serial, current });
    }
    for (const u of UNASSIGNED_ASSETS) if (!rows.has(u.tag)) rows.set(u.tag, { ...u });
    return [...rows.values()];
  }

  runValidation(): void {
    const pool = this.pool;
    const c = this.common;
    const today = isoDate(new Date());
    const commonLocation = this.commonLocation;
    const seen = new Set<string>();
    const records: BulkRecord[] = [];

    this.rows.forEach((_, i) => {
      if (this.excluded.has(i)) return;
      const cell = (k: BulkField) => this.cell(i, k);
      const rawType = cell('type');
      const type = (TYPES.find((t) => t.toLowerCase() === rawType.toLowerCase() || TARGET_LABELS[t].toLowerCase() === rawType.toLowerCase()) ?? (rawType ? '' : c.type)) as AssignmentType | '';
      const rawFrom = cell('from');
      const rawTo = cell('to');
      const rec: BulkRecord = {
        index: i, row: i + 2, tag: cell('tag'), assetName: cell('assetName'), type, assignee: cell('assignee') || (type === c.type ? c.assignee : ''),
        department: cell('department'), location: cell('location') || commonLocation, from: rawFrom ? parseDate(rawFrom) : c.from,
        to: rawTo ? parseDate(rawTo) : c.setEnd ? c.to : '', reason: cell('reason') || c.reason, remarks: cell('remarks'),
        state: 'valid', field: '', issue: '', detail: '', rule: -1,
      };
      rec.asset = this.find(pool, rec.tag);
      const fail = (rule: number, field: string, issue: string, detail: string) => Object.assign(rec, { state: 'error', rule, field, issue, detail });
      const warn = (rule: number, field: string, issue: string, detail: string) => rec.state === 'valid' && Object.assign(rec, { state: 'warning', rule, field, issue, detail });
      const key = rec.asset?.tag ?? rec.tag.toLowerCase();

      if (rec.type === 'Department' && !rec.department) rec.department = rec.assignee;
      if (rec.type === 'Employee' && !rec.department) rec.department = EMPLOYEES.find((e) => e.name.toLowerCase() === rec.assignee.toLowerCase())?.department ?? '';
      if (rec.type === 'Location' && !cell('location')) rec.location = rec.assignee;

      if (!rec.tag) fail(0, 'Asset Tag / Serial', 'Asset tag missing', 'The asset tag / serial cell is empty.');
      else if (!rec.asset) fail(6, 'Asset Tag / Serial', 'Asset not found', 'Asset tag does not exist in the system.');
      else if (seen.has(key)) fail(6, 'Asset Tag / Serial', 'Duplicate asset', `${rec.asset.tag} appears more than once in the file.`);
      else if (rawType && !type) fail(1, 'Assignment Type', 'Invalid assignment type', `"${rawType}" is not Employee, Department, Location or Other.`);
      else if (!rec.type) fail(1, 'Assignment Type', 'Assignment type missing', 'Set it in the file or in Assignment Details.');
      else if (!rec.assignee) fail(2, 'Assign To', 'Assignee missing', 'Set Assign To in the file or in Assignment Details.');
      else if (!assigneeChoices(rec.type).includes(this.canonical(rec.type, rec.assignee))) {
        if (rec.type === 'Employee') fail(2, 'Assign To (Employee)', 'Employee not found', `Employee '${rec.assignee}' not found.`);
        else if (rec.type === 'Department') fail(3, 'Department', 'Invalid department', `Department '${rec.assignee}' not found.`);
        else if (rec.type === 'Location') fail(4, 'Location / Site', 'Location not found', `Location '${rec.assignee}' not found.`);
        else fail(2, 'Assign To', 'Entity not found', `Entity '${rec.assignee}' not found.`);
      } else if (rec.department && !DEPARTMENTS.some((d) => d.toLowerCase() === rec.department.toLowerCase())) fail(3, 'Department', 'Invalid department', `Department '${rec.department}' not found.`);
      else if (rec.location && !LOCATIONS.some((l) => l.toLowerCase() === rec.location.toLowerCase())) fail(4, 'Location / Site', 'Location not found', `Location '${rec.location}' not found.`);
      else if (rawFrom && !rec.from) fail(5, 'Start Date', 'Invalid start date', `"${rawFrom}" is not a valid date.`);
      else if (!rec.from) fail(5, 'Start Date', 'Start date missing', 'Set it in the file or in Assignment Details.');
      else if (rawTo && !rec.to) fail(5, 'End Date', 'Invalid end date', `"${rawTo}" is not a valid date.`);
      else if (rec.to && rec.to < rec.from) fail(5, 'End Date', 'End before start', `End date (${this.date(rec.to)}) is earlier than start date.`);
      else if (rec.asset.current && !c.override) fail(6, 'Asset Tag / Serial', 'Already assigned', `Assigned to ${rec.asset.current.assignee}; tick Override existing assignment to hand it over.`);

      if (rec.state !== 'error') {
        rec.assignee = this.canonical(rec.type as AssignmentType, rec.assignee);
        rec.department = DEPARTMENTS.find((d) => d.toLowerCase() === rec.department.toLowerCase()) ?? rec.department;
        rec.location = LOCATIONS.find((l) => l.toLowerCase() === rec.location.toLowerCase()) ?? rec.location;
        if (rec.type === 'Employee' && INACTIVE_EMPLOYEES.has(rec.assignee)) warn(7, 'Assign To (Employee)', 'Employee not active', 'Assigned employee is inactive in the system.');
        const yearAgo = isoDate(new Date(Date.now() - 365 * 86_400_000));
        if (rec.from < yearAgo) warn(5, 'Start Date', 'Start date in past', `Start date (${this.date(rec.from)}) is more than a year ago.`);
        if (rec.to && rec.to < today) warn(5, 'End Date', 'End date in the past', `End date (${this.date(rec.to)}) is already over.`);
        if (rec.assetName && rec.assetName.toLowerCase() !== rec.asset!.assetName.toLowerCase()) warn(6, 'Asset Name', 'Asset name mismatch', `The register has '${rec.asset!.assetName}'.`);
      }
      if (rec.tag) seen.add(key);
      records.push(rec);
    });
    this.records = records;
    this.validated = true;
    if (this.page >= this.pageCount) this.page = 0;
  }

  /** Matches names case-insensitively ("it department" → "IT Department"). */
  private canonical(type: AssignmentType, name: string): string {
    return assigneeChoices(type).find((x) => x.toLowerCase() === name.toLowerCase()) ?? name;
  }

  get validCount(): number {
    return this.records.filter((r) => r.state === 'valid').length;
  }

  get warningRecords(): BulkRecord[] {
    return this.records.filter((r) => r.state === 'warning');
  }

  get errorRecords(): BulkRecord[] {
    return this.records.filter((r) => r.state === 'error');
  }

  get errorCount(): number {
    return this.errorRecords.length;
  }

  pct(n: number): number {
    return this.records.length ? Math.round((n / this.records.length) * 100) : 0;
  }

  /** Validation Rules panel: 'fail' when an error breaks it, 'warn' for warnings only. */
  ruleState(i: number): 'ok' | 'warn' | 'fail' {
    const hits = this.records.filter((r) => r.rule === i);
    return hits.some((r) => r.state === 'error') ? 'fail' : hits.length ? 'warn' : 'ok';
  }

  /** Donut segments as stroke-dasharray values (circumference 100). */
  get donut(): { valid: string; warn: string; warnOffset: number; err: string; errOffset: number } {
    const v = this.pct(this.validCount);
    const w = this.pct(this.warningRecords.length);
    const e = this.errorCount ? Math.max(0, 100 - v - w) : 0;
    return { valid: `${v} ${100 - v}`, warn: `${w} ${100 - w}`, warnOffset: -v, err: `${e} ${100 - e}`, errOffset: -(v + w) };
  }

  get issueRows(): BulkRecord[] {
    return this.records.filter((r) => r.state !== 'valid' && (this.issueTab === 'all' || r.state === this.issueTab));
  }

  setFilter(f: RecordFilter): void {
    this.filter = f;
    this.page = 0;
  }

  get filtered(): BulkRecord[] {
    const q = this.search.trim().toLowerCase();
    return this.records.filter(
      (r) => (this.filter === 'all' || r.state === this.filter) && (!q || `${r.tag} ${r.asset?.assetName ?? r.assetName} ${r.assignee} ${r.issue}`.toLowerCase().includes(q)),
    );
  }

  get pageRows(): BulkRecord[] {
    return this.filtered.slice(this.page * this.pageSize, (this.page + 1) * this.pageSize);
  }

  get pageCount(): number {
    return Math.max(1, Math.ceil(this.filtered.length / this.pageSize));
  }

  /** Page buttons: first, last and two either side of the current page, with gaps as null. */
  get pageList(): (number | null)[] {
    const n = this.pageCount;
    const out: (number | null)[] = [];
    for (let i = 0; i < n; i++) {
      if (i === 0 || i === n - 1 || Math.abs(i - this.page) <= 2) out.push(i);
      else if (out[out.length - 1] !== null) out.push(null);
    }
    return out;
  }

  get showingLabel(): string {
    const n = this.filtered.length;
    return n ? `Showing ${this.page * this.pageSize + 1} to ${Math.min(n, (this.page + 1) * this.pageSize)} of ${n} records` : 'No records';
  }

  get allPageChecked(): boolean {
    return this.pageRows.length > 0 && this.pageRows.every((r) => this.checked.has(r.index));
  }

  togglePage(on: boolean): void {
    for (const r of this.pageRows) on ? this.checked.add(r.index) : this.checked.delete(r.index);
  }

  toggleCheck(i: number): void {
    if (this.checked.has(i)) this.checked.delete(i);
    else this.checked.add(i);
  }

  /** Leaves the ticked rows out of the assignment. */
  excludeChecked(): void {
    const n = this.checked.size;
    for (const i of this.checked) this.excluded.add(i);
    this.checked.clear();
    this.runValidation();
    this.showToast(`${n} record${n === 1 ? '' : 's'} excluded.`);
  }

  restoreExcluded(): void {
    this.excluded.clear();
    this.runValidation();
  }

  /** Edit Data: cell changes are kept per row and the records re-checked when editing ends. */
  toggleEditing(): void {
    this.editing = !this.editing;
    if (!this.editing) this.runValidation();
  }

  edit(r: BulkRecord, key: BulkField, value: string): void {
    this.edits.set(r.index, { ...this.edits.get(r.index), [key]: value });
    (r as unknown as Record<string, string>)[key] = value;
  }

  exportErrors(): void {
    const rows = this.records
      .filter((r) => r.state !== 'valid')
      .map((r) => ({ Row: r.row, 'Asset Tag': r.tag, Status: r.state === 'error' ? 'Error' : 'Warning', Field: r.field, Issue: r.issue, Details: r.detail }));
    const book = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(book, XLSX.utils.json_to_sheet(rows), 'Errors & Warnings');
    XLSX.writeFile(book, 'Bulk_Assignment_Errors.xlsx');
  }

  // ===== Step 4: common details =====

  setCommonType(t: AssignmentType): void {
    if (this.common.type === t) return;
    this.common.type = t;
    this.common.assignee = '';
    this.common.manager = '';
  }

  /** Default manager of the chosen department. */
  get departmentManager(): string {
    return this.common.manager || DEPARTMENT_MANAGERS[this.commonDepartment]?.name || '';
  }

  get sites(): string[] {
    return unique(Object.values(LOCATION_DETAILS).map((d) => d.site));
  }

  get buildings(): string[] {
    return unique(Object.values(LOCATION_DETAILS).filter((d) => d.site === this.common.site).map((d) => d.building));
  }

  get floors(): string[] {
    const c = this.common;
    return unique(Object.values(LOCATION_DETAILS).filter((d) => d.site === c.site && d.building === c.building).map((d) => d.floor));
  }

  get rooms(): string[] {
    const c = this.common;
    return unique(Object.values(LOCATION_DETAILS).filter((d) => d.site === c.site && d.building === c.building && d.floor === c.floor).map((d) => d.room));
  }

  onSite(): void {
    this.common.building = this.buildings.length === 1 ? this.buildings[0] : '';
    this.onBuilding();
  }

  onBuilding(): void {
    this.common.floor = this.floors.length === 1 ? this.floors[0] : '';
    this.onFloor();
  }

  onFloor(): void {
    this.common.room = this.rooms.length === 1 ? this.rooms[0] : '';
  }

  /** The location (one of LOCATIONS) best matching the chosen site, building, floor and room. */
  get commonLocation(): string {
    const c = this.common;
    if (!c.site) return '';
    const match = Object.entries(LOCATION_DETAILS).find(
      ([, d]) => d.site === c.site && (!c.building || d.building === c.building) && (!c.floor || d.floor === c.floor) && (!c.room || d.room === c.room),
    );
    return match?.[0] ?? '';
  }

  onSetEnd(): void {
    if (!this.common.setEnd) this.common.to = '';
  }

  /** Department shown for the common details: the one assigned to, or the chosen employee's. */
  get commonDepartment(): string {
    const c = this.common;
    if (c.type === 'Department') return c.assignee;
    if (c.type === 'Employee') return EMPLOYEES.find((e) => e.name === c.assignee)?.department ?? '';
    return '';
  }

  get entityNames(): string[] {
    return ENTITIES.map((e) => e.name);
  }

  get commonRows(): { label: string; value: string }[] {
    const c = this.common;
    const rows = [{ label: 'Assignment Type', value: TARGET_LABELS[c.type] }];
    if (c.type !== 'Department') rows.push({ label: TARGET_LABELS[c.type], value: c.assignee });
    rows.push(
      { label: 'Department', value: this.commonDepartment },
      { label: 'Department Manager', value: this.departmentManager },
      { label: 'Location / Site', value: c.site },
      { label: 'Building', value: c.building },
      { label: 'Floor', value: c.floor },
      { label: 'Room / Space', value: c.room },
      { label: 'Start Date', value: this.date(c.from) },
      { label: 'End Date', value: c.setEnd ? this.date(c.to) : 'Open Ended' },
      { label: 'Assignment Reason', value: c.reason },
      { label: 'Reference', value: c.reference },
      { label: 'Notes', value: c.notes },
    );
    return rows.map((r) => ({ ...r, value: r.value || '-' }));
  }

  // ===== Step 5: confirm =====

  get validRecords(): BulkRecord[] {
    return this.records.filter((r) => r.state === 'valid');
  }

  get previewRecords(): BulkRecord[] {
    const q = this.previewSearch.trim().toLowerCase();
    const rows = this.validRecords.filter((r) => !q || `${r.tag} ${r.asset?.assetName ?? ''} ${r.assignee} ${r.location}`.toLowerCase().includes(q));
    return this.showAll.preview ? rows : rows.slice(0, 10);
  }

  /** Valid records, plus warnings when included. */
  get toAssign(): BulkRecord[] {
    return this.records.filter((r) => r.state === 'valid' || (this.includeWarnings && r.state === 'warning'));
  }

  confirm(): void {
    if (this.saving) return;
    if (!this.file) return void (this.stepError = 'Upload a file first.');
    this.runValidation();
    const list = this.toAssign;
    if (!list.length) return void (this.stepError = 'There are no valid records to assign.');
    this.saving = true;
    const now = stamp(new Date());
    const c = this.common;
    const notified = [c.notifyAssignee && 'Assignee', c.notifyManager && 'Department manager', c.notifyDepartment && 'Department'].filter(Boolean).join(', ');
    let id = Math.max(0, ...ASSIGNMENTS.map((a) => a.id));
    const created: Assignment[] = [];
    for (const r of list) {
      const p = r.asset!;
      const type = r.type as AssignmentType;
      const target = assigneeDetails(type, r.assignee);
      if (r.location && type !== 'Location') target.location = r.location;
      if (r.department && type !== 'Department') target.department = r.department;
      if (p.current) {
        p.current.history.push({ at: now, action: 'Assignment ended', by: CURRENT_USER, note: `Handed over to ${target.assignee} (bulk)` });
        p.current.status = 'Returned';
        p.current.to = isoDate(new Date());
      }
      const extra = [
        { label: 'Purpose', value: r.reason },
        { label: 'Reference', value: c.reference },
        { label: 'Cost Center', value: this.cell(r.index, 'costCenter') },
        { label: 'Source', value: `Bulk file ${this.file.name}, row ${r.row}` },
        { label: 'Notifications', value: this.notify ? notified || 'Sent' : 'Not sent' },
      ].filter((x) => x.value);
      created.push({
        id: ++id, assetName: p.assetName, assetType: p.assetType, kind: p.kind, tag: p.tag, serial: p.serial, type, ...target, from: r.from, to: r.to, status: 'Assigned',
        assignedBy: CURRENT_USER, assignedByRole: CURRENT_ROLE, assignedOn: now, remarks: [r.remarks, c.notes.trim()].filter(Boolean).join('\n'), extra,
        history: [{ at: now, action: 'Assigned', by: CURRENT_USER, note: `Bulk assigned to ${target.assignee}${p.current ? ` (from ${p.current.assignee})` : ''}` }],
      });
    }
    ASSIGNMENTS.unshift(...created);
    const skipped = this.rows.length - created.length;
    const n = created.length;
    this.router.navigate(['/assets/assignment'], {
      state: { toast: `${n} asset${n === 1 ? '' : 's'} assigned from ${this.file.name}${skipped ? `; ${skipped} record${skipped === 1 ? '' : 's'} skipped` : ''}.` },
    });
  }

  // ===== Helpers =====

  /** "2025-10-16T10:24" → "16 Oct 2025 10:24" */
  dateTime(at: string): string {
    return at ? `${this.date(at.slice(0, 10))} ${at.slice(11, 16)}` : '';
  }

  date(d: string): string {
    return d ? new Date(`${d}T00:00`).toLocaleDateString('en-GB', { day: '2-digit', month: 'short', year: 'numeric' }) : '';
  }

  /** "Head Office - F2" style short location for the preview tables. */
  shortLocation(l: string): string {
    return l.replace(/ - Floor (\d+)/, ' - F$1');
  }

  private showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 4000);
  }
}
