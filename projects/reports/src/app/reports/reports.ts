import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { forkJoin } from 'rxjs';
import { RowActions } from 'shared-ui';
import { downloadReportAsExcel, downloadReportsAsWorkbook, GeneratedReportService, Report } from '../generated-report.service';

export type { Report };
export type FrequencyFilter = 'Monthly' | 'Weekly' | 'Daily' | 'Once';

@Component({
  standalone: true,
  selector: 'app-reports',
  imports: [CommonModule, RowActions],
  templateUrl: './reports.html',
  styleUrls: ['./reports.css']
})
export class Reports {
  private router = inject(Router);
  private service = inject(GeneratedReportService);

  reports: Report[] = [];
  loading = false;
  error: string | null = null;
  busy = false;
  activeFilter: FrequencyFilter = 'Monthly';
  filters: FrequencyFilter[] = ['Monthly', 'Weekly', 'Daily', 'Once'];

  ngOnInit() {
    // If a report was just created, open the frequency tab it belongs under so it is visible straight away.
    const recent = this.service.lastCreatedRecurrence;
    if (recent && (this.filters as string[]).includes(recent)) {
      this.activeFilter = recent as FrequencyFilter;
    }
    this.service.lastCreatedRecurrence = null;
    this.loadReports();
  }

  loadReports() {
    this.loading = true;
    this.error = null;
    this.service.list(this.activeFilter).subscribe({
      next: (reports) => {
        this.reports = reports;
        this.loading = false;
      },
      error: (err: Error) => {
        this.reports = [];
        this.error = err.message;
        this.loading = false;
      }
    });
  }

  setFilter(filter: FrequencyFilter) {
    this.activeFilter = filter;
    this.loadReports();
  }

  /** The list is already filtered by recurrence on the server. */
  get filteredReports(): Report[] {
    return this.reports;
  }

  addReport() {
    this.router.navigate(['/reports-analytics/create']);
  }

  viewReport(report: Report): void {
    this.router.navigate(['/reports-analytics/view', report.id]);
  }

  /** The list holds no row data, so the report is fetched with its rows before it is written to Excel. */
  downloadReport(report: Report): void {
    this.busy = true;
    this.service.get(report.id).subscribe({
      next: (full) => {
        downloadReportAsExcel({ ...full, slNo: report.slNo });
        this.busy = false;
      },
      error: (err: Error) => {
        this.error = err.message;
        this.busy = false;
      }
    });
  }

  downloadAllReports(): void {
    const shown = this.filteredReports;
    if (shown.length === 0) return;
    this.busy = true;
    forkJoin(shown.map((report) => this.service.get(report.id))).subscribe({
      next: (full) => {
        downloadReportsAsWorkbook(
          full.map((report, i) => ({ ...report, slNo: shown[i].slNo })),
          `reports-export-${new Date().toISOString().slice(0, 10)}.xlsx`
        );
        this.busy = false;
      },
      error: (err: Error) => {
        this.error = err.message;
        this.busy = false;
      }
    });
  }

  refreshReports() {
    this.loadReports();
  }

  /** Reports cannot be edited once generated; Edit opens the report, where it can be regenerated. */
  editReport(report: Report) {
    this.viewReport(report);
  }

  deleteRow(report: Report): void {
    if (!confirm(`Delete the report "${report.title}"?`)) return;
    this.service.delete(report.id).subscribe({
      next: () => this.loadReports(),
      error: (err: Error) => {
        this.error = err.message;
      }
    });
  }
}
