import { Component, input, output } from '@angular/core';
import { LucideAngularModule, Clock, X } from 'lucide-angular';
import { ReportTemplate } from '../shared/inspection.models';
import { fmtDateTime, pillClass } from '../shared/insp-utils';

/** Side panel listing every version of a report template with its audit trail. */
@Component({
  selector: 'app-report-version-history',
  standalone: true,
  imports: [LucideAngularModule],
  template: `
    <div class="bd" (click)="closed.emit()"></div>
    <aside class="panel" role="dialog" aria-modal="true" aria-labelledby="rt-hist-title">
      <div class="head"><h2 id="rt-hist-title">Version History</h2>
        <button type="button" class="ib" aria-label="Close" (click)="closed.emit()"><lucide-icon [img]="icons.X" [size]="18"></lucide-icon></button></div>
      <div class="body">
        <p class="muted">{{ current().reportTitle }} · {{ current().reportTemplateCode }}</p>
        <ol>
          @for (v of versions(); track v.id) {
            <li [class.cur]="v.id === current().id">
              <div class="top"><strong>v{{ v.versionLabel }}</strong><span [class]="pillClass(v.status)">{{ v.status }}</span><span class="muted">{{ v.reportTemplateCode }}</span>
                <span class="grow"></span>
                @if (v.id !== current().id) { <button type="button" class="lnk" (click)="open.emit(v.id)">Open</button> } @else { <span class="muted">Current</span> }</div>
              <div class="muted"><lucide-icon [img]="icons.Clock" [size]="13"></lucide-icon> Created {{ fmtDateTime(v.createdAt) || '—' }} by {{ v.createdBy || '—' }}</div>
              @if (v.updatedAt) { <div class="muted"><lucide-icon [img]="icons.Clock" [size]="13"></lucide-icon> Updated {{ fmtDateTime(v.updatedAt) }} by {{ v.updatedBy || '—' }}</div> }
            </li>
          }
        </ol>
      </div>
    </aside>
  `,
  styles: `
    .bd { position: fixed; inset: 0; z-index: 900; background: rgba(22, 20, 39, 0.35); }
    .panel { position: fixed; top: 0; right: 0; bottom: 0; z-index: 901; display: flex; flex-direction: column; width: min(480px, 100vw); background: #fff; color: #161427; box-shadow: -8px 0 32px rgba(0, 0, 0, 0.15); font-size: 13px; }
    .head { display: flex; align-items: center; padding: 18px 20px; border-bottom: 1px solid #e6e4ee; }
    .head h2 { flex: 1; margin: 0; font-size: 18px; }
    .ib { display: inline-flex; border: 0; background: none; padding: 6px; border-radius: 6px; cursor: pointer; color: #6b6880; }
    .body { flex: 1; overflow-y: auto; padding: 16px 20px; }
    .muted { color: #6b6880; }
    .grow { flex: 1; }
    ol { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 10px; }
    li { display: flex; flex-direction: column; gap: 4px; padding: 10px 12px; border: 1px solid #e6e4ee; border-radius: 10px; }
    li.cur { border-color: #5b21e6; background: #f1ecff; }
    .top { display: flex; align-items: center; gap: 8px; }
    .lnk { background: none; border: 0; padding: 0; font: inherit; color: #5b21e6; font-weight: 600; cursor: pointer; }
    .pill { display: inline-flex; padding: 3px 10px; border-radius: 999px; font-size: 12px; font-weight: 600; background: #f0eff4; color: #6b6880; }
    .pill--green { background: #e3f6ea; color: #16a34a; }
  `
})
export class ReportVersionHistory {
  readonly current = input.required<ReportTemplate>();
  readonly versions = input.required<ReportTemplate[]>();
  readonly open = output<string>();
  readonly closed = output<void>();
  readonly icons = { Clock, X };
  readonly pillClass = pillClass;
  readonly fmtDateTime = fmtDateTime;
}
