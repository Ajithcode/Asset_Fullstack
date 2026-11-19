import { Component, HostListener, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { Observable, Subscription, catchError, forkJoin, of } from 'rxjs';
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
  RefreshCw,
  Table2,
  Trash2,
  Wand2,
  X,
} from 'lucide-angular';
import { apiErrorMessage } from '../services/asset-api';
import { AssetDropdownService, DropdownOption, LocationOption } from '../services/asset-dropdowns/asset-dropdown';
import {
  AssetBulkApi,
  BulkExportFilters,
  BulkExportType,
  BulkField,
  BulkFieldSet,
  BulkFile,
  BulkFileFormat,
  BulkHistoryItem,
  BulkImportType,
  BulkJob,
  BulkMapping,
  BulkRecord,
  BulkSummary,
  BulkTemplate,
} from './asset-bulk-api';

/** One column of the uploaded file and the Asset360 field it fills (field key, '' = not imported). */
interface ColumnMap {
  header: string;
  field: string;
  sample: string;
}

const DEFAULT_MAX_FILE_BYTES = 10 * 1024 * 1024;

/**
 * Assets › Bulk Import / Export. Every step runs on the Asset service (/api/asset-import-export):
 * the file is uploaded and kept on the server, mapped, validated against the registration rules and master data,
 * then imported into the asset register. Export builds the file on the server from the live assets.
 */
@Component({
  selector: 'app-asset-bulk',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule],
  templateUrl: './asset-bulk.html',
  // Split to keep each stylesheet under the anyComponentStyle budget
  styleUrls: ['./asset-bulk.css', './asset-bulk-panels.css'],
})
export class AssetBulk implements OnInit, OnDestroy {
  readonly icons = {
    ArrowLeft, ArrowRight, Calendar, Check, ChevronDown, CircleAlert, CircleCheck, CircleX, CloudUpload, Download, EllipsisVertical,
    FileDown, FileSpreadsheet, FileUp, History, Info, ListChecks, Plus, RefreshCw, Table2, Trash2, Wand2, X,
  };
  readonly steps = ['Download Template', 'Upload File', 'Map Fields', 'Validate', 'Import'];

  private readonly api = inject(AssetBulkApi);
  private readonly dropdowns = inject(AssetDropdownService);
  private readonly subs = new Subscription();

  // ===== Lists from the API =====
  templates: BulkTemplate[] = [];
  /** Fields a column can be mapped to, with Mandatory for the current import type. */
  fields: BulkField[] = [];
  /** Built-in fields that can be exported (custom fields are import-only). */
  exportableFields: BulkField[] = [];
  categories: string[] = [];
  sites: LocationOption[] = [];
  statuses: string[] = [];
  criticalities: string[] = [];
  departments: string[] = [];
  maxFileBytes = DEFAULT_MAX_FILE_BYTES;
  maxImportRows = 0;

  // ===== Import =====
  step = 1;
  importType: BulkImportType = 'Create';
  templateName = '';
  isDragging = false;
  busy = false;
  job: BulkJob | null = null;
  fileError = '';
  columns: ColumnMap[] = [];
  /** Labels of the mandatory fields not mapped yet (from the server). */
  missingMandatory: string[] = [];
  mappingError = '';
  mapMenuOpen = false;

  validated = false;
  summary: BulkSummary | null = null;
  preview: BulkRecord[] = [];
  problems: BulkRecord[] = [];
  /** File name of the last completed import, shown on the import card. */
  lastImport = '';

  // ===== Export =====
  exportType: BulkExportType = 'AssetMasterData';
  fieldSet: BulkFieldSet | '' = '';
  exportFields = new Set<string>();
  filterCategory = '';
  filterSite = '';
  filterStatus = '';
  dateFrom = '';
  dateTo = '';
  advancedOpen = false;
  filterCriticality = '';
  filterDepartment = '';
  exportFormat: BulkFileFormat = 'xlsx';
  exportCount: number | null = null;
  exporting = false;
  private countSub?: Subscription;

  // ===== History / dialogs =====
  history: BulkHistoryItem[] = [];
  historyLoading = false;
  historyOpen = false;
  issuesOpen = false;

  toast = '';
  toastError = false;
  private toastTimer?: ReturnType<typeof setTimeout>;

  ngOnInit(): void {
    this.subs.add(
      this.api.options().subscribe({
        next: (o) => {
          this.templates = o.templates ?? [];
          this.maxFileBytes = o.maxFileSizeBytes || DEFAULT_MAX_FILE_BYTES;
          this.maxImportRows = o.maxImportRows || 0;
        },
        error: (err) => this.showToast(apiErrorMessage(err, 'Could not load the import options.'), true),
      }),
    );
    this.loadFields();
    const noOptions = (): Observable<DropdownOption[]> => of([]);
    this.subs.add(
      forkJoin({
        categories: this.dropdowns.getAssetCategories().pipe(catchError(noOptions)),
        sites: this.dropdowns.getSites().pipe(catchError((): Observable<LocationOption[]> => of([]))),
        criticalities: this.dropdowns.getCriticalities().pipe(catchError(noOptions)),
        departments: this.dropdowns.getDepartments().pipe(catchError(noOptions)),
      }).subscribe((d) => {
        // Assets store category, criticality and department by name; locations are filtered by id
        this.categories = unique(d.categories.filter((c) => !c.parentId).map((c) => c.label));
        this.sites = d.sites;
        this.criticalities = unique(d.criticalities.map((c) => c.label));
        this.departments = unique(d.departments.map((c) => c.label));
      }),
    );
    this.loadHistory();
    this.refreshCount();
  }

  ngOnDestroy(): void {
    clearTimeout(this.toastTimer);
    this.countSub?.unsubscribe();
    this.subs.unsubscribe();
  }

  // ===== Lookups =====

  field(key: string | null | undefined): BulkField | undefined {
    return key ? this.fields.find((f) => f.key === key) : undefined;
  }

  get importableFields(): BulkField[] {
    return this.fields.filter((f) => f.importable);
  }

  get template(): BulkTemplate | undefined {
    return this.templates.find((t) => t.key === this.templateName);
  }

  private loadFields(): void {
    this.subs.add(
      this.api.fields(this.importType).subscribe({
        next: (fields) => {
          if (!this.job) this.fields = fields;
          if (!this.exportableFields.length) {
            this.exportableFields = fields.filter((f) => !f.key.startsWith('custom:'));
            this.statuses = this.exportableFields.find((f) => f.key === 'status')?.options ?? [];
          }
        },
        error: (err) => this.showToast(apiErrorMessage(err, 'Could not load the asset fields.'), true),
      }),
    );
  }

  // ===== Step 1: template =====

  downloadTemplate(): void {
    const template = this.template;
    if (!template) return this.showToast('Choose the template first.', true);
    this.api.template(template.key, 'xlsx').subscribe({
      next: (file) => {
        AssetBulkApi.save(file);
        if (this.step < 2) this.step = 2;
        this.showToast('Template downloaded. Fill it in and upload it here.');
      },
      error: (err) => this.showToast(apiErrorMessage(err, 'The template could not be downloaded.'), true),
    });
  }

  onTemplateChange(): void {
    // The Update template is for Update Existing Assets
    const t = this.template;
    if (t && t.importType !== this.importType && !this.job) {
      this.importType = t.importType;
      this.loadFields();
    }
  }

  // ===== Step 2: upload =====

  onFileInput(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.upload(input.files?.[0]);
    input.value = '';
  }

  onDrop(event: DragEvent): void {
    event.preventDefault();
    this.isDragging = false;
    this.upload(event.dataTransfer?.files?.[0]);
  }

  private upload(file: File | undefined): void {
    if (!file || this.busy) return;
    if (!/\.(xlsx|csv)$/i.test(file.name)) {
      this.fileError = 'Choose an Excel (.xlsx) or CSV (.csv) file.';
      return;
    }
    if (file.size > this.maxFileBytes) {
      this.fileError = `Files can be up to ${this.formatSize(this.maxFileBytes)}.`;
      return;
    }
    this.fileError = '';
    this.discardJob();
    this.busy = true;
    this.api.upload(file, this.importType, this.template?.key).subscribe({
      next: (res) => {
        this.busy = false;
        const failed = (res.fileChecks ?? []).filter((c) => !c.passed);
        if (failed.length) this.fileError = failed.map((c) => c.message || c.name).join(' ');
        this.job = res.import;
        this.applyMapping(res.mapping);
        this.step = 3;
        this.showToast(`${res.import.totalRecords} rows read from ${res.import.fileName}.`);
        setTimeout(() => document.getElementById('bulk-mapping')?.scrollIntoView({ behavior: 'smooth', block: 'start' }));
      },
      error: (err) => {
        this.busy = false;
        this.fileError = apiErrorMessage(err, 'The file could not be uploaded.');
      },
    });
  }

  /** × on the file: deletes the uploaded (not yet imported) file on the server. */
  removeFile(): void {
    this.discardJob();
    this.step = 2;
  }

  private discardJob(): void {
    const job = this.job;
    if (job && !job.completedAt) this.api.deleteImport(job.id).subscribe({ error: () => undefined });
    this.job = null;
    this.columns = [];
    this.missingMandatory = [];
    this.mappingError = '';
    this.resetValidation();
    this.loadFields();
  }

  formatSize(bytes: number): string {
    return bytes >= 1024 * 1024 ? `${(bytes / 1024 / 1024).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`;
  }

  importNext(): void {
    if (this.step === 1) {
      this.step = 2;
      return;
    }
    if (!this.job) {
      this.fileError = 'Upload a file first.';
      return;
    }
    this.step = Math.max(this.step, 3);
    document.getElementById('bulk-mapping')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  onImportTypeChange(): void {
    if (!this.job) {
      this.loadFields();
      return;
    }
    // The import type changes which fields are mandatory: save it with the mapping
    this.saveMapping();
  }

  // ===== Step 3: mapping =====

  private applyMapping(m: BulkMapping): void {
    this.job = m.import ?? this.job;
    if (m.fields?.length) this.fields = m.fields;
    this.columns = (m.columns ?? []).map((c) => ({ header: c.column, field: c.mappedTo ?? '', sample: c.sampleData ?? '' }));
    this.missingMandatory = m.missingMandatory ?? [];
    this.mappingError = '';
    this.resetValidation();
  }

  autoMap(): void {
    this.mapMenuOpen = false;
    if (!this.job || this.busy) return;
    this.busy = true;
    this.api.autoMap(this.job.id).subscribe({
      next: (m) => {
        this.busy = false;
        this.applyMapping(m);
        this.showToast(`${m.mappedCount} of ${m.columns.length} columns mapped automatically.`);
      },
      error: (err) => {
        this.busy = false;
        this.mappingError = apiErrorMessage(err, 'Auto Map failed.');
      },
    });
  }

  clearMapping(): void {
    this.mapMenuOpen = false;
    this.columns.forEach((c) => (c.field = ''));
    this.saveMapping();
  }

  onMappingChange(): void {
    this.resetValidation();
    if (this.duplicateFields.size) {
      this.mappingError = `Each field can be mapped once: ${[...this.duplicateFields].map((k) => this.field(k)?.label ?? k).join(', ')}.`;
      return;
    }
    this.saveMapping();
  }

  private saveMapping(): void {
    if (!this.job) return;
    const mapping: Record<string, string | null> = {};
    for (const c of this.columns) mapping[c.header] = c.field || null;
    this.api.saveMapping(this.job.id, mapping, this.importType).subscribe({
      next: (m) => this.applyMapping(m),
      error: (err) => (this.mappingError = apiErrorMessage(err, 'The mapping could not be saved.')),
    });
  }

  /** Fields chosen for more than one column. */
  get duplicateFields(): Set<string> {
    const seen = new Set<string>();
    const dup = new Set<string>();
    for (const key of this.columns.map((c) => c.field)) {
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

  mappingBack(): void {
    this.step = 2;
    document.getElementById('bulk-import')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  // ===== Step 4: validate =====

  private resetValidation(): void {
    this.validated = false;
    this.summary = null;
    this.preview = [];
    this.problems = [];
    if (this.step > 3) this.step = 3;
  }

  validate(): void {
    if (!this.job || this.busy) return;
    if (this.duplicateFields.size) {
      this.mappingError = `Each field can be mapped once: ${[...this.duplicateFields].map((k) => this.field(k)?.label ?? k).join(', ')}.`;
      return;
    }
    if (this.missingMandatory.length) {
      this.mappingError = `Map the mandatory field${this.missingMandatory.length > 1 ? 's' : ''}: ${this.missingMandatory.join(', ')}.`;
      return;
    }
    this.mappingError = '';
    this.busy = true;
    this.api.validate(this.job.id).subscribe({
      next: (res) => {
        this.busy = false;
        this.job = res.import;
        this.summary = res.summary;
        this.preview = res.preview ?? [];
        this.problems = res.problems ?? [];
        this.validated = true;
        this.step = 4;
        document.getElementById('bulk-results')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
      },
      error: (err) => {
        this.busy = false;
        this.mappingError = apiErrorMessage(err, 'Validation failed.');
      },
    });
  }

  get problemCount(): number {
    return this.summary ? this.summary.invalid + this.summary.duplicate : 0;
  }

  issueText(r: BulkRecord): string {
    return (r.issues ?? []).map((i) => i.message).join(' ') || '-';
  }

  /** The invalid and duplicate rows as an Excel file, to fix and upload again. */
  downloadProblems(): void {
    if (!this.job) return;
    this.api.problemRows(this.job.id).subscribe({
      next: (file) => AssetBulkApi.save(file),
      error: (err) => this.showToast(apiErrorMessage(err, 'The problem rows could not be downloaded.'), true),
    });
  }

  // ===== Step 5: import =====

  runImport(): void {
    if (!this.job || !this.validated || !this.summary?.valid || this.busy) return;
    this.busy = true;
    this.api.confirm(this.job.id).subscribe({
      next: (res) => {
        this.busy = false;
        const s = res.summary;
        const skipped = s.invalid + s.duplicate;
        this.showToast(
          `Import complete: ${s.created} created, ${s.updated} updated${s.failed ? `, ${s.failed} failed` : ''}${skipped ? `, ${skipped} rows skipped` : ''}. See the Asset List.`,
          s.failed > 0 && !s.created && !s.updated,
        );
        this.step = 5;
        this.lastImport = res.import.fileName;
        // The import is finished (kept in the history); start again for the next file
        this.job = null;
        this.columns = [];
        this.missingMandatory = [];
        this.resetValidation();
        this.step = 5;
        this.loadFields();
        this.loadHistory();
        this.refreshCount();
      },
      error: (err) => {
        this.busy = false;
        this.showToast(apiErrorMessage(err, 'The import failed.'), true);
      },
    });
  }

  // ===== Export =====

  setFieldSet(set: BulkFieldSet): void {
    this.fieldSet = set;
    if (set === 'Standard') this.exportFields = new Set(this.exportableFields.filter((f) => f.standard).map((f) => f.key));
    if (set === 'All') this.exportFields = new Set(this.exportableFields.map((f) => f.key));
  }

  setExportType(type: BulkExportType): void {
    this.exportType = type;
    if (type === 'CustomExport') this.fieldSet = 'Custom';
    else if (this.fieldSet === 'Custom') this.setFieldSet('Standard');
    // Related data adds extra sheets, so it is Excel only
    if (type === 'AssetWithRelatedData') this.exportFormat = 'xlsx';
  }

  toggleExportField(key: string, on: boolean): void {
    const next = new Set(this.exportFields);
    if (on) next.add(key);
    else next.delete(key);
    this.exportFields = next;
  }

  private get exportFilters(): BulkExportFilters {
    return {
      category: this.filterCategory || undefined,
      locationId: this.filterSite || undefined,
      status: this.filterStatus || undefined,
      criticality: this.filterCriticality || undefined,
      department: this.filterDepartment || undefined,
      nextMaintenanceFrom: this.dateFrom || undefined,
      nextMaintenanceTo: this.dateTo || undefined,
    };
  }

  /** The number on the Export button: assets matching the filters. */
  refreshCount(): void {
    this.countSub?.unsubscribe();
    this.countSub = this.api.exportCount(this.exportFilters).subscribe({
      next: (n) => (this.exportCount = n),
      error: () => (this.exportCount = null),
    });
  }

  get hasExportFilters(): boolean {
    return !!(this.filterCategory || this.filterSite || this.filterStatus || this.filterCriticality || this.filterDepartment || this.dateFrom || this.dateTo);
  }

  clearExportFilters(): void {
    this.filterCategory = this.filterSite = this.filterStatus = this.filterCriticality = this.filterDepartment = this.dateFrom = this.dateTo = '';
    this.refreshCount();
  }

  runExport(): void {
    if (this.exporting) return;
    if (!this.fieldSet) return this.showToast('Choose the fields to export.', true);
    const keys = this.exportableFields.map((f) => f.key).filter((k) => this.exportFields.has(k));
    if (this.fieldSet === 'Custom' && !keys.length) return this.showToast('Choose at least one field to export.', true);
    if (this.dateFrom && this.dateTo && this.dateTo < this.dateFrom) return this.showToast('The end date is before the start date.', true);
    if (this.exportCount === 0) return this.showToast('No assets match the filters.', true);
    this.exporting = true;
    this.api
      .export({
        exportType: this.exportType,
        fieldSet: this.fieldSet === 'Custom' ? undefined : this.fieldSet,
        fields: this.fieldSet === 'Custom' ? keys : undefined,
        filters: this.exportFilters,
        fileFormat: this.exportFormat,
      })
      .subscribe({
        next: (file: BulkFile) => {
          this.exporting = false;
          AssetBulkApi.save(file);
          this.showToast(`${this.exportCount ?? ''} assets exported to ${file.fileName}.`.trim());
          this.loadHistory();
        },
        error: (err) => {
          this.exporting = false;
          this.showToast(apiErrorMessage(err, 'The export failed.'), true);
        },
      });
  }

  // ===== History =====

  loadHistory(): void {
    this.historyLoading = true;
    this.api.history(1, 50).subscribe({
      next: (page) => {
        this.historyLoading = false;
        this.history = page.items ?? [];
      },
      error: () => {
        this.historyLoading = false;
        this.history = [];
      },
    });
  }

  historyFailed(h: BulkHistoryItem): boolean {
    return /fail|error|cancel/i.test(h.status ?? '');
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
    this.toastTimer = setTimeout(() => (this.toast = ''), 5000);
  }
}

function unique(values: string[]): string[] {
  return [...new Set(values.filter((v) => v && v.trim()))].sort((a, b) => a.localeCompare(b));
}
