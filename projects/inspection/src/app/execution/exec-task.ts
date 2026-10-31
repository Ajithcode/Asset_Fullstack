import { Component, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  LucideAngularModule,
  ArrowLeft,
  ArrowRight,
  ChevronDown,
  ChevronUp,
  CircleAlert,
  CircleCheck,
  FileText,
  Image as ImageIcon,
  Plus,
  Settings,
  X
} from 'lucide-angular';
import { InspectionResponse } from '../shared/inspection.models';
import { pillClass } from '../shared/insp-utils';

export interface TaskRef {
  r: InspectionResponse;
  /** Index in inspection.responses */
  i: number;
}

/** Reading check against the task limits: null when there is nothing to check. */
export function evaluateReading(r: InspectionResponse): { pass: boolean; text: string } | null {
  if (r.reading === '' || r.reading == null || (r.minValue == null && r.maxValue == null)) return null;
  const v = Number(String(r.reading).replace(',', '.'));
  if (isNaN(v)) return null;
  const unit = r.unit ? ` ${r.unit}` : '';
  const round = (n: number) => Math.round(n * 1000) / 1000;
  if (r.maxValue != null && v > r.maxValue) return { pass: false, text: `Fail – Above maximum by ${round(v - r.maxValue)}${unit}` };
  if (r.minValue != null && v < r.minValue) return { pass: false, text: `Fail – Below minimum by ${round(r.minValue - v)}${unit}` };
  return { pass: true, text: 'Pass – Within acceptance range' };
}

export function isImage(e: string): boolean {
  return /^data:image\//.test(e) || /\.(png|jpe?g|gif|webp|bmp|svg)(\?|$)/i.test(e);
}

/** Current task card of the execution page: reading, response, notes, evidence and the section's other tasks. */
@Component({
  selector: 'app-insp-exec-task',
  standalone: true,
  imports: [FormsModule, LucideAngularModule],
  templateUrl: './exec-task.html',
  styleUrls: ['../shared/insp-page.css', './exec-task.css']
})
export class ExecTask {
  readonly task = input.required<TaskRef>();
  readonly sectionTasks = input.required<TaskRef[]>();
  readonly total = input.required<number>();
  readonly readOnly = input(false);
  readonly evidenceRequired = input(false);
  readonly findingTasks = input<string[]>([]);

  readonly changed = output<void>();
  readonly go = output<number>();
  readonly createFinding = output<TaskRef>();

  readonly icons = { ArrowLeft, ArrowRight, ChevronDown, ChevronUp, CircleAlert, CircleCheck, FileText, ImageIcon, Plus, Settings, X };
  readonly results = ['Pass', 'Fail', 'N/A'];
  readonly pillClass = pillClass;
  readonly evaluate = evaluateReading;
  readonly isImage = isImage;
  readonly open = signal<number | null>(null);

  hasLimits(r: InspectionResponse): boolean {
    return r.minValue != null || r.maxValue != null;
  }

  isNumeric(r: InspectionResponse): boolean {
    return /numeric/i.test(r.responseType) || this.hasLimits(r);
  }

  isYesNo(r: InspectionResponse): boolean {
    return /yes/i.test(r.responseType);
  }

  needsReading(r: InspectionResponse): boolean {
    return this.isNumeric(r) || /text/i.test(r.responseType) || this.isYesNo(r);
  }

  range(r: InspectionResponse): string {
    const u = r.unit ? ` ${r.unit}` : '';
    if (r.minValue != null && r.maxValue != null) return `${r.minValue} – ${r.maxValue}${u}`;
    if (r.maxValue != null) return `≤ ${r.maxValue}${u}`;
    if (r.minValue != null) return `≥ ${r.minValue}${u}`;
    return '';
  }

  hasFinding(r: InspectionResponse): boolean {
    return this.findingTasks().includes(r.taskTitle) || this.findingTasks().includes(r.taskCode);
  }

  onReading(r: InspectionResponse, value: string): void {
    r.reading = value;
    const ev = evaluateReading(r);
    if (ev) r.result = ev.pass ? 'Pass' : 'Fail';
    this.changed.emit();
  }

  setResult(r: InspectionResponse, value: string): void {
    r.result = value;
    this.changed.emit();
  }

  addEvidence(r: InspectionResponse, files: FileList | null): void {
    for (const file of Array.from(files ?? [])) {
      const reader = new FileReader();
      reader.onload = () => {
        if (typeof reader.result === 'string') {
          r.evidence = [...(r.evidence ?? []), reader.result];
          this.changed.emit();
        }
      };
      reader.readAsDataURL(file);
    }
  }

  removeEvidence(r: InspectionResponse, n: number): void {
    r.evidence = r.evidence.filter((_, k) => k !== n);
    this.changed.emit();
  }

  /** Short label for a placeholder evidence tile. */
  evidenceLabel(e: string): string {
    if (e.startsWith('data:')) return e.slice(5, e.indexOf(';')).split('/')[1]?.toUpperCase() ?? 'FILE';
    return e.split('/').pop() || e;
  }

  toggle(i: number): void {
    this.open.set(this.open() === i ? null : i);
  }
}
