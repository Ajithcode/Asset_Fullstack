import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { LucideAngularModule, Check, Pencil, RefreshCw, X } from 'lucide-angular';
import { AccessService, pushToast } from 'shared-ui';
import { apiErrorMessage } from '../../services/asset-api';
import { AssessmentDetail, AssessmentVersion, CriticalityApiService } from '../criticality-api.service';

/** One assessment: score breakdown, override, consequences, documents, approval trail, version history, Approve / Reject. */
@Component({
  selector: 'app-assessment-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule],
  templateUrl: './assessment-detail.html',
  styleUrl: './assessment-detail.css',
})
export class AssessmentDetailPage implements OnInit {
  private readonly api = inject(CriticalityApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly access = inject(AccessService);

  readonly icons = { Check, Pencil, RefreshCw, X };
  a: AssessmentDetail | null = null;
  versions: AssessmentVersion[] = [];
  loading = true;
  loadError = '';
  busy = false;
  error = '';
  comment = '';
  rejecting = false;
  rejectComment = '';

  get canAssess(): boolean { return this.access.can('AssetCriticality', 'assess'); }

  ngOnInit(): void {
    this.route.paramMap.subscribe((p) => this.load(p.get('id') ?? ''));
  }

  private load(id: string): void {
    this.loading = true;
    this.loadError = '';
    this.api.assessment(id).subscribe({
      next: (a) => {
        this.a = a;
        this.loading = false;
        this.api.versions(a.asset.assetRegistrationId).subscribe({ next: (v) => (this.versions = v), error: () => (this.versions = []) });
      },
      error: (e) => { this.loading = false; this.loadError = apiErrorMessage(e, 'Could not load the assessment.'); },
    });
  }

  levelColor(): string { return this.a?.finalLevelColor || '#6b7280'; }

  tone(status: string): string {
    switch (status) {
      case 'Approved': return 'tone-ok';
      case 'Rejected': return 'tone-bad';
      case 'PendingApproval':
      case 'Submitted': return 'tone-warn';
      case 'Draft': return 'tone-info';
      default: return 'tone-muted';
    }
  }

  label(status: string): string { return status === 'PendingApproval' ? 'Pending Approval' : status; }

  private run(call: ReturnType<CriticalityApiService['approve']>, message: string): void {
    if (this.busy) return;
    this.busy = true;
    this.error = '';
    call.subscribe({
      next: (a) => { this.busy = false; this.a = a; this.rejecting = false; pushToast(message); this.api.versions(a.asset.assetRegistrationId).subscribe((v) => (this.versions = v)); },
      error: (e) => { this.busy = false; this.error = apiErrorMessage(e, 'The action could not be completed.'); },
    });
  }

  submit(): void { if (this.a) this.run(this.api.submit(this.a.id, this.a.concurrencyVersion), 'Submitted for approval.'); }
  approve(): void { if (this.a) this.run(this.api.approve(this.a.id, this.comment.trim() || null), 'Assessment approved. The asset criticality is updated.'); }

  reject(): void {
    if (!this.a) return;
    if (!this.rejectComment.trim()) { this.error = 'A comment is required to reject.'; return; }
    this.run(this.api.reject(this.a.id, this.rejectComment.trim()), 'Assessment rejected.');
  }

  cancelDraft(): void {
    if (!this.a || this.busy || !confirm('Cancel this assessment? It will be discarded.')) return;
    this.busy = true;
    this.api.cancel(this.a.id).subscribe({
      next: () => { pushToast('Assessment cancelled.'); this.router.navigate(['/assets/criticality']); },
      error: (e) => { this.busy = false; this.error = apiErrorMessage(e, 'Could not cancel.'); },
    });
  }

  reassess(): void {
    if (this.a) this.router.navigate(['/assets/criticality/assess', this.a.asset.assetRegistrationId]);
  }
}
