import { Component, ElementRef, input, output, viewChild } from '@angular/core';
import { LucideAngularModule, Printer, X } from 'lucide-angular';
import { Checklist } from '../shared/inspection.models';
import { fmtDate } from '../shared/insp-utils';
import { printElement } from './checklist-utils';

/** Modal that renders a checklist as a blank, printable inspection form. */
@Component({
  selector: 'app-checklist-preview',
  standalone: true,
  imports: [LucideAngularModule],
  template: `
    <div class="pv-backdrop" (click)="closed.emit()"></div>
    <div class="pv-modal" role="dialog" aria-modal="true" aria-label="Checklist preview">
      <div class="pv-bar">
        <strong class="pv-grow">Preview — {{ checklist().templateName }} v{{ checklist().versionLabel }}</strong>
        <button type="button" class="pv-btn pv-btn--primary" (click)="print()"><lucide-icon [img]="icons.Printer" [size]="16"></lucide-icon>Print / PDF</button>
        <button type="button" class="pv-btn" aria-label="Close preview" (click)="closed.emit()"><lucide-icon [img]="icons.X" [size]="16"></lucide-icon></button>
      </div>
      <div class="pv-scroll">
        <article #sheet class="sheet">
          <header class="sh-head">
            <div><div class="sh-brand">asset360</div><div class="sh-sub">by Purple IQ</div></div>
            <div class="sh-right"><div class="sh-kind">INSPECTION CHECKLIST</div><div>{{ checklist().checklistCode }} · v{{ checklist().versionLabel }}</div></div>
          </header>
          <h1 class="sh-title">{{ checklist().templateName }}</h1>
          <table class="sh-meta">
            <tr><th>Inspection type</th><td>{{ checklist().inspectionTypeName || '—' }}</td><th>Scoring</th><td>{{ checklist().scoring || '—' }}</td></tr>
            <tr><th>Applicable assets</th><td>{{ checklist().assetCategory || checklist().assetType || 'All assets' }}</td><th>Effective</th><td>{{ fmtDate(checklist().effectiveDate) || '—' }}</td></tr>
            <tr><th>Asset</th><td class="blank"></td><th>Date</th><td class="blank"></td></tr>
            <tr><th>Location</th><td class="blank"></td><th>Inspector</th><td class="blank"></td></tr>
          </table>
          @for (s of sections(); track $index; let si = $index) {
            <h2 class="sh-sec">{{ si + 1 }}. {{ s.sectionName }}</h2>
            <table class="sh-tasks">
              <thead><tr><th class="n">#</th><th>Task</th><th>Acceptance</th><th class="res">Result</th><th class="notes">Notes</th></tr></thead>
              <tbody>
                @for (t of s.tasks; track $index; let ti = $index) {
                  <tr>
                    <td class="n">{{ si + 1 }}.{{ ti + 1 }}</td>
                    <td>{{ t.taskTitle }}@if (t.isMandatory) {<span class="req"> *</span>}</td>
                    <td>{{ t.acceptanceCriteria && t.acceptanceCriteria !== '-' ? t.acceptanceCriteria : '' }}</td>
                    <td class="res">
                      @if (t.responseType.startsWith('Numeric')) { <span class="line"></span> {{ unit(t.responseType) }} }
                      @else if (t.responseType === 'Yes / No') { <span class="box"></span>Yes <span class="box"></span>No }
                      @else if (t.responseType === 'Text') { <span class="line line--wide"></span> }
                      @else { <span class="box"></span>Pass <span class="box"></span>Fail <span class="box"></span>N/A }
                    </td>
                    <td class="notes"></td>
                  </tr>
                }
              </tbody>
            </table>
          } @empty { <p>This checklist has no tasks yet.</p> }
          <div class="sh-rules">
            @if (checklist().allMandatoryRequired) { <span>* All mandatory items required</span> }
            @if (checklist().photoOnFailedItems) { <span>Photo evidence required for failed items</span> }
            @if (checklist().criticalFailureOverridesScore) { <span>A critical failure fails the inspection</span> }
          </div>
          <div class="sh-sign">
            <div><span class="line line--sign"></span>Inspector signature</div>
            <div><span class="line line--sign"></span>Date</div>
            @if (checklist().inspectorSignatureRequired) { <div><span class="line line--sign"></span>Supervisor</div> }
          </div>
        </article>
      </div>
    </div>
  `,
  styles: `
    .pv-backdrop { position: fixed; inset: 0; z-index: 950; background: rgba(22, 20, 39, 0.45); }
    .pv-modal { position: fixed; z-index: 951; inset: 24px max(16px, calc(50vw - 460px)); display: flex; flex-direction: column; background: #f3f2f7; border-radius: 12px; overflow: hidden; box-shadow: 0 20px 60px rgba(0, 0, 0, 0.25); }
    .pv-bar { display: flex; align-items: center; gap: 10px; padding: 12px 16px; background: #fff; border-bottom: 1px solid #e6e4ee; font-size: 14px; }
    .pv-grow { flex: 1; min-width: 0; }
    .pv-btn { display: inline-flex; align-items: center; gap: 6px; height: 34px; padding: 0 12px; border: 1px solid #d6d3e0; border-radius: 8px; background: #fff; font: inherit; font-size: 13px; cursor: pointer; }
    .pv-btn--primary { background: #5b21e6; border-color: #5b21e6; color: #fff; }
    .pv-scroll { flex: 1; overflow: auto; padding: 20px; }
    .sheet { max-width: 794px; margin: 0 auto; padding: 32px 36px; background: #fff; color: #161427; font: 12px/1.45 'Red Hat Display', Arial, sans-serif; box-shadow: 0 2px 10px rgba(0, 0, 0, 0.08); box-sizing: border-box; }
    .sh-head { display: flex; justify-content: space-between; border-bottom: 3px solid #5b21e6; padding-bottom: 10px; }
    .sh-brand { font-size: 20px; font-weight: 800; color: #5b21e6; }
    .sh-sub { font-size: 10px; color: #6b6880; }
    .sh-right { text-align: right; }
    .sh-kind { font-weight: 800; }
    .sh-title { font-size: 18px; margin: 14px 0 8px; }
    .sh-meta, .sh-tasks { width: 100%; border-collapse: collapse; }
    .sh-meta th { text-align: left; color: #6b6880; font-weight: 600; width: 18%; padding: 4px 6px; }
    .sh-meta td { padding: 4px 6px; width: 32%; }
    .sh-meta td.blank { border-bottom: 1px solid #c9c6d4; }
    .sh-sec { font-size: 13px; margin: 16px 0 6px; padding: 5px 8px; background: #f1ecff; color: #3d2a86; }
    .sh-tasks th, .sh-tasks td { border: 1px solid #d6d3e0; padding: 6px; text-align: left; vertical-align: top; }
    .sh-tasks th { background: #f7f6fb; font-size: 11px; }
    .n { width: 34px; }
    .res { width: 170px; white-space: nowrap; }
    .notes { width: 22%; }
    .req { color: #dc2626; }
    .box { display: inline-block; width: 11px; height: 11px; border: 1px solid #6b6880; margin: 0 3px 0 6px; vertical-align: -1px; }
    .box:first-child { margin-left: 0; }
    .line { display: inline-block; width: 70px; border-bottom: 1px solid #6b6880; height: 12px; }
    .line--wide { width: 150px; }
    .line--sign { display: block; width: 100%; height: 34px; margin-bottom: 4px; }
    .sh-rules { display: flex; flex-direction: column; gap: 2px; margin-top: 12px; font-size: 11px; color: #6b6880; }
    .sh-sign { display: grid; grid-template-columns: repeat(3, 1fr); gap: 24px; margin-top: 28px; color: #6b6880; }
    @media print { .sheet { box-shadow: none; padding: 0; max-width: none; } }
  `
})
export class ChecklistPreview {
  readonly checklist = input.required<Checklist>();
  readonly closed = output<void>();
  readonly icons = { Printer, X };
  readonly fmtDate = fmtDate;
  private readonly sheet = viewChild.required<ElementRef<HTMLElement>>('sheet');

  sections() {
    return [...(this.checklist().sections ?? [])].sort((a, b) => a.order - b.order).map((s) => ({
      ...s,
      tasks: [...(s.tasks ?? [])].sort((a, b) => a.order - b.order)
    }));
  }

  unit(responseType: string): string {
    return /\((.+)\)/.exec(responseType)?.[1] ?? '';
  }

  print(): void {
    const c = this.checklist();
    printElement(this.sheet().nativeElement, `${c.templateName} v${c.versionLabel}`);
  }
}
