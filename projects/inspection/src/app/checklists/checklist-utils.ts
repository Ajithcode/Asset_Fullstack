import { ClipboardCheck, FileText, Forklift, ListChecks, Shield, Zap } from 'lucide-angular';
import { Checklist, InspectionAudit, InspectionTask } from '../shared/inspection.models';

export const RESPONSE_TYPES = ['Pass / Fail', 'Yes / No', 'Numeric', 'Text'];
export const MODULES = ['Inspection', 'Maintenance', 'WIP', 'Audit'];
export const SCORING_METHODS = ['Weighted', 'Pass / Fail', 'Percentage', 'None'];

/** Copy of a record without identity / audit fields, ready to POST as a new record. */
export function withoutAudit<T extends InspectionAudit>(r: T): Partial<T> {
  const { id, createdBy, createdAt, updatedBy, updatedAt, ...rest } = r;
  void id; void createdBy; void createdAt; void updatedBy; void updatedAt;
  return rest as Partial<T>;
}

/** '3.0' -> '4.0'; falls back to `${version}.0`. */
export function nextVersionLabel(label: string, version: number): string {
  const n = parseFloat(label);
  return `${isNaN(n) ? version + 1 : Math.floor(n) + 1}.0`;
}

/** Icon for a checklist card, picked from its name / inspection type. */
export function checklistIcon(c: Pick<Checklist, 'templateName' | 'inspectionTypeName'>) {
  const s = `${c.templateName} ${c.inspectionTypeName}`.toLowerCase();
  if (s.includes('forklift') || s.includes('pre-use') || s.includes('vehicle')) return Forklift;
  if (s.includes('electr')) return Zap;
  if (s.includes('verification') || s.includes('audit') || s.includes('statutory')) return ClipboardCheck;
  if (s.includes('safety')) return Shield;
  if (s.includes('condition') || s.includes('assessment')) return FileText;
  return ListChecks;
}

/** Label for a task's response type: 'Numeric (°C)'. */
export function responseLabel(t: Pick<InspectionTask, 'responseType' | 'unit'>): string {
  return t.responseType === 'Numeric' && t.unit ? `Numeric (${t.unit})` : t.responseType;
}

/** Default acceptance criteria text for a task picked into a checklist. */
export function defaultCriteria(t: InspectionTask): string {
  if (t.responseType === 'Numeric') {
    if (t.minValue != null && t.maxValue != null) return `${t.minValue} – ${t.maxValue} ${t.unit}`.trim();
    if (t.maxValue != null) return `≤ ${t.maxValue} ${t.unit}`.trim();
    if (t.minValue != null) return `≥ ${t.minValue} ${t.unit}`.trim();
  }
  return '-';
}

function escapeHtml(s: string): string {
  return s.replace(/[&<>"]/g, (ch) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;' })[ch] ?? ch);
}

/**
 * Opens `el` in a new window with the app's stylesheets and calls print (the browser's "Save as PDF").
 * Elements keep their emulated-encapsulation attributes, so component styles still match; avoid relying on
 * `:host` rules or host CSS variables inside the printed element. Returns false when popups are blocked.
 */
export function printElement(el: HTMLElement, title: string, page = 'A4 portrait'): boolean {
  const w = window.open('', '_blank', 'width=960,height=1100');
  if (!w) return false;
  const styles = Array.from(document.querySelectorAll('style, link[rel="stylesheet"]'))
    .map((n) => n.outerHTML)
    .join('\n');
  w.document.open();
  w.document.write(
    `<!doctype html><html><head><meta charset="utf-8"><title>${escapeHtml(title)}</title>${styles}` +
      `<style>@page{size:${page};margin:10mm}html,body{margin:0;background:#fff}` +
      `body{-webkit-print-color-adjust:exact;print-color-adjust:exact}</style></head>` +
      `<body>${el.outerHTML}</body></html>`
  );
  w.document.close();
  w.focus();
  setTimeout(() => w.print(), 400);
  return true;
}
