import { Component, Input } from '@angular/core';
import { DataJob } from '../data-import.model';
import { formatStamp } from '../data-import.util';

/** History tab: previous runs of the job, newest first. */
@Component({
  selector: 'app-adm-job-history',
  styleUrls: ['../../shared/admin-workspace.css', '../data-import-export.css'],
  template: `
    <div class="panel__head"><h3>Run History <small class="muted">· {{ job.runs.length }} runs</small></h3></div>
    @if (job.runs.length) {
      <div class="table-wrap">
        <table class="table">
          <thead>
            <tr><th>Run ID</th><th>Started</th><th>Duration</th><th>Run By</th><th>Total</th><th>Successful</th><th>Failed</th><th>Status</th></tr>
          </thead>
          <tbody>
            @for (r of job.runs; track r.id) {
              <tr>
                <td>{{ r.id }}</td>
                <td>{{ formatStamp(r.startedAt) }}</td>
                <td>{{ duration(r.durationSec) }}</td>
                <td>{{ r.runBy }}</td>
                <td>{{ r.total.toLocaleString('en-US') }}</td>
                <td class="tone-green">{{ r.successful.toLocaleString('en-US') }}</td>
                <td [class.tone-red]="r.failed > 0">{{ r.failed.toLocaleString('en-US') }}</td>
                <td><span class="pill" [attr.data-tone]="r.status === 'Completed' ? 'green' : 'red'"><i></i>{{ r.status }}</span></td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    } @else {
      <p class="empty">This job hasn't run yet. Use Run Now to start it.</p>
    }
  `
})
export class JobHistory {
  @Input({ required: true }) job!: DataJob;
  readonly formatStamp = formatStamp;

  duration(sec: number): string {
    const m = Math.floor(sec / 60);
    return m ? `${m}m ${sec % 60}s` : `${sec}s`;
  }
}
