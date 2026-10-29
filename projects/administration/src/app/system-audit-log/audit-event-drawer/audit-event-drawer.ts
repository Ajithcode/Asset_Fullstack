import { Component, EventEmitter, Input, Output } from '@angular/core';
import { ArrowRight, CircleCheck, Copy, Download, FileText, LucideAngularModule, Shield, X } from 'lucide-angular';
import { AuditEvent } from '../audit-log.model';
import { flag, formatAuditTime } from '../audit-log.util';

const SEVERITY_TONES: Record<string, string> = { Critical: 'red', High: 'pink', Medium: 'orange', Low: 'blue' };
const OUTCOME_TONES: Record<string, string> = { Success: 'green', Failed: 'red', Blocked: 'grey' };

/** Right-hand "Audit Event Details" drawer. Read-only: audit events can't be edited. */
@Component({
  selector: 'app-adm-audit-event-drawer',
  imports: [LucideAngularModule],
  templateUrl: './audit-event-drawer.html',
  styleUrls: ['../../shared/admin-workspace.css', '../system-audit-log.css']
})
export class AuditEventDrawer {
  @Input({ required: true }) event!: AuditEvent;
  @Output() closed = new EventEmitter<void>();
  @Output() copyId = new EventEmitter<AuditEvent>();
  @Output() exportEvent = new EventEmitter<AuditEvent>();

  readonly icons = { X, Copy, Download, FileText, ArrowRight, Shield, CircleCheck };
  readonly formatAuditTime = formatAuditTime;
  readonly flag = flag;

  severityTone(s: string): string {
    return SEVERITY_TONES[s] ?? 'grey';
  }

  outcomeTone(o: string): string {
    return OUTCOME_TONES[o] ?? 'grey';
  }

  get riskLabel(): string {
    const r = this.event.riskScore;
    return r >= 60 ? 'High' : r >= 30 ? 'Medium' : 'Low';
  }

  get riskTone(): string {
    return this.riskLabel === 'High' ? 'red' : this.riskLabel === 'Medium' ? 'orange' : 'green';
  }
}
