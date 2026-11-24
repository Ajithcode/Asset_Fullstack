import { Component, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { ExportMenu, ExportRequest } from 'shared-ui';
import { GeneratedReport, GeneratedReportService, reportExportRequest } from '../generated-report.service';

interface ViewColumn {
  key: string;
  label: string;
  visible: boolean;
}

@Component({
  standalone: true,
  selector: 'app-report-view',
  imports: [CommonModule, ExportMenu],
  templateUrl: './report-view.html',
  styleUrls: ['./report-view.css']
})
export class ReportView {
  report: GeneratedReport | null = null;
  columns: ViewColumn[] = [];
  showColumnPicker = false;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private service: GeneratedReportService
  ) {
    const id = this.route.snapshot.paramMap.get('id');
    this.report = id ? this.service.getById(id) ?? null : null;
    this.columns = (this.report?.headers ?? []).map((label) => ({ key: label, label, visible: true }));
  }

  get visibleColumnCount(): number {
    return this.columns.filter((c) => c.visible).length;
  }

  /** Rows from imported files can lack a header's key, so fall back to a dash. */
  cell(row: Record<string, string | number>, key: string): string | number {
    return row[key] ?? '-';
  }

  toggleColumnPicker(): void {
    this.showColumnPicker = !this.showColumnPicker;
  }

  closeColumnPicker(): void {
    this.showColumnPicker = false;
  }

  toggleColumn(col: ViewColumn): void {
    col.visible = !col.visible;
  }

  /** Download (CSV / PDF): the report's rows in the columns currently shown. */
  exportSource = (): ExportRequest =>
    reportExportRequest(this.report as GeneratedReport, this.report?.headers.length ? this.columns.filter((c) => c.visible).map((c) => c.label) : []);

  onRefresh(): void {
    // No backend to re-fetch from — mirrors the stub Refresh actions used
    // elsewhere in this app where there's nothing live to reload.
  }

  backToReports(): void {
    this.router.navigate(['/reports-analytics/generated-reports']);
  }

  @HostListener('document:click')
  onDocumentClick(): void {
    this.closeColumnPicker();
  }
}
