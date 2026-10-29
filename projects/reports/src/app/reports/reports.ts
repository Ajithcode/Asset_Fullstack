import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { environment } from '../../../../../environments/environment';
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
  private apiUrl = environment.reportsApiUrl || environment.apiUrl;

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

  loadReports() {
    this.loading = true;
    this.http.get(`${this.apiUrl}api/reports`).subscribe({
      next: (res: any) => {
        this.reports = [...this.generatedReportService.getAll(), ...(res.data || res)];
        this.loading = false;
      },
      error: () => {
        this.loading = false;
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
    this.router.navigate(['/report/create']);
  }

  viewReport(report: Report): void {
    this.router.navigate(['/report/view', report.id]);
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
    this.http.delete(`${this.apiUrl}api/reports/${report.id}`).subscribe({
      next: () => this.loadReports(),
      error: () => {
        this.reports = this.reports.filter(r => r.id !== report.id);
      }
    });
  }
}
