import { Component, computed, inject, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { InspectionApi } from '../shared/inspection-api';
import { Checklist } from '../shared/inspection.models';
import { fmtDate, fmtDateTime, pillClass } from '../shared/insp-utils';

/** "Usage & Versions" tab: where the checklist is used and every version of it. */
@Component({
  selector: 'app-checklist-usage',
  standalone: true,
  imports: [RouterLink],
  template: `
    <div class="grid">
      <div class="box"><span class="muted">Modules</span>
        <div class="tags">@for (m of checklist().modules; track m) { <span class="tag" [attr.data-m]="m">{{ m }}</span> } @empty { <span class="muted">—</span> }</div></div>
      <div class="box"><span class="muted">Inspection plans</span><strong class="big">{{ plans().length }}</strong></div>
      <div class="box"><span class="muted">Inspections (open)</span><strong class="big">{{ inspections().total }} <span class="muted">({{ inspections().open }})</span></strong></div>
    </div>

    @if (plans().length) {
      <h3>Plans using this checklist</h3>
      <ul class="plain">
        @for (p of plans(); track p.id) { <li><span class="code">{{ p.planCode }}</span> {{ p.planName }} <span class="muted">· {{ p.frequency }}</span></li> }
      </ul>
      <a class="lnk" routerLink="/inspection/plans-schedules">Open plans &amp; schedules →</a>
    }

    <h3>Version history</h3>
    <div class="wrap">
      <table>
        <thead><tr><th>Version</th><th>Code</th><th>Status</th><th>Effective</th><th>Last change</th><th></th></tr></thead>
        <tbody>
          @for (v of versions(); track v.id) {
            <tr [class.cur]="v.id === checklist().id">
              <td><strong>v{{ v.versionLabel }}</strong></td>
              <td>{{ v.checklistCode }}</td>
              <td><span [class]="pillClass(v.status)">{{ v.status }}</span></td>
              <td>{{ fmtDate(v.effectiveDate) || '—' }}</td>
              <td>{{ fmtDateTime(v.updatedAt || v.createdAt) }} <span class="muted">{{ v.updatedBy || v.createdBy }}</span></td>
              <td>@if (v.id !== checklist().id) { <button type="button" class="lnk" (click)="selectId.emit(v.id)">Open</button> } @else { <span class="muted">Current</span> }</td>
            </tr>
          }
        </tbody>
      </table>
    </div>
  `,
  styleUrls: ['./checklist-usage.css']
})
export class ChecklistUsage {
  private readonly api = inject(InspectionApi);
  readonly checklist = input.required<Checklist>();
  readonly versions = input.required<Checklist[]>();
  readonly selectId = output<string>();
  readonly pillClass = pillClass;
  readonly fmtDate = fmtDate;
  readonly fmtDateTime = fmtDateTime;

  readonly plans = computed(() => this.api.plans.items().filter((p) => p.checklistCode === this.checklist().checklistCode));

  readonly inspections = computed(() => {
    const rows = this.api.inspections.items().filter((i) => i.checklistCode === this.checklist().checklistCode);
    return { total: rows.length, open: rows.filter((i) => !['Completed', 'Cancelled'].includes(i.status ?? '')).length };
  });
}
