import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import * as XLSX from 'xlsx';
import { Observable, finalize, forkJoin, of, switchMap } from 'rxjs';
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
import { apiErrorMessage } from '../../services/asset-api';
import { AssignmentType, ENTITIES, TARGET_LABELS, isoDate, stamp } from '../../asset-assignment/asset-assignment-data';
import {
  AssetAssignmentService, AssignmentLookups, EMPTY_LOOKUPS, assignToOf, choicesFor, dayOf, departmentRequest, employeeRequest, locationFrom, uniqueSorted,
} from '../../asset-assignment/asset-assignment.service';
import { BulkConfirmDto, BulkDetailsRequest, BulkFieldKey, BulkFileCheckDto, BulkRecordDto, BulkAssignmentService, BulkValuesDto, saveBlob } from './bulk-assignment.service';

type BulkField = 'tag' | 'assetName' | 'assetType' | 'type' | 'assignee' | 'department' | 'location' | 'from' | 'to' | 'reason' | 'costCenter' | 'remarks';
type RecordState = 'valid' | 'warning' | 'error';
type RecordFilter = RecordState | 'all';
type IssueTab = 'all' | 'error' | 'warning';

interface FieldDef {
  key: BulkField;
  /** The API's field key. */
  server: BulkFieldKey;
  label: string;
  dataType: 'Text' | 'Lookup' | 'Date';
  /** Shown as Required on Map Columns; each record needs a value (from the file or the common details). */
  required: boolean;
  /** The file must have a column for it. */
  mustMap: boolean;
  /** Headings auto-mapping recognises (lower case, letters only). */
  aliases: string[];
}

interface ColumnMap {
  header: string;
  field: BulkField | '';
  sample: string;
}

/** One checked row of the batch. */
interface BulkRecord {
  /** Row number in the file (heading = 1); also the selection key. */
  index: number;
  row: number;
  tag: string;
  /** The registered asset the row was matched to. */
  asset?: { tag: string; assetName: string; assetType: string };
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
  /** Field, short issue name and detail of the first issue. */
  field: string;
  issue: string;
  detail: string;
  /** VALIDATION_RULES index the issue breaks. */
  rule: number;
  /** Exactly what the file (or Edit Data) gave, for editing. */
  values: BulkValuesDto;
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
  { key: 'tag', server: 'assetTag', label: 'Asset Tag / Serial', dataType: 'Text', required: true, mustMap: true, aliases: ['assettag', 'tag', 'assettagserial', 'serial', 'serialnumber', 'assetid'] },
  { key: 'assetName', server: 'assetName', label: 'Asset Name', dataType: 'Text', required: true, mustMap: false, aliases: ['assetname', 'name', 'asset'] },
  { key: 'assetType', server: 'assetType', label: 'Asset Type', dataType: 'Lookup', required: true, mustMap: false, aliases: ['assettype', 'category', 'assetcategory'] },
  { key: 'type', server: 'assignmentType', label: 'Assignment Type', dataType: 'Lookup', required: true, mustMap: true, aliases: ['assignmenttype', 'type', 'assigntype'] },
  { key: 'assignee', server: 'assignTo', label: 'Assign To', dataType: 'Lookup', required: true, mustMap: false, aliases: ['assignto', 'assigntoemployee', 'assignedto', 'assignee', 'employee', 'entity'] },
  { key: 'department', server: 'department', label: 'Department', dataType: 'Lookup', required: true, mustMap: false, aliases: ['department', 'dept'] },
  { key: 'location', server: 'location', label: 'Location / Site', dataType: 'Lookup', required: true, mustMap: false, aliases: ['location', 'locationsite', 'site'] },
  { key: 'from', server: 'startDate', label: 'Start Date', dataType: 'Date', required: true, mustMap: false, aliases: ['startdate', 'fromdate', 'from', 'start'] },
  { key: 'to', server: 'endDate', label: 'End Date', dataType: 'Date', required: false, mustMap: false, aliases: ['enddate', 'todate', 'to', 'end'] },
  { key: 'reason', server: 'purpose', label: 'Purpose / Reason', dataType: 'Text', required: false, mustMap: false, aliases: ['purpose', 'reason', 'purposereason', 'assignmentreason'] },
  { key: 'costCenter', server: 'costCenter', label: 'Cost Center', dataType: 'Lookup', required: false, mustMap: false, aliases: ['costcenter', 'costcentre', 'cc'] },
  { key: 'remarks', server: 'notes', label: 'Notes / Remarks', dataType: 'Text', required: false, mustMap: false, aliases: ['notes', 'remarks', 'comment', 'comments', 'notesremarks'] },
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

const TYPES: AssignmentType[] = ['Employee', 'Department', 'Location', 'Other'];
const MAX_FILE_BYTES = 10 * 1024 * 1024;
const pad = (n: number) => String(n).padStart(2, '0');
const norm = (s: string) => s.toLowerCase().replace(/[^a-z]/g, '');

/** Validation Rules panel line an API issue belongs to. */
function ruleOf(field: string, message: string): number {
  const m = message.toLowerCase();
  switch (field) {
    case 'assetTag':
      return m.includes('required') ? 0 : 6;
    case 'assetName':
    case 'assetType':
      return 6;
    case 'assignmentType':
      return 1;
    case 'assignTo':
      return m.includes('not active') ? 7 : m.includes('location') ? 4 : 2;
    case 'department':
      return m.includes('not active') ? 7 : 3;
    case 'location':
      return 4;
    case 'startDate':
    case 'endDate':
      return 5;
    default:
      return -1;
  }
}

function typeFromText(text: string | null): AssignmentType | '' {
  const t = norm(text ?? '');
  if (t === 'employee') return 'Employee';
  if (t === 'department') return 'Department';
  if (t === 'location' || t === 'locationsite' || t === 'site') return 'Location';
  if (t === 'otherentity' || t === 'other' || t === 'entity') return 'Other';
  return '';
}

function toRecord(r: BulkRecordDto): BulkRecord {
  const issue = r.issues?.slice().sort((a, b) => (a.severity === b.severity ? 0 : a.severity === 'Error' ? -1 : 1))[0];
  const state: RecordState = r.status === 'Error' ? 'error' : r.status === 'Warning' ? 'warning' : 'valid';
  return {
    index: r.rowNumber,
    row: r.rowNumber,
    tag: r.assetTag ?? r.values?.assetTag ?? '',
    asset: r.assetRegistrationId ? { tag: r.assetTag ?? '', assetName: r.assetName ?? '', assetType: r.assetType ?? '' } : undefined,
    assetName: r.values?.assetName ?? r.assetName ?? '',
    type: typeFromText(r.assignmentType),
    assignee: r.assignTo ?? '',
    department: r.department ?? '',
    location: r.location?.replace(/ > /g, ' - ') ?? '',
    from: dayOf(r.startDate),
    to: dayOf(r.endDate),
    reason: r.purpose ?? '',
    remarks: r.notes ?? '',
    state,
    field: issue ? FIELDS.find((f) => f.server === issue.field)?.label ?? issue.field : '',
    issue: issue?.message ?? '',
    detail: issue?.details ?? '',
    rule: issue ? ruleOf(issue.field, issue.message) : -1,
    values: { ...r.values },
  };
}

/** Bulk Assignment (route assets/assignment/bulk-assignment): assigns assets from an Excel / CSV file through the bulk assignment API. */
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
export class BulkAssignment implements OnInit, OnDestroy {
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
  readonly pageSizes = [10, 25, 50];
  readonly notesMax = 500;

  step = 1;
  /** The batch stored by the API for the uploaded file. */
  batchId = '';
  file: { name: string; size: number; uploadedOn: string; uploadedBy: string } | null = null;
  /** Blank rows dropped while reading the file. */
  emptyRows = 0;
  fileError = '';
  isDragging = false;
  /** Data rows of the file as read by the API (heading row removed). */
  rows: string[][] = [];
  columns: ColumnMap[] = [];
  private fileChecksDto: BulkFileCheckDto[] = [];
  /** The mapping last saved, so it is only sent again when changed. */
  private savedMapping = '';
  /** Changed values per row number (Edit Data), sent when editing ends. */
  edits = new Map<number, BulkValuesDto>();
  /** Row numbers left out with Exclude; skipped on confirm. */
  excluded = new Set<number>();
  common: CommonDetails = {
    type: 'Department', assignee: '', manager: '', site: '', building: '', floor: '', room: '', from: isoDate(new Date()), to: '', setEnd: false,
    reason: 'Department Use', reference: `BULK-ASSIGN-${new Date().getFullYear()}-${pad(new Date().getMonth() + 1)}${pad(new Date().getDate())}`, notes: '', override: true,
    notifyManager: true, notifyAssignee: false, notifyDepartment: false,
  };
  private allRecords: BulkRecord[] = [];
  validated = false;
  lookups: AssignmentLookups = EMPTY_LOOKUPS;
  /** An API call is running (upload, mapping, records, details). */
  busy = false;

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
  private readonly api = inject(BulkAssignmentService);
  private readonly assignments = inject(AssetAssignmentService);

  ngOnInit(): void {
    this.assignments.lookups().subscribe((lk) => (this.lookups = lk));
  }

  ngOnDestroy(): void {
    clearTimeout(this.toastTimer);
  }

  get currentUser(): string {
    return this.file?.uploadedBy || this.assignments.user;
  }

  get records(): BulkRecord[] {
    return this.excluded.size ? this.allRecords.filter((r) => !this.excluded.has(r.row)) : this.allRecords;
  }

  /** Runs an API call with the busy flag; errors go to the step error line. */
  private call<T>(obs: Observable<T>, next: (v: T) => void, fallback?: string): void {
    this.busy = true;
    this.stepError = '';
    obs.pipe(finalize(() => (this.busy = false))).subscribe({ next, error: (err) => (this.stepError = apiErrorMessage(err, fallback)) });
  }

  // ===== Steps =====

  stepSub(i: number): string {
    if (i === 0 && this.file) return `${this.rows.length} rows uploaded`;
    if (i === 1 && this.file && this.step > 2) return `${this.mappedCount} of ${FIELDS.length} fields mapped`;
    if (i === 2 && this.validated && this.step > 3) return `${this.validCount} valid · ${this.errorCount} errors`;
    if (i === 3 && this.step > 4) return `Assign to ${this.common.assignee || TARGET_LABELS[this.common.type]}`;
    return this.steps[i].sub;
  }

  /** Stepper tabs: steps after Map Columns save the mapping (when changed) and load the checked rows first. */
  goStep(step: number): void {
    this.stepError = '';
    this.editing = false;
    if (step < 3 || !this.batchId) return void (this.step = step);
    const mapError = this.mappingError();
    if (mapError) {
      this.step = 2;
      this.stepError = mapError;
      return;
    }
    this.call(this.saveMappingIfChanged().pipe(switchMap(() => this.api.records(this.batchId))), (records) => {
      this.setRecords(records);
      this.step = step;
    }, 'The records could not be checked.');
  }

  private mappingError(): string {
    const missing = FIELDS.filter((f) => f.mustMap && !this.isMapped(f.key));
    if (missing.length) return `Map the ${missing.map((f) => f.label).join(' and ')} column to continue.`;
    if (this.duplicateFields.size) return 'Each Asset360 field can be mapped to one column only.';
    return '';
  }

  private get mapping(): Partial<Record<BulkFieldKey, string>> {
    const out: Partial<Record<BulkFieldKey, string>> = {};
    for (const c of this.columns) {
      const f = this.fieldDef(c.field);
      if (f) out[f.server] = c.header;
    }
    return out;
  }

  private saveMappingIfChanged(): Observable<unknown> {
    const mapping = this.mapping;
    const key = JSON.stringify(mapping);
    if (key === this.savedMapping) return of(null);
    return this.api.saveMapping(this.batchId, mapping).pipe(
      switchMap((r) => {
        this.savedMapping = key;
        this.edits.clear();
        return of(r);
      }),
    );
  }

  private setRecords(records: BulkRecordDto[]): void {
    this.allRecords = records.map(toRecord);
    this.validated = true;
    for (const i of [...this.checked]) if (!this.allRecords.some((r) => r.index === i)) this.checked.delete(i);
    if (this.page >= this.pageCount) this.page = 0;
  }

  next(): void {
    if (this.busy) return;
    if (this.step === 1 && !this.file) return void (this.stepError = 'Upload a file, or use the sample data, to continue.');
    if (this.step === 2) {
      const error = this.mappingError();
      if (error) return void (this.stepError = error);
    }
    if (this.step === 4) {
      const c = this.common;
      if (!c.from) return void (this.stepError = 'Choose the start date.');
      if (c.setEnd && !c.to) return void (this.stepError = 'Choose the end date, or untick Set end date.');
      if (c.setEnd && c.to < c.from) return void (this.stepError = 'The end date must be on or after the start date.');
      if (!c.reason) return void (this.stepError = 'Choose the assignment reason.');
      if (c.type !== 'Employee' && !c.assignee) return void (this.stepError = `Choose the ${TARGET_LABELS[c.type].toLowerCase()}.`);
      const request = this.detailsRequest();
      if (c.type === 'Department' && !request.department?.departmentId) return void (this.stepError = `Department '${c.assignee}' was not found in master data.`);
      if (c.type === 'Location' && !request.location?.siteId) return void (this.stepError = `Site '${c.assignee}' was not found in master data.`);
      this.call(this.api.saveDetails(this.batchId, request).pipe(switchMap(() => this.api.records(this.batchId))), (records) => {
        this.setRecords(records);
        this.step = 5;
      }, 'The assignment details could not be saved.');
      return;
    }
    this.goStep(this.step + 1);
  }

  back(): void {
    if (this.step === 1) return void this.router.navigate(['/assets/assignment']);
    this.goStep(this.step - 1);
  }

  // ===== Step 1: upload =====

  downloadTemplate(): void {
    this.api.template(true).subscribe({
      next: (blob) => saveBlob(blob, 'Asset_Bulk_Assignment_Template.xlsx'),
      error: (err) => (this.fileError = apiErrorMessage(err, 'The template could not be downloaded.')),
    });
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

  /** Uploads the file; the API reads it and keeps its rows as a batch. */
  private readFile(file: File | undefined, announce = true): void {
    if (!file || this.busy) return;
    if (/\.xls$/i.test(file.name)) return void (this.fileError = 'Old .xls files cannot be read; save the file as .xlsx and upload it again.');
    if (!/\.(xlsx|csv)$/i.test(file.name)) return void (this.fileError = 'Choose an Excel (.xlsx) or CSV (.csv) file.');
    if (file.size > MAX_FILE_BYTES) return void (this.fileError = 'Files can be up to 10 MB.');
    this.fileError = '';
    const previous = this.batchId;
    this.busy = true;
    this.api
      .upload(file)
      .pipe(
        switchMap((up) => forkJoin({ up: of(up), rows: this.api.fileRows(up.batch.id) })),
        finalize(() => (this.busy = false)),
      )
      .subscribe({
        next: ({ up, rows }) => {
          if (previous) this.api.delete(previous).subscribe({ error: () => undefined });
          const b = up.batch;
          this.batchId = b.id;
          this.file = { name: b.file.fileName, size: b.file.fileSize, uploadedOn: b.file.uploadedOn ? stamp(new Date(b.file.uploadedOn)) : stamp(new Date()), uploadedBy: b.file.uploadedBy ?? '' };
          this.rows = rows.map((r) => b.columns.map((_, i) => (r.cells[i] ?? '').trim()));
          this.fileChecksDto = up.fileValidations ?? [];
          const empty = /(\d+) empty rows/.exec(this.fileChecksDto.find((c) => !c.passed && /empty/i.test(c.name))?.message ?? '');
          this.emptyRows = empty ? +empty[1] : 0;
          this.columns = b.columns.map((h, i) => {
            const server = (Object.keys(up.suggestedMapping) as BulkFieldKey[]).find((k) => up.suggestedMapping[k] === h);
            return { header: h, field: FIELDS.find((f) => f.server === server)?.key ?? '', sample: this.rows.find((r) => r[i])?.[i] ?? '' };
          });
          this.savedMapping = '';
          this.allRecords = [];
          this.edits.clear();
          this.excluded.clear();
          this.checked.clear();
          this.showAll.file = false;
          this.validated = false;
          this.stepError = '';
          if (announce) this.showToast(`${this.rows.length} rows read from ${this.file.name}.`);
        },
        error: (err) => (this.fileError = apiErrorMessage(err, 'The file could not be uploaded.')),
      });
  }

  /** Builds a sample file from registered assets and master data and uploads it; falls back to the template's sample rows. */
  useSample(): void {
    if (this.busy) return;
    this.busy = true;
    forkJoin({ assets: this.assignments.listAssets(), lookups: this.assignments.lookups() })
      .pipe(finalize(() => (this.busy = false)))
      .subscribe({
        next: ({ assets, lookups }) => {
          const people = lookups.employees.filter((e) => e.active);
          const today = isoDate(new Date());
          const body = assets.slice(0, 25).map((a, i) => {
            const e = people.length ? people[i % people.length] : undefined;
            const d = lookups.departments.length ? lookups.departments[i % lookups.departments.length] : undefined;
            const site = lookups.sites.length ? lookups.sites[i % lookups.sites.length] : undefined;
            const kind = i % 5 === 1 && d ? 'Department' : i % 7 === 3 && site ? 'Location' : e ? 'Employee' : d ? 'Department' : 'Location';
            const assignee = kind === 'Employee' ? e?.id ?? '' : kind === 'Department' ? d?.name ?? '' : site?.name ?? '';
            return [a.tag, a.assetName, a.assetType, kind, assignee, kind === 'Employee' ? e?.department ?? '' : kind === 'Department' ? d?.name ?? '' : '', kind === 'Location' ? '' : site?.name ?? e?.location ?? '', today, '', this.reasons[i % 4], e?.costCenter ?? '', ''];
          });
          if (!body.length) return this.sampleFromTemplate();
          const sheet = XLSX.utils.aoa_to_sheet([FIELDS.map((f) => f.label), ...body]);
          const book = XLSX.utils.book_new();
          XLSX.utils.book_append_sheet(book, sheet, 'Assignments');
          const data = XLSX.write(book, { type: 'array', bookType: 'xlsx' }) as ArrayBuffer;
          this.busy = false;
          this.readFile(new File([data], 'Asset_Bulk_Assignment_Sample.xlsx', { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' }));
        },
        error: () => this.sampleFromTemplate(),
      });
  }

  private sampleFromTemplate(): void {
    this.api.template(true).subscribe({
      next: (blob) => {
        this.busy = false;
        this.readFile(new File([blob], 'Asset_Bulk_Assignment_Sample.xlsx', { type: blob.type }));
      },
      error: (err) => (this.fileError = apiErrorMessage(err, 'The sample file could not be loaded.')),
    });
  }

  removeFile(): void {
    if (this.batchId) this.api.delete(this.batchId).subscribe({ error: () => undefined });
    this.batchId = '';
    this.file = null;
    this.rows = [];
    this.columns = [];
    this.allRecords = [];
    this.emptyRows = 0;
    this.fileChecksDto = [];
    this.savedMapping = '';
    this.edits.clear();
    this.excluded.clear();
    this.validated = false;
    this.step = 1;
  }

  /** File Validations panel on the Upload step, as returned by the upload. */
  get fileChecks(): { label: string; state: 'ok' | 'warn' | 'fail' }[] {
    return this.fileChecksDto.map((c) => ({
      label: c.passed ? c.name : c.message || c.name,
      state: c.passed ? 'ok' : /empty/i.test(c.name) ? 'warn' : 'fail',
    }));
  }

  /** File values for preview tables, by the current mapping. */
  fileRows(limit: number): Record<BulkField, string>[] {
    return this.rows.slice(0, limit).map((_, i) => Object.fromEntries(FIELDS.map((f) => [f.key, this.cell(i, f.key)])) as Record<BulkField, string>);
  }

  private cell(i: number, key: BulkField): string {
    const c = this.columns.findIndex((x) => x.field === key);
    return c < 0 ? '' : (this.rows[i]?.[c] ?? '').trim();
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
    if (this.busy && !n) return 'Loading...';
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

  /** Leaves the ticked rows out of the assignment (they are skipped on confirm). */
  excludeChecked(): void {
    const n = this.checked.size;
    for (const i of this.checked) this.excluded.add(i);
    this.checked.clear();
    if (this.page >= this.pageCount) this.page = 0;
    this.showToast(`${n} record${n === 1 ? '' : 's'} excluded.`);
  }

  restoreExcluded(): void {
    this.excluded.clear();
  }

  /** Edit Data: changed rows are sent when editing ends, and every row is checked again. */
  toggleEditing(): void {
    if (!this.editing) return void (this.editing = true);
    if (!this.edits.size) return void (this.editing = false);
    const rows = [...this.edits].map(([rowNumber, values]) => ({ rowNumber, values }));
    this.call(this.api.updateRecords(this.batchId, rows).pipe(switchMap(() => this.api.records(this.batchId))), (records) => {
      this.edits.clear();
      this.editing = false;
      this.setRecords(records);
      this.showToast(`${rows.length} row${rows.length === 1 ? '' : 's'} updated.`);
    }, 'The changes could not be saved.');
  }

  edit(r: BulkRecord, key: BulkField, value: string): void {
    const server = FIELDS.find((f) => f.key === key)!.server;
    const values = { ...(this.edits.get(r.row) ?? r.values), [server]: value?.trim() ? (key === 'type' && value === 'Other' ? 'Other Entity' : value) : null };
    this.edits.set(r.row, values);
    (r as unknown as Record<string, string>)[key] = value;
  }

  exportErrors(): void {
    if (!this.batchId) return;
    this.api.exportIssues(this.batchId, true).subscribe({
      next: (blob) => saveBlob(blob, 'Bulk_Assignment_Errors.xlsx'),
      error: (err) => this.showToast(apiErrorMessage(err, 'The errors could not be exported.')),
    });
  }

  // ===== Step 4: common details =====

  setCommonType(t: AssignmentType): void {
    if (this.common.type === t) return;
    this.common.type = t;
    this.common.assignee = '';
    this.common.manager = '';
  }

  assigneeChoices(type: AssignmentType): string[] {
    return choicesFor(this.lookups, type, ENTITIES.map((e) => e.name));
  }

  get managers(): string[] {
    return uniqueSorted(this.lookups.employees.map((e) => e.name));
  }

  private get department() {
    return this.lookups.departments.find((d) => d.name === this.commonDepartment);
  }

  /** Default manager of the chosen department. */
  get departmentManager(): string {
    return this.common.manager || this.department?.manager?.name || '';
  }

  get sites(): string[] {
    return uniqueSorted(this.lookups.sites.map((x) => x.name));
  }

  private get siteId(): string | undefined {
    return this.lookups.sites.find((x) => x.name === this.common.site)?.id;
  }

  get buildings(): string[] {
    const id = this.siteId;
    return uniqueSorted(this.lookups.buildings.filter((b) => !b.siteId || b.siteId === id).map((b) => b.name));
  }

  get floors(): string[] {
    const c = this.common;
    const id = this.siteId;
    return uniqueSorted(this.lookups.floors.filter((f) => (!f.siteId || f.siteId === id) && (!f.buildingName || f.buildingName === c.building)).map((f) => f.name));
  }

  get rooms(): string[] {
    const c = this.common;
    const id = this.siteId;
    return uniqueSorted(
      this.lookups.rooms.filter((r) => (!r.siteId || r.siteId === id) && (!r.buildingName || r.buildingName === c.building) && (!r.floorName || r.floorName === c.floor)).map((r) => r.name),
    );
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

  onSetEnd(): void {
    if (!this.common.setEnd) this.common.to = '';
  }

  /** Department shown for the common details: the one assigned to, or the chosen employee's. */
  get commonDepartment(): string {
    const c = this.common;
    if (c.type === 'Department') return c.assignee;
    if (c.type === 'Employee') return this.lookups.employees.find((e) => e.name === c.assignee)?.department ?? '';
    return '';
  }

  /** Step 4 as the API request. */
  private detailsRequest(): BulkDetailsRequest {
    const c = this.common;
    const lk = this.lookups;
    const employee = c.type === 'Employee' && c.assignee ? employeeRequest(lk.employees.find((e) => e.name === c.assignee), c.assignee) : null;
    let department = this.department || c.type === 'Department' ? departmentRequest(this.department, this.commonDepartment) : null;
    if (department && c.manager) {
      const m = lk.employees.find((e) => e.name === c.manager);
      department = { ...department, manager: { id: m?.mdmId ?? null, code: m?.id ?? null, name: c.manager, designation: m?.title || null } };
    }
    const site = c.type === 'Location' ? c.assignee || c.site : c.site;
    return {
      assignTo: assignToOf(c.type),
      employee,
      department,
      otherEntity:
        c.type === 'Other' && c.assignee
          ? {
              entityType: ENTITIES.find((e) => e.name === c.assignee)?.kind ?? 'Other', entityId: null, entityName: c.assignee, contactPerson: null, contactPhone: null,
              contactEmail: null, agreementReference: null, reason: null, serviceType: null, serviceRequestNumber: null, serviceLocation: null, expectedReturnDate: null,
              returnCondition: null, responsiblePerson: null,
            }
          : null,
      location: locationFrom(lk, { site, building: c.building, floor: c.floor, room: c.room }),
      startDate: c.from || null,
      endDate: c.setEnd ? c.to || null : null,
      isOpenEnded: !c.setEnd,
      assignmentReason: c.reason || null,
      reference: c.reference.trim() || null,
      notes: c.notes.trim() || null,
      overrideExistingAssignment: c.override,
      notifyAssignees: c.notifyAssignee,
      notifyDepartmentManager: c.notifyManager,
      notifyDepartment: c.notifyDepartment,
    };
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

  /** Creates the assignments on the server; rows with errors, excluded rows and (when not included) warnings are skipped. */
  confirm(): void {
    if (this.saving) return;
    if (!this.file || !this.batchId) return void (this.stepError = 'Upload a file first.');
    if (!this.toAssign.length) return void (this.stepError = 'There are no valid records to assign.');
    this.saving = true;
    this.stepError = '';
    const name = this.file.name;
    this.api
      .confirm(this.batchId, this.notify, this.includeWarnings, [...this.excluded])
      .pipe(finalize(() => (this.saving = false)))
      .subscribe({
        next: (r: BulkConfirmDto) => {
          const n = r.assignedCount;
          const skipped = r.skippedCount;
          this.router.navigate(['/assets/assignment'], {
            state: { toast: `${n} asset${n === 1 ? '' : 's'} assigned from ${name}${skipped ? `; ${skipped} record${skipped === 1 ? '' : 's'} skipped` : ''}.` },
          });
        },
        error: (err) => (this.stepError = apiErrorMessage(err, 'The assets could not be assigned.')),
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
