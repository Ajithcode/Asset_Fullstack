import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { environment } from 'shared-ui';
import { ImportColumn, ImportFileModal } from 'shared-ui';
import { RowActions } from 'shared-ui';
import { downloadReportAsExcel, downloadReportsAsWorkbook, GeneratedReportService, Report } from '../generated-report.service';

export type { Report };
export type FrequencyFilter = 'Monthly' | 'Weekly' | 'Daily' | 'Once';

@Component({
  standalone: true,
  selector: 'app-reports',
  imports: [CommonModule, ImportFileModal, RowActions],
  templateUrl: './reports.html',
  styleUrls: ['./reports.css']
})
export class Reports {
  private http = inject(HttpClient);
  private router = inject(Router);
  private generatedReportService = inject(GeneratedReportService);
  /** Generated reports are the Report Analytics standard reports (the old api/reports endpoint does not exist). */
  private apiUrl = `${environment.reportAnalyticsApiUrl}api/standard-reports`;

  readonly importColumns: ImportColumn[] = [
    { key: 'slNo', label: 'Sl NO' },
    { key: 'title', label: 'Reports' },
    { key: 'timeRange', label: 'Time Range' },
    { key: 'expireOn', label: 'Expire On' },
    { key: 'recurrence', label: 'Recurrence' },
    { key: 'createdOn', label: 'Created On' },
    { key: 'type', label: 'Type' },
    { key: 'shareWith', label: 'Share With' },
    { key: 'generatedOn', label: 'Generated On' },
    { key: 'status', label: 'Status' }
  ];
  showImportModal = false;

  reports: Report[] = [];
  loading = false;
  error: string | null = null;
  activeFilter: FrequencyFilter = 'Monthly';
  filters: FrequencyFilter[] = ['Monthly', 'Weekly', 'Daily', 'Once'];

  ngOnInit() {
    // If a report was just created, jump to whichever frequency tab it
    // belongs under so it's actually visible without the user hunting for it.
    const recent = this.generatedReportService.mostRecent;
    if (recent && (this.filters as string[]).includes(recent.recurrence)) {
      this.activeFilter = recent.recurrence as FrequencyFilter;
    }
    this.loadReports();
  }

  /** GET /api/standard-reports (every report; the tabs filter by frequency). */
  loadReports() {
    this.loading = true;
    this.error = null;
    const params = { tenantId: environment.tenantId, clientId: environment.clientId, sortBy: 'generatedOn', sortDirection: 'desc', page: 1, pageSize: 200 };
    this.http.get<{ items?: StandardReportDto[] }>(this.apiUrl, { params }).subscribe({
      next: (res) => {
        const fromApi = (res?.items ?? []).map((dto, i) => toReport(dto, i + 1));
        this.reports = [...this.generatedReportService.getAll(), ...fromApi];
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.error = 'Could not load reports.';
        this.reports = this.generatedReportService.getAll();
      }
    });
  }

  setFilter(filter: FrequencyFilter) {
    this.activeFilter = filter;
    this.loadReports();
  }

  get filteredReports(): Report[] {
    return this.reports.filter(r => r.recurrence === this.activeFilter);
  }

  addReport() {
    this.router.navigate(['/reports-analytics/create']);
  }

  viewReport(report: Report): void {
    this.router.navigate(['/reports-analytics/view', report.id]);
  }

  uploadReport() {
    this.showImportModal = true;
  }

  onImportRows(rows: Record<string, string>[]): void {
    const startIndex = this.reports.length;
    const normalizeStatus = (value: string | undefined): Report['status'] => {
      const v = (value ?? '').trim();
      return v === 'Completed' || v === 'Pending' || v === 'Failed' ? v : 'Pending';
    };

    const imported: Report[] = rows.map((row, i) => ({
      id: `RPT-${startIndex + i + 1}`,
      slNo: row['slNo'] ? Number(row['slNo']) : startIndex + i + 1,
      title: row['title'] ?? '',
      timeRange: row['timeRange'] ?? '',
      expireOn: row['expireOn'] ?? '',
      recurrence: row['recurrence'] ?? '',
      createdOn: row['createdOn'] ?? '',
      type: row['type'] ?? '',
      shareWith: row['shareWith'] ?? '',
      generatedOn: row['generatedOn'] ?? '',
      status: normalizeStatus(row['status'])
    }));

    this.reports = [...this.reports, ...imported];
    this.showImportModal = false;
  }

  downloadReport(report: Report): void {
    downloadReportAsExcel({ headers: [], rows: [], ...report });
  }

  downloadAllReports(): void {
    downloadReportsAsWorkbook(
      this.filteredReports.map((report) => ({ headers: [], rows: [], ...report })),
      `reports-export-${new Date().toISOString().slice(0, 10)}.xlsx`
    );
  }

  refreshReports() {
    this.loadReports();
  }

  editReport(report: Report) {
    // Edit action
  }

  deleteRow(report: Report): void {
    this.reports = this.reports.filter(r => r.id !== report.id);
  }

  deleteReport(report: Report) {
    this.http.delete(`${this.apiUrl}/${encodeURIComponent(report.id)}`).subscribe({
      next: () => this.loadReports(),
      // Rows created this session (not in the API) are only removed locally; otherwise keep the row and say why
      error: (err: { status?: number }) => {
        if (err?.status === 404) this.reports = this.reports.filter(r => r.id !== report.id);
        else this.error = 'Could not delete the report.';
      }
    });
  }
}

/** GET /api/standard-reports item (the fields this list shows). */
interface StandardReportDto {
  reportId: string;
  reportName: string;
  reportType?: string | null;
  category?: string;
  reportPeriodFrom?: string | null;
  reportPeriodTo?: string | null;
  frequency?: string;
  status?: string | null;
  createdAt?: string | null;
  generatedOn?: string | null;
}

/** Standard report → generated-report row: frequency drives the tabs (On demand shows under Once). */
function toReport(dto: StandardReportDto, slNo: number): Report {
  const day = (v?: string | null) => (v ? v.slice(0, 10) : '');
  const from = day(dto.reportPeriodFrom);
  const to = day(dto.reportPeriodTo);
  return {
    id: dto.reportId,
    slNo,
    title: dto.reportName,
    timeRange: from && to ? `${from} – ${to}` : '',
    expireOn: '',
    recurrence: dto.frequency === 'On demand' ? 'Once' : dto.frequency ?? '',
    createdOn: day(dto.createdAt),
    type: dto.reportType || dto.category || '',
    shareWith: '',
    generatedOn: day(dto.generatedOn),
    status: dto.status === 'Ready' ? 'Completed' : 'Pending'
  };
}
