import { Component, EventEmitter, Input, OnChanges, Output } from '@angular/core';
import {
  Check, CircleX, ClipboardList, Download, FileSpreadsheet, LucideAngularModule, Settings, ShieldCheck, TriangleAlert
} from 'lucide-angular';
import { DataJob, ValidationIssue } from '../data-import.model';
import { formatStamp } from '../data-import.util';

type StepState = 'done' | 'active' | 'pending' | 'failed';

interface Step {
  title: string;
  text: string;
  state: StepState;
}

const PREVIEW_ROWS = 4;

/** Overview tab: progress summary, file / settings / validation cards, stepper and issues table. */
@Component({
  selector: 'app-adm-job-overview',
  imports: [LucideAngularModule],
  templateUrl: './job-overview.html',
  styleUrls: ['../../shared/admin-workspace.css', '../data-import-export.css']
})
export class JobOverview implements OnChanges {
  @Input({ required: true }) job!: DataJob;
  @Output() downloadErrors = new EventEmitter<void>();

  readonly icons = { ClipboardList, Download, FileSpreadsheet, Settings, ShieldCheck, TriangleAlert, CircleX, Check };
  readonly formatStamp = formatStamp;
  showAllIssues = false;

  ngOnChanges(): void {
    this.showAllIssues = false;
  }

  get isImport(): boolean {
    return this.job.type === 'Import';
  }

  get successRate(): number {
    return this.job.records ? Math.round((this.job.successful / this.job.records) * 1000) / 10 : 0;
  }

  get errorCount(): number {
    return this.job.issues.filter((i) => i.severity === 'Error').length;
  }

  get visibleIssues(): ValidationIssue[] {
    return this.showAllIssues ? this.job.issues : this.job.issues.slice(0, PREVIEW_ROWS);
  }

  get hasMoreIssues(): boolean {
    return this.job.issues.length > PREVIEW_ROWS;
  }

  get steps(): Step[] {
    const j = this.job;
    const hasSource = !this.isImport || !!j.fileName;
    const ran = j.status === 'Completed' || j.status === 'Running' || j.status === 'Failed';
    const mapState: StepState = !hasSource ? 'pending' : ran ? 'done' : 'active';
    const finalStep = (text: string, state: StepState): Step => ({ title: 'Import / Export', text, state });
    const final =
      j.status === 'Completed' ? finalStep('Process completed', 'done')
      : j.status === 'Running' ? finalStep('Processing records…', 'active')
      : j.status === 'Failed' ? finalStep('Job failed', 'failed')
      : j.status === 'Scheduled' ? finalStep('Waiting for next scheduled run', 'pending')
      : finalStep('Not started', 'pending');
    return [
      {
        title: this.isImport ? 'Upload File' : 'Select Data',
        text: hasSource ? (this.isImport ? 'File uploaded successfully' : 'Records selected') : 'No file uploaded yet',
        state: hasSource ? 'done' : 'active'
      },
      {
        title: 'Map & Validate',
        text: mapState === 'pending' ? 'Waiting for file' : j.issues.length ? 'Validation completed with issues' : 'Validation passed',
        state: mapState
      },
      final
    ];
  }

  severityTone(s: string): string {
    return s === 'Error' ? 'red' : 'orange';
  }
}
