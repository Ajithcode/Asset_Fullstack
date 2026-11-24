import { Component, ElementRef, OnInit, ViewChild, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { Observable, Subject, Subscription, debounceTime } from 'rxjs';
import { Calendar, Download, File, FileText, Funnel, LucideAngularModule, Plus, Search, Upload } from 'lucide-angular';
import { ExportRequest, PDF_LIMIT_MESSAGE } from 'shared-ui';
import { AnalyticsPage } from '../shared/analytics-common';
import { ConfirmDialog } from './dialogs/confirm-dialog';
import { ReportBuilder } from './report-builder/report-builder';
import { ReportConfig } from './report-config/report-config';
import { LibrarySort, ReportLibrary } from './report-library/report-library';
import { previewSignature } from './report-preview/report-preview';
import { RA_MODULES, SCHEDULES } from './custom-reports.data';
import { CustomReport, ReportStatus, ReportVisibility } from './custom-reports.model';
import { CustomReportOwner, CustomReportsService, apiError, cloneReport, dubaiNow } from './custom-reports.service';

type UpdatedRange = '' | '1' | '7' | '30' | 'older';

const MAX_IMPORT_BYTES = 1_000_000;
const NOT_SCHEDULED = SCHEDULES[0];

/** Reports & Analytics > Custom Reports (Report Analytics API: /api/custom-reports) */
@Component({
  selector: 'app-ra-custom-reports',
  imports: [FormsModule, RouterLink, LucideAngularModule, ReportLibrary, ReportBuilder, ReportConfig, ConfirmDialog],
  templateUrl: './custom-reports.html',
  styleUrls: ['../shared/analytics-page.css', './custom-reports.css']
})
export class CustomReportsPage extends AnalyticsPage implements OnInit {
  private readonly service = inject(CustomReportsService);
  @ViewChild(ReportBuilder) builder?: ReportBuilder;
  @ViewChild('builderAnchor') builderAnchor?: ElementRef<HTMLElement>;
  @ViewChild('importInput') importInput?: ElementRef<HTMLInputElement>;

  readonly icons = { FileText, Funnel, Calendar, File, Plus, Upload, Download, Search };
  /** From GET /api/custom-reports/options (filled when the catalogue loads) */
  readonly modules = RA_MODULES;
  readonly statuses: ReportStatus[] = ['Draft', 'Published', 'Scheduled'];
  readonly updatedRanges: { key: UpdatedRange; label: string }[] = [
    { key: '1', label: 'Last 24 hours' },
    { key: '7', label: 'Last 7 days' },
    { key: '30', label: 'Last 30 days' },
    { key: 'older', label: 'Older than 30 days' }
  ];

  // Filters (search / owner / module / status / sort go to the API; the updated range is applied here)
  search = '';
  owner = '';
  module = '';
  status: '' | ReportStatus = '';
  updated: UpdatedRange = '';
  librarySort: LibrarySort = 'updated';
  filtered: CustomReport[] = [];
  owners: CustomReportOwner[] = [];
  loading = false;
  loadError = '';
  busy = false;

  /** The report in the builder (a working copy until saved) */
  draft: CustomReport;
  /** previewSignature of the draft as last loaded / saved (unchanged → the saved report is previewed) */
  savedSignature = '';
  showErrors = false;
  confirmDelete: CustomReport | null = null;

  private librarySub: Subscription | null = null;
  private readonly searchInput = new Subject<void>();
  private readonly searchSub = this.searchInput.pipe(debounceTime(300)).subscribe(() => this.loadLibrary());

  constructor() {
    super();
    this.draft = this.service.blank();
  }

  ngOnInit(): void {
    this.service.loadCatalog().subscribe({
      next: () => {
        // Re-read the draft's definition now that fields are known
        this.draft = cloneReport(this.draft);
      },
      error: (err) => this.notify(apiError(err, 'Could not load the report fields.'))
    });
    this.loadLibrary();
    this.loadSummary();
    this.loadOwners();
  }

  override ngOnDestroy(): void {
    super.ngOnDestroy();
    this.searchSub.unsubscribe();
    this.librarySub?.unsubscribe();
  }

  get stats() {
    return this.service.stats;
  }

  get nameError(): string {
    const n = this.draft.name.trim();
    if (!n) return 'Report name is required.';
    if (n.length < 3) return 'Use at least 3 characters.';
    if (this.service.isNameTaken(n, this.draft.id)) return 'Another custom report already uses this name.';
    return '';
  }

  // ----- Library -----

  /** GET /api/custom-reports with the server-side filters and sort. */
  loadLibrary(): void {
    this.librarySub?.unsubscribe();
    this.loading = true;
    this.loadError = '';
    this.librarySub = this.service
      .loadLibrary({
        search: this.search.trim(),
        owner: this.owner,
        module: this.module,
        status: this.status,
        sortBy: this.librarySort === 'name' ? 'reportName' : 'updatedAt',
        sortDirection: this.librarySort === 'name' ? 'asc' : 'desc'
      })
      .subscribe({
        next: () => {
          this.loading = false;
          this.refilter();
        },
        error: (err) => {
          this.loading = false;
          this.loadError = apiError(err, 'Could not load custom reports.');
          this.filtered = [];
          this.notify(this.loadError);
        }
      });
  }

  private loadSummary(): void {
    this.service.loadSummary().subscribe({ error: (err) => console.warn('[custom-reports] summary failed', err) });
  }

  private loadOwners(): void {
    this.service.owners().subscribe({
      next: (owners) => (this.owners = owners),
      error: (err) => console.warn('[custom-reports] owners failed', err)
    });
  }

  /** After a change: library, tiles and owners. */
  private reloadAll(): void {
    this.loadLibrary();
    this.loadSummary();
    this.loadOwners();
  }

  /** Server filters changed: reload. */
  filterChanged(): void {
    this.loadLibrary();
  }

  searchChanged(): void {
    this.searchInput.next();
  }

  /** Local part of the filtering: the Last Updated range and the favourites-first order. */
  refilter(): void {
    const now = new Date(`${dubaiNow()}:00Z`).getTime();
    const list = this.service.reports.filter((r) => {
      if (this.updated) {
        const days = (now - new Date(`${r.updatedAt}:00Z`).getTime()) / 86_400_000;
        if (this.updated === 'older' ? days <= 30 : days > Number(this.updated)) return false;
      }
      return true;
    });
    if (this.librarySort === 'favourites') list.sort((a, b) => Number(b.favourite) - Number(a.favourite) || b.updatedAt.localeCompare(a.updatedAt));
    this.filtered = list;
  }

  get hasFilters(): boolean {
    return !!(this.search || this.owner || this.module || this.status || this.updated);
  }

  clearFilters(): void {
    this.search = '';
    this.owner = '';
    this.module = '';
    this.status = '';
    this.updated = '';
    this.loadLibrary();
  }

  setSort(sort: LibrarySort): void {
    this.librarySort = sort;
    this.loadLibrary();
  }

  /** GET /api/custom-reports/{id} for the full definition, then open it in the builder. */
  openReport(r: CustomReport): void {
    this.service.load(r.id).subscribe({
      next: (full) => {
        this.setDraft(full);
        this.builder?.goTo(1);
        this.notify(`“${full.name}” opened in the Report Builder.`);
      },
      error: (err) => this.notify(apiError(err, 'Could not open the report.'))
    });
  }

  private setDraft(r: CustomReport): void {
    this.draft = cloneReport(r);
    this.savedSignature = r.id ? previewSignature(this.draft) : '';
    this.showErrors = false;
  }

  /** PUT /api/custom-reports/{id}/favorite. */
  toggleFavourite(r: CustomReport): void {
    const next = !r.favourite;
    this.service.setFavourite(r.id, next).subscribe({
      next: () => {
        if (this.draft.id === r.id) this.draft.favourite = next;
        this.refilter();
        this.notify(next ? `“${r.name}” added to favourites.` : `“${r.name}” removed from favourites.`);
      },
      error: (err) => this.notify(apiError(err, 'Could not update favourites.'))
    });
  }

  /** POST /api/custom-reports/{id}/duplicate. */
  duplicate(r: CustomReport): void {
    this.service.duplicate(r.id).subscribe({
      next: (copy) => {
        this.reloadAll();
        this.notify(`Duplicated as “${copy.name}” (draft).`);
      },
      error: (err) => this.notify(apiError(err, 'Could not duplicate the report.'))
    });
  }

  askDelete(r: CustomReport): void {
    this.confirmDelete = r;
  }

  /** DELETE /api/custom-reports/{id}. */
  confirmRemove(): void {
    const r = this.confirmDelete;
    this.confirmDelete = null;
    if (!r) return;
    this.service.remove(r.id).subscribe({
      next: () => {
        if (this.draft.id === r.id) this.newReport(false);
        this.reloadAll();
        this.notify(`“${r.name}” deleted.`);
      },
      error: (err) => this.notify(apiError(err, 'Could not delete the report.'))
    });
  }

  /** Library menu: POST /{id}/publish or /{id}/unpublish (the API checks the report is complete). */
  togglePublish(r: CustomReport): void {
    const live = r.status === 'Published';
    (live ? this.service.unpublish(r.id) : this.service.publish(r.id)).subscribe({
      next: (updated) => {
        if (this.draft.id === r.id) {
          this.draft.status = updated.status;
          this.draft.updatedAt = updated.updatedAt;
        }
        this.reloadAll();
        this.notify(live ? `“${r.name}” unpublished (${updated.status}).` : `“${r.name}” published (${audience(updated.visibility)}).`);
      },
      error: (err) => this.notify(apiError(err, live ? 'Could not unpublish the report.' : 'Could not publish the report.'))
    });
  }

  /** Library menu: the report's rows (GET /api/custom-reports/{id}/preview, all rows) as CSV or PDF. */
  exportData(e: { report: CustomReport; format: 'csv' | 'pdf' }): void {
    const r = e.report;
    this.service.exportRows(r.id).subscribe({
      next: (res) => {
        const req: ExportRequest = {
          title: r.name,
          fileBase: r.name.toLowerCase().replace(/[^a-z0-9]+/g, '-') || 'custom-report',
          columns: res.columns.map((c) => ({ label: c.label, key: c.key })),
          filters: { Report: r.id, Module: r.module, Status: r.status },
          rows: () => res.rows
        };
        this.exporter.run(e.format, req).then(
          (out) => this.notify(out.truncated ? PDF_LIMIT_MESSAGE : `Exported “${r.name}” as ${e.format.toUpperCase()}.`),
          (err: Error) => this.notify(err?.message || 'Could not export the report data.')
        );
      },
      error: (err) => this.notify(apiError(err, 'Could not export the report data. Make sure the report is complete.'))
    });
  }

  // ----- Builder + configuration -----

  newReport(announce = true): void {
    this.setDraft(this.service.blank());
    this.builder?.goTo(0);
    if (announce) {
      this.builderAnchor?.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'start' });
      this.notify('New blank report — start by choosing a data source.');
    }
  }

  /**
   * Saves the draft (POST new / PUT existing), then optionally publishes or unpublishes it.
   * Emits the final report and refreshes the library.
   */
  private store(then: 'publish' | 'unpublish' | null, message: (r: CustomReport) => string): void {
    if (this.busy) return;
    this.busy = true;
    this.draft.name = this.draft.name.trim();
    const step = (this.builder?.step ?? 0) + 1;
    this.service.save(this.draft, step).subscribe({
      next: (saved) => {
        const after$: Observable<CustomReport> | null =
          then === 'publish' ? this.service.publish(saved.id) : then === 'unpublish' && saved.status !== 'Draft' ? this.service.unpublish(saved.id) : null;
        if (!after$) return this.stored(saved, message);
        after$.subscribe({
          next: (r) => this.stored(r, message),
          error: (err) => {
            this.stored(saved, () => apiError(err, `“${saved.name}” was saved but could not be ${then === 'publish' ? 'published' : 'unpublished'}.`));
          }
        });
      },
      error: (err) => {
        this.busy = false;
        this.notify(apiError(err, 'Could not save the report.'));
      }
    });
  }

  private stored(r: CustomReport, message: (r: CustomReport) => string): void {
    this.busy = false;
    this.setDraft(r);
    this.reloadAll();
    this.notify(message(r));
  }

  /** A draft only needs a valid name. */
  saveDraft(): void {
    this.showErrors = true;
    const problem = this.nameError || (this.draft.sourceKey ? '' : 'Select a data source.');
    if (problem) {
      this.notify(problem);
      return;
    }
    this.store('unpublish', (r) => `“${r.name}” saved as a draft.`);
  }

  /** Save / publish need a valid name, at least one field and complete filters. */
  private validateAll(): boolean {
    this.showErrors = true;
    const problem = this.nameError || (this.draft.sourceKey ? '' : 'Select a data source.') || (this.draft.fields.length ? '' : 'Select at least one field.')
      || this.builder?.firstProblem || (this.draft.visibility ? '' : 'Select a visibility.') || (this.draft.schedule ? '' : 'Select a schedule.');
    if (problem) {
      this.notify(problem);
      return false;
    }
    return true;
  }

  /** Save Report: a scheduled report goes live (publish → Scheduled); a published one stays published. */
  saveReport(): void {
    if (!this.validateAll()) return;
    const scheduled = this.draft.schedule !== NOT_SCHEDULED;
    this.store(scheduled && this.draft.status === 'Draft' ? 'publish' : null, (r) =>
      scheduled ? `“${r.name}” saved and scheduled (${r.schedule}).` : `“${r.name}” saved.`
    );
  }

  publish(): void {
    if (!this.validateAll()) return;
    if (this.draft.status === 'Published') {
      this.store('unpublish', (r) => `“${r.name}” unpublished.`);
    } else {
      this.store('publish', (r) => `“${r.name}” published (${audience(r.visibility)}).`);
    }
  }

  previewReport(): void {
    this.builder?.goTo(5);
    this.builderAnchor?.nativeElement.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  // ----- Import / export -----

  chooseImport(): void {
    this.importInput?.nativeElement.click();
  }

  /** POST /api/custom-reports/import: each template in the file becomes a Draft. */
  onImportFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    if (!/\.json$/i.test(file.name)) {
      this.notify('Choose a .json report template.');
      return;
    }
    if (file.size > MAX_IMPORT_BYTES) {
      this.notify('Template is larger than 1 MB.');
      return;
    }
    this.service.importFile(file).subscribe({
      next: (res) => {
        this.reloadAll();
        if (!res.created) {
          this.notify(`No templates imported${res.failed[0] ? `: ${res.failed[0].message}` : '.'}`);
          return;
        }
        const skipped = res.failed.length;
        this.notify(`Imported ${res.created} template${res.created === 1 ? '' : 's'} as draft${skipped ? `; ${skipped} skipped` : ''}.`);
      },
      error: (err) => this.notify(apiError(err, 'The template could not be imported.'))
    });
  }

  /** GET /api/custom-reports/export?format=json for the reports in the library (current filters). */
  exportReports(): void {
    const reports = this.filtered;
    if (!reports.length) {
      this.notify('No reports to export with the current filters.');
      return;
    }
    this.service.exportTemplates(reports.map((r) => r.id)).subscribe({
      next: (blob) => {
        download(blob, `custom-reports-${dubaiNow().slice(0, 10)}.json`);
        this.notify(`Exported ${reports.length} report${reports.length === 1 ? '' : 's'} as JSON (current filters).`);
      },
      error: (err) => this.notify(apiError(err, 'Could not export the reports.'))
    });
  }
}

function audience(v: ReportVisibility): string {
  return v === 'Only me' ? 'visible to you only' : v === 'My team' ? 'visible to your team' : 'visible to the organization';
}

function download(blob: Blob, fileName: string): void {
  const url = URL.createObjectURL(blob);
  const a = document.createElement('a');
  a.href = url;
  a.download = fileName;
  a.click();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
