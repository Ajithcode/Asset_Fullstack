import { Component, HostListener, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { downloadReportAsExcel, GeneratedReport, GeneratedReportService, ReportCell } from '../generated-report.service';

interface ViewColumn {
  key: string;
  label: string;
  visible: boolean;
}

@Component({
  standalone: true,
  selector: 'app-report-view',
  imports: [CommonModule],
  templateUrl: './report-view.html',
  styleUrls: ['./report-view.css']
})
export class ReportView implements OnInit {
  report: GeneratedReport | null = null;
  columns: ViewColumn[] = [];
  showColumnPicker = false;
  loading = false;
  regenerating = false;
  error: string | null = null;

  private id: string | null = null;

  constructor(
    private route: ActivatedRoute,
    private router: Router,
    private service: GeneratedReportService
  ) {}

  ngOnInit(): void {
    this.id = this.route.snapshot.paramMap.get('id');
    this.load();
  }

  private load(): void {
    if (!this.id) {
      this.error = 'Report not found.';
      return;
    }

    this.loading = true;
    this.error = null;
    this.service.get(this.id).subscribe({
      next: (report) => this.show(report),
      error: (err: Error) => {
        this.report = null;
        this.error = err.message;
        this.loading = false;
      }
    });
  }

  private show(report: GeneratedReport): void {
    const hidden = new Set(this.columns.filter((c) => !c.visible).map((c) => c.key));
    this.report = report;
    this.columns = report.headers.map((label) => ({ key: label, label, visible: !hidden.has(label) }));
    this.loading = false;
    this.regenerating = false;
  }

  get visibleColumnCount(): number {
    return this.columns.filter((c) => c.visible).length;
  }

  cell(row: Record<string, ReportCell>, key: string): string | number {
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

  onDownload(): void {
    if (!this.report) return;
    downloadReportAsExcel(this.report);
  }

  /** Reads the report's dataset again for the same time range ending now. */
  onRefresh(): void {
    if (!this.report || this.regenerating) return;
    this.regenerating = true;
    this.error = null;
    this.service.regenerate(this.report.id).subscribe({
      next: (report) => this.show(report),
      error: (err: Error) => {
        this.error = err.message;
        this.regenerating = false;
      }
    });
  }

  backToReports(): void {
    this.router.navigate(['/reports-analytics/generated-reports']);
  }

  @HostListener('document:click')
  onDocumentClick(): void {
    this.closeColumnPicker();
  }
}
