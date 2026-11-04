import { Component, input, output, signal } from '@angular/core';
import { NgTemplateOutlet } from '@angular/common';
import { LucideAngularModule, CalendarDays, CircleCheck, CircleX, Download, ExternalLink, FileText, Gauge, Pencil, Plus } from 'lucide-angular';
import { CalibrationInstrument } from '../shared/inspection.models';
import { fmtDate, pillClass } from '../shared/insp-utils';
import { certHref, certLabel } from './calibration-utils';

type Tab = 'Overview' | 'Results' | 'History';

/** Right-hand panel for the selected instrument: overview, last results, traceability and history. */
@Component({
  selector: 'app-insp-instrument-detail',
  imports: [NgTemplateOutlet, LucideAngularModule],
  templateUrl: './instrument-detail.html',
  styleUrls: ['../shared/insp-page.css', './instrument-detail.css']
})
export class InstrumentDetail {
  readonly instrument = input.required<CalibrationInstrument>();
  /** Status derived by the page from the data. */
  readonly status = input('');
  readonly schedule = output<void>();
  readonly record = output<void>();
  readonly edit = output<void>();

  readonly icons = { CalendarDays, CircleCheck, CircleX, Download, ExternalLink, FileText, Gauge, Pencil, Plus };
  readonly fmtDate = fmtDate;
  readonly pillClass = pillClass;
  readonly certHref = certHref;
  readonly certLabel = certLabel;
  readonly tab = signal<Tab>('Overview');
  readonly tabs: Tab[] = ['Overview', 'Results', 'History'];

  history(i: CalibrationInstrument) {
    return [...(i.history ?? [])].sort((a, b) => b.date.localeCompare(a.date));
  }

  fixed(n: number | null | undefined): string {
    return n === null || n === undefined || isNaN(Number(n)) ? '—' : Number(n).toFixed(2);
  }
}
