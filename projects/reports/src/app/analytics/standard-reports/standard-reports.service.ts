import { Injectable } from '@angular/core';
import { money, percent } from '../shared/analytics-common';
import { ReportPeriod, ReportSchedule, SR_TODAY, StandardReport, defaultReportPeriod } from './standard-reports.data';
import { nextRun, pad } from './standard-reports.util';

export interface PreviewRow {
  label: string;
  value: number;
  display: string;
  /** Share of the total (count / money / hours), or the gap to the average (percent) */
  share: string;
}

/** Standard reports kept in memory; populated from the reporting API (no seeded records). */
@Injectable({ providedIn: 'root' })
export class StandardReportsService {
  reports: StandardReport[] = [];
  /** The applied reporting period (shared by every report). */
  period: ReportPeriod = defaultReportPeriod();

  get today(): string {
    return SR_TODAY;
  }

  find(id: string): StandardReport | undefined {
    return this.reports.find((r) => r.id === id);
  }

  /** Apply the reporting period to every report. */
  applyPeriod(period: ReportPeriod): void {
    this.period = { ...period };
    this.reports = this.reports.map((r) => ({ ...r, period: { ...period } }));
  }

  saveSchedule(id: string, schedule: ReportSchedule): StandardReport | undefined {
    return this.update(id, (r) => ({
      ...r,
      schedule: { ...schedule, recipients: [...schedule.recipients] },
      frequency: schedule.frequency,
      nextScheduled: nextRun(schedule, SR_TODAY),
      status: r.status === 'Needs review' ? r.status : 'Scheduled',
      updatedOn: SR_TODAY
    }));
  }

  runNow(id: string): StandardReport | undefined {
    const now = new Date();
    return this.update(id, (r) => ({
      ...r,
      generatedOn: `${SR_TODAY}T${pad(now.getHours())}:${pad(now.getMinutes())}`,
      status: r.status === 'Needs review' ? r.status : 'Ready'
    }));
  }

  markReviewed(id: string): StandardReport | undefined {
    return this.update(id, (r) => ({ ...r, status: r.schedule ? 'Scheduled' : 'Ready', reviewDue: null, updatedOn: SR_TODAY }));
  }

  /** Breakdown rows for the viewer and CSV export. Empty until the reporting API supplies report data. */
  previewRows(_report: StandardReport): PreviewRow[] {
    return [];
  }

  formatValue(report: StandardReport, value: number): string {
    switch (report.preview.unit) {
      case 'money':
        return money(value);
      case 'percent':
        return percent(value);
      case 'hours':
        return `${value.toLocaleString('en-US')} h`;
      default:
        return value.toLocaleString('en-US');
    }
  }

  private update(id: string, change: (r: StandardReport) => StandardReport): StandardReport | undefined {
    let updated: StandardReport | undefined;
    this.reports = this.reports.map((r) => {
      if (r.id !== id) return r;
      updated = change(r);
      return updated;
    });
    return updated;
  }
}
