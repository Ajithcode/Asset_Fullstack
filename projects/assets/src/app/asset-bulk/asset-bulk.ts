import { Component, HostListener, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import * as XLSX from 'xlsx';
import { Observable, Subject, debounceTime, forkJoin, map, of, switchMap, throwError } from 'rxjs';
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
import { AssetRecord, AssetStatus, Criticality } from 'shared-ui';
import { iconForAsset } from '../asset-details/asset-details.service';
import { AssetDropdownService, DropdownOption } from '../services/asset-dropdowns/asset-dropdown';
import { AssetBulkImport, AssetExportFilters, ImportExportHistoryDto, ImportRecordDto, ImportValidateResult } from '../services/asset-bulk-import/asset-bulk-import';
import { apiErrorMessage } from '../services/asset-api';

type ImportType = 'create' | 'update' | 'upsert';
type ExportType = 'master' | 'related' | 'custom';
/** ExportType values the export API accepts. */
const EXPORT_TYPE_API: Record<ExportType, string> = { master: 'AssetMasterData', related: 'AssetWithRelatedData', custom: 'CustomExport' };
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
/** A field of the "Mapped To (Asset360 Field)" dropdown, from GET asset-import-export/fields. */
interface MapField {
  key: string;
  label: string;
  type: string;
  mandatory: boolean;
  allowed: string[];
  /** The page field it matches (Asset ID, Status, ...), used by the in-page checks and auto map. */
  local?: FieldKey;
}

interface ColumnMap {
  header: string;
  /** MapField key, or '' for "Do not import". */
  field: string;
  sample: string;
}

/** A field given one value for every row ("Add More Fields"). */
interface FixedValue {
  field: string;
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


/** Assets imported in this session (kept in memory until the import is wired to the Assets API). */
const REGISTER: AssetRecord[] = [];

const norm = (s: string) => s.toLowerCase().replace(/[^a-z0-9/]+/g, ' ').trim();
/** norm() without spaces, so "assetTag", "Asset Tag" and "asset_tag" compare equal. */
const squash = (s: string) => norm(s.replace(/([a-z])([A-Z])/g, '$1 $2')).replace(/ /g, '');

/** The page field a server field stands for, matched on key, label or alias. */
function localFieldOf(key: string, label: string): FieldKey | undefined {
  const names = [squash(key), squash(label)];
  return FIELDS.find((x) => [x.key, x.label, ...x.aliases].some((n) => names.includes(squash(n))))?.key;
}
const pad = (n: number) => String(n).padStart(2, '0');

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

type AlertKind = 'success' | 'error' | 'warning' | 'info';

interface PageAlert {
  id: number;
  kind: AlertKind;
  title: string;
  message: string;
  /** Time on screen before it closes itself. */
  ms: number;
  paused?: boolean;
  timer?: ReturnType<typeof setTimeout>;
}

const ALERT_TITLES: Record<AlertKind, string> = { success: 'Success', error: 'Error', warning: 'Warning', info: 'Info' };
const MAX_ALERTS = 4;

@Component({
  selector: 'app-asset-bulk',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule],
  templateUrl: './asset-bulk.html',
  // Split to keep each stylesheet under the anyComponentStyle budget
  styleUrls: ['./asset-bulk.css', './asset-bulk-panels.css', './asset-bulk-alerts.css'],
})
export class AssetBulk implements OnInit, OnDestroy {
  readonly icons = {
    ArrowLeft, ArrowRight, Calendar, Check, ChevronDown, CircleAlert, CircleCheck, CircleX, CloudUpload, Download, EllipsisVertical,
    FileDown, FileSpreadsheet, FileUp, History, Info, ListChecks, Plus, Table2, Trash2, Wand2, X,
  };
  readonly fields = FIELDS;
  readonly templates = TEMPLATES;
  readonly steps = ['Download Template', 'Upload File', 'Map Fields', 'Validate', 'Import'];
  /** Master data lists; empty until a master data API serves them. */
  /** Export filter dropdowns, loaded from the master data APIs when the page opens. */
  categories: string[] = [];
  /** Location filter: master-data locations; value is the location id (sent as filters.locationId). */
  sites: { label: string; value: string }[] = [];
  departments: string[] = [];
  readonly statuses = STATUSES;
  criticalities: string[] = [];

  // ===== Import =====
  step = 1;
  importType: ImportType = 'create';
  templateName = '';
  isDragging = false;
  file: File | null = null;
  fileError = '';
  /** Upload in progress (POST asset-import-export/imports). */
  uploading = false;
  /** Import session id returned by the upload; empty until the file is uploaded. */
  importId = '';
  /** Data rows of the uploaded sheet (without the heading row). */
  rows: string[][] = [];
  columns: ColumnMap[] = [];
  fixed: FixedValue[] = [];
  mappingError = '';
  mapMenuOpen = false;
  /** Options of the "Mapped To (Asset360 Field)" dropdown, loaded when the Map Fields tab opens. */
  mapFields: MapField[] = [];
  mapFieldsLoading = false;
  mapFieldsError = '';

  validated = false;
  clean: CleanRow[] = [];
  issues: RowIssue[] = [];

  // ===== Export =====
  exportType: ExportType = 'master';
  fieldSet: FieldSet | '' = 'standard';
  /** Server field keys picked under "Choose Fields…" (custom field set). */
  exportFields = new Set<string>();
  /** Checkboxes of "Choose Fields…": server fields from GET asset-import-export/fields. */
  exportFieldOptions: { key: string; label: string }[] = [];
  /** Assets matching the filters (GET exports/count); null while unknown. */
  exportCount: number | null = null;
  exporting = false;
  private readonly exportCount$ = new Subject<void>();
  private exportCountSub?: { unsubscribe(): void };
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
  private readonly bulkApi = inject(AssetBulkImport);
  private readonly dropdowns = inject(AssetDropdownService);
  /** Export filter dropdowns are loading. */
  filtersLoading = false;

  /** Template download in progress (GET asset-import-export/template). */
  templateLoading = false;
  templateError = '';

  // ===== History (GET asset-import-export/history) =====
  /** Latest jobs for the card on the page. */
  history: ImportExportHistoryDto[] = [];
  /** Current page of the history dialog. */
  historyPageItems: ImportExportHistoryDto[] = [];
  historyPage = 1;
  readonly historyPageSize = 10;
  historyTotal = 0;
  historyLoading = false;

  get historyPages(): number {
    return Math.max(1, Math.ceil(this.historyTotal / this.historyPageSize));
  }

  /** Reloads the 5 latest jobs shown on the page. */
  loadHistory(): void {
    this.bulkApi.history(1, 5).subscribe({
      next: (res) => (this.history = res.items),
      error: (err) => console.warn('[asset-bulk] history failed', err),
    });
  }

  /** Opens the history dialog on page 1. */
  openHistory(): void {
    this.historyOpen = true;
    this.loadHistoryPage(1);
  }

  loadHistoryPage(page: number): void {
    if (page < 1 || (page > this.historyPages && page !== 1)) return;
    this.historyPage = page;
    this.historyLoading = true;
    this.bulkApi.history(page, this.historyPageSize).subscribe({
      next: (res) => {
        this.historyLoading = false;
        this.historyPageItems = res.items;
        this.historyTotal = res.total;
      },
      error: (err) => {
        this.historyLoading = false;
        this.historyPageItems = [];
        this.showToast(apiErrorMessage(err, 'The import / export history could not be loaded.'), true);
      },
    });
  }

  /** Colour of a history status: green when done, red when failed, amber otherwise. */
  historyStatusClass(status: string): string {
    if (/^(completed|success|succeeded|done)$/i.test(status)) return 'map--mapped';
    if (/fail|error|cancel/i.test(status)) return 'map--failed';
    return 'map--duplicate';
  }

  /** Top-right alert stack, newest first. */
  alerts: PageAlert[] = [];
  private alertSeq = 0;

  ngOnInit(): void {
    this.loadFilterOptions();
    this.loadHistory();
    // Count refreshes 400 ms after the last filter change
    this.exportCountSub = this.exportCount$
      .pipe(
        debounceTime(400),
        switchMap(() => this.bulkApi.exportCount(this.exportFilters())),
      )
      .subscribe((n) => (this.exportCount = n));
    this.refreshExportCount();
  }

  /**
   * Export filters, all in parallel: category (Asset service asset-categories, top level only) and
   * location / department / criticality (Master Data service: master-data/locations, departments, criticalities).
   * Each call falls back to an empty list on error, so one failing API does not empty the others.
   */
  private loadFilterOptions(): void {
    this.filtersLoading = true;
    const sorted = (list: string[]) => [...new Set(list)].sort((a, b) => a.localeCompare(b));
    forkJoin({
      categories: this.dropdowns.getAssetCategories(),
      locations: this.bulkApi.masterOptions('locations', 'locationName'),
      departments: this.bulkApi.masterNames('departments', 'departmentName'),
      criticalities: this.bulkApi.masterNames('criticalities', 'criticalityName'),
    }).subscribe({
      next: (r) => {
        this.filtersLoading = false;
        this.categories = sorted(r.categories.filter((c) => !(c as DropdownOption & { parentId?: string }).parentId).map((c) => c.label));
        this.sites = [...r.locations].sort((a, b) => a.label.localeCompare(b.label));
        this.departments = sorted(r.departments);
        this.criticalities = [...new Set(r.criticalities)];
      },
      error: () => (this.filtersLoading = false),
    });
  }

  ngOnDestroy(): void {
    // leaving the page abandons the upload in progress
    this.discardImport();
    this.alerts.forEach((a) => clearTimeout(a.timer));
    this.exportCountSub?.unsubscribe();
  }

  // ===== Lookups =====

  field(key: FieldKey | ''): FieldDef | undefined {
    return FIELDS.find((f) => f.key === key);
  }

  readonly fieldLabel = (f: FieldDef): string => f.label;
  readonly mapFieldLabel = (f: MapField): string => f.label;

  mapField(key: string): MapField | undefined {
    return key ? this.mapFields.find((f) => f.key === key) : undefined;
  }

  /** Page field key of a dropdown field ('status', 'tag', ...), or '' when it has none. */
  localKey(key: string): FieldKey | '' {
    return this.mapField(key)?.local ?? '';
  }

  get template(): TemplateDef {
    return TEMPLATES.find((t) => t.name === this.templateName) ?? TEMPLATES[0];
  }

  // ===== Step 1: template =====

  /** Downloads the server's template (GET asset-import-export/template?includeSampleData=true) on click only. */
  downloadTemplate(): void {
    if (!this.importType) return this.showToast('Choose the import type first.', true);
    if (!this.templateName) return this.showToast('Choose the template first.', true);
    if (this.templateLoading) return;
    this.templateLoading = true;
    this.templateError = '';
    this.bulkApi.template(true).subscribe({
      next: ({ blob, fileName }) => {
        this.templateLoading = false;
        const link = document.createElement('a');
        link.href = URL.createObjectURL(blob);
        link.download = fileName;
        link.click();
        setTimeout(() => URL.revokeObjectURL(link.href), 1000);
        if (this.step < 2) this.step = 2;
        this.showToast('Template downloaded. Fill it in and upload it here.');
      },
      error: (err) => {
        this.templateLoading = false;
        this.templateError = apiErrorMessage(err, 'The template could not be downloaded.');
        this.showToast(this.templateError, true);
      },
    });
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
      // Use the first sheet that has data rows (skips an Instructions sheet placed first), else the first with headings
      const grids = book.SheetNames.map((name) => {
        const grid = XLSX.utils.sheet_to_json<unknown[]>(book.Sheets[name], { header: 1, raw: true, defval: '' }).map((r) => r.map(cellText));
        const headAt = grid.findIndex((r) => r.some((c) => c)); // skip blank rows above the headings
        const head = headAt >= 0 ? grid[headAt] : [];
        return { head, rows: grid.slice(headAt + 1).filter((r) => r.some((c) => c)) };
      });
      const picked = grids.find((g) => g.head.length && g.rows.length) ?? grids.find((g) => g.head.length);
      if (!picked) {
        this.fileError = 'The file has no heading row. Use the downloaded template.';
        return;
      }
      const { head, rows } = picked;
      if (!rows.length) {
        // The API rejects it too ("The file has no data rows"), so stop here
        this.fileError = `${file.name} has only the heading row. Add at least one asset row under the headings, save, and upload again.`;
        return;
      }
      this.file = file;
      this.rows = rows;
      this.columns = head.map((h, i) => ({ header: h || `Column ${i + 1}`, field: '', sample: rows.find((r) => r[i])?.[i] ?? '' }));
      this.fixed = [];
      if (this.mapFields.length) this.autoMap(false);
      this.resetValidation();
      this.importResult = '';
      this.discardImport();
      this.showToast(`${rows.length} rows read from ${file.name}. Click Next to upload it.`);
    } catch {
      this.fileError = 'The file could not be read as a spreadsheet.';
    }
  }

  removeFile(): void {
    this.file = null;
    this.discardImport();
    this.rows = [];
    this.columns = [];
    this.fixed = [];
    this.mappingError = '';
    this.resetValidation();
    this.step = 2;
  }

  /** Deletes the uploaded import session that will not be confirmed (file removed or replaced, page left). */
  private discardImport(): void {
    const id = this.importId;
    this.importId = '';
    if (id) this.bulkApi.deleteImport(id).subscribe({ error: (err) => console.warn('[asset-bulk] could not discard import', id, err) });
  }

  /** Validate → View Details: the server's error report of this import as an Excel file. */
  downloadErrors(): void {
    if (!this.importId || this.errorsDownloading) return;
    this.errorsDownloading = true;
    this.bulkApi.exportErrors(this.importId).subscribe({
      next: ({ blob, fileName }) => {
        this.errorsDownloading = false;
        const link = document.createElement('a');
        link.href = URL.createObjectURL(blob);
        link.download = fileName;
        link.click();
        setTimeout(() => URL.revokeObjectURL(link.href), 1000);
      },
      error: (err) => {
        this.errorsDownloading = false;
        this.showToast(apiErrorMessage(err, 'The error report could not be downloaded.'), true);
      },
    });
  }

  formatSize(bytes: number): string {
    return bytes >= 1024 * 1024 ? `${(bytes / 1024 / 1024).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`;
  }

  /** Upload tab: Next uploads the file (POST asset-import-export/imports), then goes to Map Fields. */
  importNext(): void {
    if (!this.file) {
      this.fileError = 'Upload a file first.';
      return;
    }
    if (this.importId) return this.goStep(3);
    if (this.uploading) return;
    if (!this.templateName) {
      this.fileError = 'Choose the template in step 1 first.';
      return;
    }
    this.uploading = true;
    this.fileError = '';
    const file = this.file;
    this.bulkApi.upload(file, this.importType, this.templateName).subscribe({
      next: (res) => {
        this.uploading = false;
        if (this.file !== file) return; // file changed while uploading
        if (!res.id) {
          console.warn('[asset-bulk] upload response has no import id', res.raw);
          this.fileError = 'The server accepted the file but sent no import id, so mapping / validate / import cannot run.';
          this.notify('error', this.fileError, 'Upload failed');
          return;
        }
        this.importId = res.id;
        this.showToast(`${file.name} uploaded.`);
        this.goStep(3);
      },
      error: (err) => {
        this.uploading = false;
        this.fileError = apiErrorMessage(err, 'The file could not be uploaded.');
        this.notify('error', this.fileError, 'Upload failed');
      },
    });
  }

  /** Opens an import tab. Later tabs show what they still need when opened early. */
  goStep(n: number): void {
    if (n !== 5 || !this.importResult) this.importResult = '';
    this.step = n;
    if (n === 3) this.loadMapFields();
  }

  /** GET asset-import-export/fields?importableOnly=false when Map Fields opens; fills the "Mapped To" dropdown. */
  loadMapFields(): void {
    if (this.mapFieldsLoading || this.mapFields.length) return;
    this.mapFieldsLoading = true;
    this.mapFieldsError = '';
    this.bulkApi.fields(false).subscribe({
      next: (list) => {
        this.mapFieldsLoading = false;
        this.mapFields = list
          .filter((f) => f.importable)
          .map((f) => ({ key: f.key, label: f.label, type: f.dataType, mandatory: f.required, allowed: f.allowedValues, local: localFieldOf(f.key, f.label) }));
        if (!this.mapFields.length) this.mapFieldsError = 'The server returned no import fields.';
        // Map the file columns once the dropdown has its options
        else if (this.columns.length && !this.mappedCount) this.autoMap(false);
      },
      error: (err) => {
        this.mapFieldsLoading = false;
        this.mapFieldsError = apiErrorMessage(err, 'The import fields could not be loaded.');
        this.notify('error', this.mapFieldsError);
      },
    });
  }

  /** True when the work of that step is finished. */
  stepDone(n: number): boolean {
    switch (n) {
      case 1: return !!this.templateName || !!this.file;
      case 2: return !!this.file;
      case 3: return this.validated;
      case 4: return this.validated && !!this.serverCheck && this.validCount > 0;
      default: return !!this.importResult;
    }
  }

  // ===== Step 3: mapping =====

  /** Matches each column heading to a field by its label, key or a known alias. */
  autoMap(announce = true): void {
    this.mapMenuOpen = false;
    const taken = new Set<string>();
    for (const col of this.columns) {
      const h = squash(col.header.replace('*', ''));
      const aliasOf = (f: MapField) => (f.local ? FIELDS.find((x) => x.key === f.local)!.aliases.map(squash) : []);
      const f = this.mapFields.find((x) => !taken.has(x.key) && (squash(x.label) === h || squash(x.key) === h || aliasOf(x).includes(h)));
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
  get duplicateFields(): Set<string> {
    const seen = new Set<string>();
    const dup = new Set<string>();
    for (const key of [...this.columns.map((c) => c.field), ...this.fixed.map((f) => f.field)]) {
      if (!key) continue;
      if (seen.has(key)) dup.add(key);
      seen.add(key);
    }
    return dup;
  }

  mapStatus(field: string): 'Mapped' | 'Not Mapped' | 'Duplicate' {
    if (!field) return 'Not Mapped';
    return this.duplicateFields.has(field) ? 'Duplicate' : 'Mapped';
  }

  get mappedCount(): number {
    return this.columns.filter((c) => c.field).length;
  }

  get missingMandatory(): MapField[] {
    const mapped = new Set([...this.columns.map((c) => c.field), ...this.fixed.filter((f) => f.value.trim()).map((f) => f.field)]);
    // Updating existing assets only needs the Asset ID; the rest keep their values
    return this.mapFields.filter((f) => f.mandatory && !mapped.has(f.key) && (this.importType !== 'update' || f.local === 'tag'));
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

  // ===== Step 4: validate =====

  private resetValidation(): void {
    this.validated = false;
    this.serverCheck = null;
    this.serverCheckError = '';
    this.serverIssues = null;
    this.clean = [];
    this.issues = [];
    this.mappingError = '';
    if (this.step > 3) this.step = 3;
  }

  validate(): void {
    if (!this.file) return;
    // Mapping problems do not block the move: the Validate tab shows them
    if (this.duplicateFields.size) {
      this.mappingError = `Each field can be mapped once: ${[...this.duplicateFields].map((k) => this.mapField(k)?.label ?? k).join(', ')}.`;
      this.step = 4;
      return;
    }
    const missing = this.missingMandatory;
    if (missing.length) {
      this.mappingError = `Map the mandatory field${missing.length > 1 ? 's' : ''}: ${missing.map((f) => f.label).join(', ')}.`;
      this.step = 4;
      return;
    }
    this.mappingError = '';
    const clean: CleanRow[] = [];
    const issues: RowIssue[] = [];
    const seenTags = new Set<string>();
    this.rows.forEach((cells, i) => {
      const rowNo = i + 2; // heading is row 1
      // Values keyed by page field ('tag', 'status', ...) when the server field matches one, else by the server key
      const values: Record<string, string> = {};
      const keyOf = (k: string) => this.localKey(k) || k;
      this.columns.forEach((c, n) => c.field && (values[keyOf(c.field)] = cells[n] ?? ''));
      this.fixed.forEach((f) => f.field && f.value.trim() && !values[keyOf(f.field)] && (values[keyOf(f.field)] = f.value.trim()));
      const tag = (values['tag'] ?? '').trim().toUpperCase();
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
      values['tag'] = tag;
      for (const key of ['purchaseDate', 'nextMaintenance'] as const) if (values[key]) values[key] = parseDate(values[key]!) ?? '';
      clean.push({ row: rowNo, values, exists: REGISTER.some((a) => a.tag === tag) });
    });
    this.clean = clean;
    this.issues = issues;
    this.validated = true;
    this.step = 4;
    this.runServerCheck();
  }

  // ===== Server mapping + validation =====

  /** Server result of PUT mapping + POST validate for the current mapping; null until it ran. */
  serverCheck: ImportValidateResult | null = null;
  serverChecking = false;
  serverCheckError = '';

  /** Column heading in the file -> server field key (null = do not import). */
  private mappingBody(): Record<string, string | null> {
    const mapping: Record<string, string | null> = {};
    // The server reads headings without the "required" star ("Asset Name *" is column "Asset Name")
    for (const c of this.columns) mapping[c.header.replace(/\s*\*+\s*$/, '').trim()] = c.field || null;
    return mapping;
  }

  /** PUT imports/{id}/mapping, then POST imports/{id}/validate. */
  private serverCheck$(): Observable<ImportValidateResult> {
    const importId = this.importId;
    if (!importId) return throwError(() => new Error('The file was not uploaded to the server (no import id). Go back to Upload File, remove the file and upload it again.'));
    return this.bulkApi.saveMapping(importId, this.importType, this.mappingBody()).pipe(
      switchMap(() => this.bulkApi.validate(importId)),
      map((check) => {
        this.serverCheck = check;
        return check;
      }),
    );
  }

  /** Server row errors (GET imports/{id}/records); they replace the in-page issues in View Details. */
  serverIssues: RowIssue[] | null = null;
  errorsDownloading = false;

  /** One View Details line per server issue (row, Asset Tag, "Column: message"). */
  private setServerIssues(rows: ImportRecordDto[]): void {
    this.serverIssues = rows.flatMap((r) =>
      (r.issues.length ? r.issues : [r.message || r.status || 'Invalid row.']).map((message) => ({
        row: r.row,
        tag: r.tag || '-',
        kind: /duplicate/i.test(r.status) ? ('Duplicate' as const) : ('Invalid' as const),
        message,
      })),
    );
    this.issues = this.serverIssues;
  }

  /** Rows the server rejected (distinct row numbers). */
  get serverBadRows(): number {
    return new Set((this.serverIssues ?? []).map((i) => i.row)).size;
  }

  private loadServerIssues(importId: string): void {
    this.bulkApi.records(importId).subscribe({
      next: (records) => {
        if (importId !== this.importId || !records.length) return;
        const bad = records.filter((r) => !r.valid);
        this.setServerIssues(bad);
        // Fill in counts the validate response did not send
        const check = this.serverCheck;
        if (check && check.valid === null) check.valid = records.length - bad.length;
        if (check && check.invalid === null) check.invalid = bad.length;
      },
      error: (err) => console.warn('[asset-bulk] could not load server row errors', err),
    });
  }

  /** Next: Validate also saves the mapping and validates on the server. */
  runServerCheck(): void {
    if (this.serverChecking) return;
    this.serverChecking = true;
    this.serverCheckError = '';
    this.serverCheck = null;
    const importId = this.importId;
    this.serverCheck$().subscribe({
      next: (check) => {
        this.serverChecking = false;
        // Row errors come in the validate response ("problems"); else ask GET imports/{id}/records
        if (check.problems.length || check.valid !== null) this.setServerIssues(check.problems);
        else this.loadServerIssues(importId);
        const bad = this.invalidTotal;
        if (bad) this.notify('warning', `${this.validCount} valid, ${bad} invalid record${bad > 1 ? 's' : ''}. See View Details.`, 'Validation finished');
        else this.notify('success', `${this.validCount} records are ready to import.`, 'Validation passed');
      },
      error: (err) => {
        this.serverChecking = false;
        this.serverCheckError = err instanceof Error && !('status' in err) ? err.message : apiErrorMessage(err, 'The server could not validate the file.');
        this.notify('error', this.serverCheckError, 'Validation failed');
      },
    });
  }

  /** First problem with a row, or '' when it is fine. */
  private rowProblem(v: Record<string, string>, tag: string): string {
    const exists = REGISTER.some((a) => a.tag === tag);
    for (const f of this.mapFields) {
      const value = (v[f.local ?? f.key] ?? '').trim();
      const isTag = f.local === 'tag';
      const needed = f.mandatory && (this.importType !== 'update' || isTag) && !(this.importType === 'upsert' && exists && !isTag);
      if (needed && !value) return `${f.label} is missing.`;
      if (!value) continue;
      if (/date/i.test(f.type) && !parseDate(value)) return `${f.label} "${value}" is not a valid date.`;
      if (f.allowed.length && !f.allowed.some((a) => a.toLowerCase() === value.toLowerCase())) return `${f.label} "${value}" is not one of: ${f.allowed.join(', ')}.`;
      if (f.allowed.length) continue;
      if (f.local === 'status' && !STATUSES.includes(value as AssetStatus)) return `Status "${value}" is not one of: ${STATUSES.join(', ')}.`;
      if (f.local === 'criticality' && !CRITICALITIES.includes(value as Criticality)) return `Criticality "${value}" is not one of: ${CRITICALITIES.join(', ')}.`;
      if (f.local === 'category' && this.categories.length && !this.categories.includes(value)) return `Category "${value}" does not exist.`;
    }
    if (!/^[A-Z0-9][A-Z0-9-]*$/.test(tag)) return `Asset ID "${tag}" may only use letters, digits and dashes.`;
    if (this.importType === 'create' && exists) return `Asset ID ${tag} already exists.`;
    if (this.importType === 'update' && !exists) return `Asset ID ${tag} was not found.`;
    return '';
  }

  /** Valid rows: the server count when it sent one, else rows minus the server's invalid count, else the in-page count. */
  get validCount(): number {
    const c = this.serverCheck;
    if (c?.valid !== null && c?.valid !== undefined) return c.valid;
    if (c?.invalid !== null && c?.invalid !== undefined) return Math.max(0, this.rows.length - c.invalid);
    return this.clean.length;
  }

  /** Invalid rows: the server count when it sent one, else the in-page count. */
  get invalidTotal(): number {
    const c = this.serverCheck;
    if (c?.invalid !== null && c?.invalid !== undefined) return c.invalid;
    if (c?.valid !== null && c?.valid !== undefined) return Math.max(0, this.rows.length - c.valid);
    return this.invalidCount;
  }

  /** Duplicate rows: the server count when it sent one, else the in-page count. */
  get duplicateTotal(): number {
    return this.serverCheck?.duplicate ?? this.duplicateCount;
  }

  get invalidCount(): number {
    return this.issues.filter((i) => i.kind === 'Invalid').length;
  }

  get duplicateCount(): number {
    return this.issues.filter((i) => i.kind === 'Duplicate').length;
  }

  // ===== Step 5: import =====

  /** Why the import cannot run yet; '' when it can. */
  get importBlocker(): string {
    if (!this.file) return 'Upload a file first.';
    if (this.mappingError) return this.mappingError;
    if (!this.validated) return 'Validate the file first (Map Fields, then Next: Validate).';
    // The server decides what is valid; the in-page row checks are only a preview
    if (this.serverChecking) return 'The server is still validating the file.';
    if (this.serverCheckError) return this.serverCheckError;
    return '';
  }

  /** Final Import button: POST asset-import-export/imports/{importId}/confirm. */
  runImport(): void {
    const blocker = this.importBlocker;
    if (blocker || !this.file) return this.showToast(blocker, true);
    if (!this.importId) return this.showToast('The file was not uploaded to the server. Go back to Upload File and click Next.', true);
    if (this.importing) return;
    this.importing = true;
    const file = this.file;
    const importId = this.importId;
    // Mapping + validate already ran on Next: Validate; run them again only if they did not
    this.importStage = this.serverCheck ? 'Importing…' : 'Validating…';
    (this.serverCheck ? of(this.serverCheck) : this.serverCheck$())
      .pipe(
        // Always call confirm; the server answers with its own error when no row is valid
        switchMap(() => {
          this.importStage = 'Importing…';
          return this.bulkApi.confirm(importId);
        }),
      )
      .subscribe({
      next: (res) => {
        this.importing = false;
        this.importStage = '';
        this.loadHistory();
        const parts = [
          res.created !== null ? `${res.created} created` : '',
          res.updated !== null ? `${res.updated} updated` : '',
          res.failed ? `${res.failed} failed` : '',
        ].filter(Boolean);
        this.step = 5;
        this.importResult = parts.length ? `Import complete: ${parts.join(', ')}. See the Asset List.` : res.message || 'Import complete. See the Asset List.';
        this.notify(res.failed ? 'warning' : 'success', this.importResult, res.failed ? 'Import finished with errors' : 'Import complete');
        // Start again for the next file
        this.file = null;
        this.importId = '';
        this.rows = [];
        this.columns = [];
        this.fixed = [];
        this.validated = false;
        this.clean = [];
        this.issues = [];
        this.lastImport = file.name;
      },
      error: (err) => {
        this.importing = false;
        const stage = this.importStage;
        this.importStage = '';
        const fallback = stage === 'Validating…' ? 'The server could not validate the file.' : 'The import could not be completed.';
        this.notify('error', err instanceof Error && !('status' in err) ? err.message : apiErrorMessage(err, fallback), 'Import failed');
      },
    });
  }

  /** Mapping -> validate -> confirm calls in progress. */
  importing = false;
  /** Label of the running call, shown on the Import button. */
  importStage = '';
  /** File name of the last completed import, shown on the import card. */
  lastImport = '';
  /** Summary of the last import, shown on the Import tab until another tab is opened. */
  importResult = '';

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
    const record: AssetRecord = {
      id: Math.max(0, ...REGISTER.map((a) => a.id)) + 1,
      tag: v.tag!, name: v.name ?? v.tag!, category: v.category ?? '', type: v.type ?? '',
      site: v.site ?? '', area: v.area ?? '', custodian: v.custodian ?? '', department: v.department ?? '',
      status: (v.status as AssetStatus) || 'Active', criticality: (v.criticality as Criticality) || 'Medium',
      manufacturer: v.manufacturer ?? '', model: v.model ?? '', nextMaintenance: v.nextMaintenance || null,
      icon: iconForAsset(v.category, v.type, v.name), image: '',
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
    if (set === 'custom') this.loadExportFieldOptions();
  }

  setExportType(type: ExportType): void {
    this.exportType = type;
    if (type === 'custom') this.setFieldSet('custom');
    else if (this.fieldSet === 'custom') this.setFieldSet('standard');
  }

  /** Fills "Choose Fields…" from GET asset-import-export/fields (once). */
  private loadExportFieldOptions(): void {
    if (this.exportFieldOptions.length) return;
    this.bulkApi.fields(false).subscribe({
      next: (list) => (this.exportFieldOptions = list.map((f) => ({ key: f.key, label: f.label }))),
      error: (err) => this.showToast(apiErrorMessage(err, 'The export fields could not be loaded.'), true),
    });
  }

  toggleExportField(key: string, on: boolean): void {
    const next = new Set(this.exportFields);
    if (on) next.add(key);
    else next.delete(key);
    this.exportFields = next;
  }

  /** Called on every filter change; the count call is debounced. */
  refreshExportCount(): void {
    this.exportCount$.next();
  }

  /** Filters as the export API takes them (empty ones left out). */
  private exportFilters(): AssetExportFilters {
    const iso = (d: string, end = false) => (d ? new Date(`${d}T${end ? '23:59:59' : '00:00:00'}`).toISOString() : undefined);
    const f: AssetExportFilters = {
      category: this.filterCategory || undefined,
      locationId: this.filterSite || undefined,
      status: this.filterStatus || undefined,
      criticality: this.filterCriticality || undefined,
      department: this.filterDepartment || undefined,
      nextMaintenanceFrom: iso(this.dateFrom),
      nextMaintenanceTo: iso(this.dateTo, true),
    };
    return Object.fromEntries(Object.entries(f).filter(([, v]) => v)) as AssetExportFilters;
  }

  get hasExportFilters(): boolean {
    return !!(this.filterCategory || this.filterSite || this.filterStatus || this.filterCriticality || this.filterDepartment || this.dateFrom || this.dateTo);
  }

  clearExportFilters(): void {
    this.filterCategory = this.filterSite = this.filterStatus = this.filterCriticality = this.filterDepartment = this.dateFrom = this.dateTo = '';
    this.refreshExportCount();
  }

  /** Export button: POST asset-import-export/exports, then downloads the file the server returns. */
  runExport(): void {
    if (!this.fieldSet) return this.showToast('Choose the fields to export.', true);
    if (this.fieldSet === 'custom' && !this.exportFields.size) return this.showToast('Choose at least one field to export.', true);
    if (this.dateFrom && this.dateTo && this.dateTo < this.dateFrom) return this.showToast('The end date is before the start date.', true);
    if (this.exporting) return;
    this.exporting = true;
    this.bulkApi
      .export({
        exportType: EXPORT_TYPE_API[this.exportType],
        fieldSet: this.fieldSet,
        fields: this.fieldSet === 'custom' ? [...this.exportFields] : null,
        filters: this.exportFilters(),
        fileFormat: this.exportFormat,
      })
      .subscribe({
        next: (res) => {
          this.exporting = false;
          if (res.blob) {
            const link = document.createElement('a');
            link.href = URL.createObjectURL(res.blob);
            link.download = res.fileName;
            link.click();
            setTimeout(() => URL.revokeObjectURL(link.href), 1000);
          } else if (res.downloadUrl) {
            window.open(res.downloadUrl, '_blank');
          } else {
            console.info('[asset-bulk] export response', res.json);
          }
          this.loadHistory();
          this.showToast(res.blob || res.downloadUrl ? `Exported to ${res.fileName}.` : 'Export started. It will appear in Import / Export History.');
        },
        error: (err) => {
          this.exporting = false;
          this.notify('error', apiErrorMessage(err, 'The export failed.'), 'Export failed');
        },
      });
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

  /** Short success / error alert (kept for the existing call sites). */
  private showToast(message: string, error = false): void {
    this.notify(error ? 'error' : 'success', message);
  }

  // ===== Alerts (top-right) =====

  /** Shows an alert in the top-right stack. Errors stay longer; the same text is not stacked twice. */
  notify(kind: AlertKind, message: string, title = ALERT_TITLES[kind]): void {
    if (!message) return;
    const same = this.alerts.find((a) => a.kind === kind && a.message === message);
    if (same) this.dismissAlert(same.id);
    const alert: PageAlert = { id: ++this.alertSeq, kind, title, message, ms: kind === 'error' || kind === 'warning' ? 8000 : 5000 };
    this.alerts = [alert, ...this.alerts];
    // Keep the stack short
    this.alerts.slice(MAX_ALERTS).forEach((a) => this.dismissAlert(a.id));
    this.startAlertTimer(alert);
  }

  dismissAlert(id: number): void {
    const alert = this.alerts.find((a) => a.id === id);
    if (!alert) return;
    clearTimeout(alert.timer);
    this.alerts = this.alerts.filter((a) => a.id !== id);
  }

  /** Hovering an alert keeps it open; leaving restarts its timer. */
  holdAlert(alert: PageAlert, hold: boolean): void {
    clearTimeout(alert.timer);
    alert.paused = hold;
    if (!hold) this.startAlertTimer(alert);
  }

  alertIcon(kind: AlertKind) {
    return kind === 'success' ? CircleCheck : kind === 'error' ? CircleX : kind === 'warning' ? CircleAlert : Info;
  }

  readonly trackAlert = (_: number, a: PageAlert): number => a.id;

  private startAlertTimer(alert: PageAlert): void {
    alert.timer = setTimeout(() => this.dismissAlert(alert.id), alert.ms);
  }
}
