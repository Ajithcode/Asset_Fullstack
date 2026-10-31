import { Component, computed, input, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { LucideAngularModule, Download, Search } from 'lucide-angular';
import { Inspection } from '../shared/inspection.models';
import { Pager, downloadCsv, fmtDateTime, pillClass } from '../shared/insp-utils';

interface HistoryRow {
  code: string;
  title: string;
  role: string;
  name: string;
  state: string;
  signedAt: string;
}

/** Every signed sign-off step across all inspections, newest first. */
@Component({
  selector: 'app-insp-signing-history',
  standalone: true,
  imports: [FormsModule, RouterLink, LucideAngularModule],
  template: `
    <div class="card">
      <div class="card-head"><h2 class="grow">Signing History</h2>
        <button type="button" class="btn btn-outline btn-sm" [disabled]="!rows().length" (click)="export()"><lucide-icon [img]="icons.Download" [size]="14"></lucide-icon>Export</button></div>
      <div class="filters">
        <label class="search"><lucide-icon [img]="icons.Search" [size]="16"></lucide-icon>
          <input type="search" placeholder="Search by inspection, signer or role..." [ngModel]="q()" (ngModelChange)="q.set($event); pager.page = 1" aria-label="Search signing history" /></label>
      </div>
      <div class="table-wrap">
        <table class="table">
          <thead><tr><th>Inspection</th><th>Title</th><th>Role</th><th>Signed by</th><th>Signed on</th><th>State</th></tr></thead>
          <tbody>
            @for (r of pager.slice(rows()); track $index) {
              <tr>
                <td><a class="link" routerLink="/inspection/execution" [queryParams]="{ code: r.code }">{{ r.code }}</a></td>
                <td>{{ r.title }}</td><td>{{ r.role }}</td><td class="strong">{{ r.name }}</td>
                <td class="nowrap">{{ fmtDateTime(r.signedAt) }}</td><td><span [class]="pillClass(r.state)">{{ r.state }}</span></td>
              </tr>
            } @empty {
              <tr><td colspan="6" class="empty">{{ loading() ? 'Loading…' : 'No signed steps recorded yet.' }}</td></tr>
            }
          </tbody>
        </table>
      </div>
      <div class="pager">
        <span class="grow">{{ pager.label(rows().length, 'signatures') }}</span>
        @for (p of pager.pageList(rows().length); track p) {
          <button type="button" [class.is-active]="pager.page === p" (click)="pager.page = p">{{ p }}</button>
        }
      </div>
    </div>
  `,
  styleUrls: ['../shared/insp-page.css'],
  styles: ['.table tbody tr { cursor: default; }']
})
export class SigningHistory {
  readonly icons = { Download, Search };
  readonly inspections = input<Inspection[]>([]);
  readonly loading = input(false);
  readonly q = signal('');
  readonly pager = new Pager(10);
  readonly pillClass = pillClass;
  readonly fmtDateTime = fmtDateTime;

  readonly rows = computed<HistoryRow[]>(() => {
    const q = this.q().trim().toLowerCase();
    return this.inspections()
      .flatMap((i) =>
        (i.signOff ?? [])
          .filter((s) => s.state === 'Signed' && s.signedAt)
          .map((s) => ({ code: i.inspectionCode, title: i.title, role: s.role, name: s.name, state: s.state, signedAt: s.signedAt! }))
      )
      .filter((r) => !q || `${r.code} ${r.title} ${r.role} ${r.name}`.toLowerCase().includes(q))
      .sort((a, b) => b.signedAt.localeCompare(a.signedAt));
  });

  export(): void {
    downloadCsv(
      'signing-history.csv',
      this.rows().map((r) => ({ Inspection: r.code, Title: r.title, Role: r.role, 'Signed by': r.name, 'Signed on': fmtDateTime(r.signedAt) }))
    );
  }
}
