import { Component, ElementRef, input, viewChild } from '@angular/core';
import { Inspection, InspectionFinding, InspectionResponse, ReportTemplate } from '../shared/inspection.models';
import { fmtDate } from '../shared/insp-utils';
import { printElement } from '../checklists/checklist-utils';

type Kind = 'header' | 'summary' | 'asset' | 'checklist' | 'findings' | 'evidence' | 'signatures' | 'footer' | 'custom';

/** Maps a report section name onto the block that renders it. */
function kindOf(name: string): Kind {
  const n = name.toLowerCase();
  if (n.includes('header') || n.includes('branding')) return 'header';
  if (n.includes('summary')) return 'summary';
  if (n.includes('asset')) return 'asset';
  if (n.includes('checklist') || n.includes('result')) return 'checklist';
  if (n.includes('finding') || n.includes('corrective')) return 'findings';
  if (n.includes('evidence') || n.includes('photo') || n.includes('gallery')) return 'evidence';
  if (n.includes('signature') || n.includes('stamp')) return 'signatures';
  if (n.includes('footer') || n.includes('page number')) return 'footer';
  return 'custom';
}

/** Rough page estimate for the pager / footer: three enabled sections per page. */
export function reportPageCount(t: ReportTemplate): number {
  return Math.max(1, Math.ceil((t.sections ?? []).filter((s) => s.enabled).length / 3));
}

const PAGE_WIDTH: Record<string, number> = { A4: 794, Letter: 816, A3: 1123, Legal: 816 };

/** Live HTML rendering of a report template, filled with one sample inspection. */
@Component({
  selector: 'app-report-preview',
  standalone: true,
  templateUrl: './report-preview.html',
  styleUrls: ['./report-preview.css']
})
export class ReportPreview {
  readonly template = input.required<ReportTemplate>();
  readonly inspection = input<Inspection | null>(null);
  readonly findings = input<InspectionFinding[]>([]);
  readonly zoom = input(75);
  readonly fmtDate = fmtDate;
  private readonly page = viewChild.required<ElementRef<HTMLElement>>('page');

  // Not memoised on purpose: the template object is edited in place by the settings form.
  blocks(): { name: string; kind: Kind }[] {
    const t = this.template();
    return [...(t.sections ?? [])]
      .sort((a, b) => a.order - b.order)
      .filter((s) => s.enabled)
      .map((s) => ({ name: s.name, kind: kindOf(s.name) }))
      .filter((b) => (b.kind !== 'evidence' || t.includePhotos) && (b.kind !== 'signatures' || t.includeSignatures));
  }

  pageCount(): number {
    return reportPageCount(this.template());
  }

  pageWidth(): number {
    const t = this.template();
    const w = PAGE_WIDTH[t.pageSize] ?? 794;
    return t.orientation === 'Landscape' ? Math.round(w * 1.414) : w;
  }

  responses(): InspectionResponse[] {
    const rows = this.inspection()?.responses ?? [];
    return this.template().includePassedItems ? rows : rows.filter((r) => r.result !== 'Pass');
  }

  photos(): string[] {
    const fromResponses = (this.inspection()?.responses ?? []).flatMap((r) => r.evidence ?? []);
    const fromFindings = this.findings().flatMap((f) => f.evidence ?? []);
    return [...new Set([...fromResponses, ...fromFindings])].slice(0, 6);
  }

  isImage(src: string): boolean {
    return /^(https?:|data:image|\/|blob:)/i.test(src);
  }

  reading(r: InspectionResponse): string {
    if (!r.reading) return 'N/A';
    return r.unit && !r.reading.includes(r.unit) ? `${r.reading} ${r.unit}` : r.reading;
  }

  criteria(r: InspectionResponse): string {
    if (r.minValue != null && r.maxValue != null) return `${r.minValue} – ${r.maxValue} ${r.unit}`;
    if (r.maxValue != null) return `≤ ${r.maxValue} ${r.unit}`;
    if (r.minValue != null) return `≥ ${r.minValue} ${r.unit}`;
    return r.responseType || '—';
  }

  /** 'INSR-{YYYY}-{SEQ}' -> 'INSR-2026-0148' using the sample inspection's date and number. */
  reportNumber(): string {
    const ins = this.inspection();
    const d = new Date(ins?.completedAt || ins?.startedAt || Date.now());
    const seq = /(\d+)\D*$/.exec(ins?.inspectionCode ?? '')?.[1] ?? '0001';
    return (this.template().reportNumberFormat || 'INSR-{YYYY}-{SEQ}')
      .replace(/\{YYYY\}/g, String(d.getFullYear()))
      .replace(/\{YY\}/g, String(d.getFullYear()).slice(2))
      .replace(/\{MM\}/g, String(d.getMonth() + 1).padStart(2, '0'))
      .replace(/\{DD\}/g, String(d.getDate()).padStart(2, '0'))
      .replace(/\{SEQ\}/g, seq.padStart(4, '0'));
  }

  banner(): { tone: string; title: string; text: string } {
    const ins = this.inspection();
    if (!ins) return { tone: 'info', title: 'Sample data', text: 'No completed or in-progress inspection is available; placeholders are shown.' };
    const open = this.findings().length || ins.findingsCount;
    if (ins.status === 'Completed')
      return open
        ? { tone: 'warn', title: 'Completed with Findings', text: 'The inspection has been completed with one or more findings requiring action.' }
        : { tone: 'good', title: 'Completed', text: 'The inspection has been completed with no findings.' };
    return { tone: 'info', title: ins.status ?? 'In Progress', text: 'The inspection is still in progress; results may change.' };
  }

  print(title: string): boolean {
    const t = this.template();
    return printElement(this.page().nativeElement, title, `${t.pageSize || 'A4'} ${(t.orientation || 'Portrait').toLowerCase()}`);
  }
}
