import { Component, computed, input, output } from '@angular/core';
import { RouterLink } from '@angular/router';
import { LucideAngularModule, CircleCheck, Clock, ClipboardList, FileText } from 'lucide-angular';
import { Inspection } from '../shared/inspection.models';
import { fmtDateTime, pillClass } from '../shared/insp-utils';

export const REQUIRED_CONTROLS = [
  'Re-authentication before signing',
  'Record signer and timestamp',
  'Lock signed inspection version',
  'Changes require a new revision'
];

/** Right column: sign-off progress of the latest inspection plus the controls every signature must satisfy. */
@Component({
  selector: 'app-insp-signoff-panel',
  standalone: true,
  imports: [RouterLink, LucideAngularModule],
  template: `
    <div class="card">
      <div class="card-head">
        <h2 class="grow">Inspection Sign-off</h2>
        @if (inspection(); as i) {
          <a class="link sm" routerLink="/inspection/execution" [queryParams]="{ code: i.inspectionCode }">View Inspection</a>
        }
      </div>
      <div class="card-body body">
        @if (inspection(); as i) {
          <div class="sub-box">
            <div class="ins">
              <lucide-icon [img]="icons.ClipboardList" [size]="22"></lucide-icon>
              <div><b>{{ i.inspectionCode }}</b><span>{{ i.title }}</span></div>
            </div>
            <ol class="steps">
              @for (s of i.signOff; track $index) {
                <li>
                  <span class="n" [class]="'n n--' + tone(s.state)">{{ $index + 1 }}</span>
                  <div class="grow"><b>{{ s.role }}</b><span class="muted">{{ s.name || '—' }}</span></div>
                  <div class="st">
                    <span [class]="pillClass(s.state)">
                      <lucide-icon [img]="s.state === 'Signed' ? icons.CircleCheck : icons.Clock" [size]="13"></lucide-icon>{{ s.state }}</span>
                    <span class="muted">{{ s.signedAt ? fmtDateTime(s.signedAt) : '—' }}</span>
                  </div>
                </li>
              }
            </ol>
            @if (lastSigned(); as ls) {
              <div class="rec">
                <dl class="kv kv-sm">
                  <dt>Signed record</dt><dd></dd>
                  <dt>Inspection status</dt><dd>{{ i.status }}</dd>
                  <dt>Steps signed</dt><dd>{{ signedCount() }} of {{ i.signOff.length }}</dd>
                  <dt>Signed by</dt><dd>{{ ls.name }} ({{ ls.role }})</dd>
                  <dt>Signed on</dt><dd>{{ fmtDateTime(ls.signedAt) }}</dd>
                </dl>
                <button type="button" class="link sm" (click)="history.emit()"><lucide-icon [img]="icons.FileText" [size]="15"></lucide-icon>View signing record</button>
              </div>
            } @else {
              <p class="muted">No step has been signed yet.</p>
            }
          </div>
        } @else {
          <div class="empty">{{ loading() ? 'Loading inspections…' : 'No inspection with sign-off steps yet.' }}</div>
        }
      </div>
    </div>
    <div class="card">
      <div class="card-head"><h2>Required Controls</h2></div>
      <ul class="ctl">
        @for (c of controls; track c) {
          <li><lucide-icon [img]="icons.CircleCheck" [size]="18"></lucide-icon>{{ c }}</li>
        }
      </ul>
    </div>
  `,
  styleUrls: ['../shared/insp-page.css'],
  styles: [
    `:host { display: flex; flex-direction: column; gap: 14px; min-width: 0; }
     .body { padding-top: 0; } .sm { font-size: 13px; }
     .ins { display: flex; gap: 10px; align-items: flex-start; margin-bottom: 10px; }
     .ins div { display: flex; flex-direction: column; font-size: 13.5px; }
     .steps { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: 12px; }
     .steps li { display: flex; gap: 10px; align-items: flex-start; font-size: 13.5px; }
     .steps .grow { display: flex; flex-direction: column; }
     .n { display: inline-flex; align-items: center; justify-content: center; flex: 0 0 auto; width: 26px; height: 26px; border-radius: 50%; color: #fff; font-weight: 700; font-size: 12.5px; background: #9e9bb0; }
     .n--good { background: var(--ip-good); } .n--warn { background: var(--ip-warn); }
     .st { display: flex; flex-direction: column; align-items: flex-end; gap: 4px; }
     .rec { display: flex; flex-wrap: wrap; align-items: flex-end; gap: 8px 12px; margin-top: 12px; padding-top: 10px; border-top: 1px solid var(--ip-border); }
     .kv-sm { flex: 1 1 200px; font-size: 12.5px; gap: 4px 10px; }
     .kv-sm dt:first-child { color: var(--ip-text); font-weight: 700; }
     .ctl { list-style: none; margin: 0; padding: 0 18px 16px; display: flex; flex-direction: column; gap: 10px; font-size: 13.5px; }
     .ctl li { display: flex; gap: 10px; align-items: center; }
     .ctl lucide-icon { color: var(--ip-good); }`
  ]
})
export class SignoffPanel {
  readonly icons = { CircleCheck, Clock, ClipboardList, FileText };
  readonly inspection = input<Inspection | undefined>();
  readonly loading = input(false);
  readonly history = output<void>();
  readonly controls = REQUIRED_CONTROLS;
  readonly pillClass = pillClass;
  readonly fmtDateTime = fmtDateTime;

  readonly signedCount = computed(() => (this.inspection()?.signOff ?? []).filter((s) => s.state === 'Signed').length);
  readonly lastSigned = computed(() =>
    (this.inspection()?.signOff ?? [])
      .filter((s) => s.state === 'Signed' && s.signedAt)
      .sort((a, b) => b.signedAt!.localeCompare(a.signedAt!))[0]
  );

  tone(state: string): string {
    return state === 'Signed' ? 'good' : state === 'Awaiting review' ? 'warn' : 'idle';
  }
}
