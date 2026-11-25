import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { Subject, Subscription, debounceTime, of, switchMap, catchError } from 'rxjs';
import { LucideAngularModule, Plus, Save, Send, Upload, X } from 'lucide-angular';
import { pushToast } from 'shared-ui';
import { apiErrorMessage } from '../../services/asset-api';
import {
  AssessmentAttachment, AssessmentDetail, AssessmentDraftContext, AssessmentScorePreview, AssessmentTemplate,
  AssetSearchHit, CriticalityApiService, SaveAssessmentBody,
} from '../criticality-api.service';

/** Short meaning of each 1-5 rating, shown under every criterion. */
export const RATING_HELP: Record<number, string> = {
  1: 'Negligible: no real impact if this asset fails',
  2: 'Minor: small, easily absorbed impact',
  3: 'Moderate: noticeable impact, manageable workaround',
  4: 'Major: serious impact, hard to work around',
  5: 'Severe: critical impact, cannot be tolerated',
};

/**
 * Assessment form: A. asset, B. template and ratings with a live score from the server, C. consequences, documents and
 * review date. Save Draft keeps it private to the assessor; Submit sends it for approval. Scoring is done by the API only.
 */
@Component({
  selector: 'app-assessment-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule],
  templateUrl: './assessment-form.html',
  styleUrl: './assessment-form.css',
})
export class AssessmentForm implements OnInit, OnDestroy {
  private readonly api = inject(CriticalityApiService);
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);

  readonly icons = { Plus, Save, Send, Upload, X };
  readonly ratingValues = [1, 2, 3, 4, 5];
  readonly ratingHelp = RATING_HELP;

  ctx: AssessmentDraftContext | null = null;
  editing: AssessmentDetail | null = null;
  loading = true;
  loadError = '';
  saving = false;
  formError = '';
  fieldErrors: Record<string, string> = {};

  // A. asset search (New Assessment without an asset)
  assetQuery = '';
  hits: AssetSearchHit[] = [];
  searching = false;
  private readonly search$ = new Subject<string>();

  // B. template, ratings, override
  templateId = '';
  ratings: Record<string, number | null> = {};
  comments: Record<string, string> = {};
  assessmentDate = '';
  overrideOn = false;
  overrideLevel = '';
  justification = '';
  preview: AssessmentScorePreview | null = null;
  previewError = '';
  private readonly preview$ = new Subject<void>();

  // C. consequences, documents, review
  failureConsequences = '';
  alternativeEquipment: boolean | null = null;
  downtimeImpact = '';
  financialLoss = '';
  safetyEnvironmental = '';
  attachments: AssessmentAttachment[] = [];
  uploading = false;
  nextReviewDate = '';
  nextReviewTouched = false;
  remarks = '';

  private subs = new Subscription();

  ngOnInit(): void {
    this.subs.add(
      this.search$.pipe(debounceTime(250), switchMap((q) => {
        if (q.trim().length < 2) { this.searching = false; return of({ items: [] as AssetSearchHit[] }); }
        this.searching = true;
        return this.api.searchAssets(q.trim()).pipe(catchError(() => of({ items: [] as AssetSearchHit[] })));
      })).subscribe((r) => { this.hits = r.items; this.searching = false; }),
    );
    this.subs.add(
      this.preview$.pipe(debounceTime(200), switchMap(() => this.requestPreview())).subscribe((p) => {
        if (p === 'error') return;
        this.preview = p;
        this.previewError = '';
        if (p && !this.nextReviewTouched && p.suggestedNextReviewDate) this.nextReviewDate = p.suggestedNextReviewDate.slice(0, 10);
      }),
    );

    const id = this.route.snapshot.paramMap.get('id');
    const assetId = this.route.snapshot.paramMap.get('assetId');
    if (id) {
      this.api.assessment(id).subscribe({
        next: (a) => {
          if (!a.actions.canEdit) { this.router.navigate(['/assets/criticality/assessments', id], { replaceUrl: true }); return; }
          this.editing = a;
          this.loadContext(a.asset.assetRegistrationId);
        },
        error: (e) => this.fail(e, 'Could not load the assessment.'),
      });
    } else if (assetId) {
      this.loadContext(assetId);
    } else {
      this.loading = false;
    }
  }

  ngOnDestroy(): void {
    this.subs.unsubscribe();
  }

  // ----- Loading -----

  private loadContext(assetId: string): void {
    this.loading = true;
    this.api.draftContext(assetId).subscribe({
      next: (ctx) => {
        if (!this.editing && ctx.openAssessmentId) {
          pushToast('This asset already has an assessment in progress. Opening it.');
          this.router.navigate(['/assets/criticality/assessments', ctx.openAssessmentId], { replaceUrl: true });
          return;
        }
        this.ctx = ctx;
        this.assessmentDate = (this.editing?.assessmentDate ?? ctx.assessmentDate).slice(0, 10);
        this.templateId = this.editing?.templateId ?? ctx.defaultTemplateId ?? ctx.templates[0]?.id ?? '';
        if (this.editing) this.fillFrom(this.editing);
        this.loading = false;
        this.queuePreview();
      },
      error: (e) => this.fail(e, 'Could not load the asset.'),
    });
  }

  private fillFrom(a: AssessmentDetail): void {
    for (const c of a.criteria) { this.ratings[c.code] = c.rating; if (c.comment) this.comments[c.code] = c.comment; }
    this.overrideOn = a.isOverride;
    this.overrideLevel = a.isOverride ? a.finalLevel : '';
    this.justification = a.justification ?? '';
    this.failureConsequences = a.risk.failureConsequences ?? '';
    this.alternativeEquipment = a.risk.alternativeEquipmentAvailable;
    this.downtimeImpact = a.risk.downtimeImpact ?? '';
    this.financialLoss = a.risk.financialLoss ?? '';
    this.safetyEnvironmental = a.risk.safetyEnvironmentalConsequences ?? '';
    this.attachments = [...a.attachments];
    this.nextReviewDate = a.nextReviewDate?.slice(0, 10) ?? '';
    this.nextReviewTouched = true;
    this.remarks = a.remarks ?? '';
  }

  private fail(e: unknown, fallback: string): void {
    this.loading = false;
    this.loadError = apiErrorMessage(e, fallback);
  }

  // ----- A. asset -----

  onAssetQuery(): void { this.search$.next(this.assetQuery); }

  pickAsset(hit: AssetSearchHit): void {
    this.hits = [];
    this.assetQuery = '';
    this.router.navigate(['/assets/criticality/assess', hit.assetRegistrationId], { replaceUrl: true });
  }

  // ----- B. template and ratings -----

  get template(): AssessmentTemplate | null {
    return this.ctx?.templates.find((t) => t.id === this.templateId) ?? null;
  }

  onTemplateChange(): void {
    const codes = new Set(this.template?.criteria.map((c) => c.code));
    for (const k of Object.keys(this.ratings)) if (!codes.has(k)) delete this.ratings[k];
    this.queuePreview();
  }

  rate(code: string, value: number): void {
    this.ratings[code] = value;
    this.queuePreview();
  }

  get allRated(): boolean {
    return !!this.template && this.template.criteria.every((c) => !!this.ratings[c.code]);
  }

  get ratedCount(): number {
    return this.template?.criteria.filter((c) => !!this.ratings[c.code]).length ?? 0;
  }

  onOverrideToggle(): void {
    if (!this.overrideOn) this.overrideLevel = '';
    this.queuePreview();
  }

  queuePreview(): void { this.preview$.next(); }

  private requestPreview() {
    if (!this.ctx || !this.template || !this.allRated) { this.preview = null; return of(null); }
    return this.api.scorePreview({
      assetRegistrationId: this.ctx.asset.assetRegistrationId,
      templateId: this.template.id,
      ratings: this.template.criteria.map((c) => ({ criterionCode: c.code, rating: this.ratings[c.code] ?? null })),
      overrideLevel: this.overrideOn && this.overrideLevel ? this.overrideLevel : null,
      assessmentDate: this.assessmentDate || null,
    }).pipe(catchError((e) => { this.previewError = apiErrorMessage(e, 'Could not work out the score.'); return of('error' as const); }));
  }

  get overrideChoices() {
    return (this.ctx?.levels ?? []).filter((l) => l.name !== this.preview?.calculatedLevel?.name);
  }

  get justificationRequired(): boolean {
    return !!this.preview?.justificationRequired || (this.overrideOn && !!this.overrideLevel);
  }

  levelColor(level: { color: string | null } | null | undefined): string {
    return level?.color || '#6b7280';
  }

  // ----- C. documents -----

  onFiles(event: Event): void {
    const input = event.target as HTMLInputElement;
    const files = Array.from(input.files ?? []);
    input.value = '';
    for (const file of files) {
      this.uploading = true;
      this.api.uploadAttachment(file).subscribe({
        next: (a) => { this.attachments = [...this.attachments, a]; this.uploading = false; },
        error: (e) => { this.uploading = false; this.formError = apiErrorMessage(e, `Could not upload ${file.name}.`); },
      });
    }
  }

  removeAttachment(i: number): void {
    this.attachments = this.attachments.filter((_, k) => k !== i);
  }

  size(bytes: number): string {
    return bytes >= 1048576 ? (bytes / 1048576).toFixed(1) + ' MB' : Math.max(1, Math.round(bytes / 1024)) + ' KB';
  }

  // ----- Save / submit -----

  private body(): SaveAssessmentBody {
    return {
      assetRegistrationId: this.ctx!.asset.assetRegistrationId,
      templateId: this.templateId,
      assessmentDate: this.assessmentDate || null,
      ratings: this.template!.criteria.map((c) => ({ criterionCode: c.code, rating: this.ratings[c.code] ?? null, comment: this.comments[c.code]?.trim() || null })),
      overrideLevel: this.overrideOn && this.overrideLevel ? this.overrideLevel : null,
      justification: this.justification.trim() || null,
      risk: {
        failureConsequences: this.failureConsequences.trim() || null,
        alternativeEquipmentAvailable: this.alternativeEquipment,
        downtimeImpact: this.downtimeImpact.trim() || null,
        financialLoss: this.financialLoss.trim() || null,
        safetyEnvironmentalConsequences: this.safetyEnvironmental.trim() || null,
      },
      attachments: this.attachments,
      nextReviewDate: this.nextReviewDate || null,
      remarks: this.remarks.trim() || null,
      concurrencyVersion: this.editing?.concurrencyVersion ?? null,
    };
  }

  /** Save Draft: only the asset and the template are needed; the rest is checked on Submit. */
  private validate(forSubmit: boolean): boolean {
    this.fieldErrors = {};
    this.formError = '';
    if (!this.ctx || !this.template) { this.formError = 'Choose an asset and a template.'; return false; }
    if (!forSubmit) return true;
    if (!this.allRated) { this.fieldErrors['ratings'] = 'Rate every criterion from 1 to 5.'; this.formError = `Rate every criterion before submitting (${this.ratedCount} of ${this.template.criteria.length} rated).`; return false; }
    if (this.overrideOn && !this.overrideLevel) { this.fieldErrors['override'] = 'Choose the level to apply.'; this.formError = 'Choose the override level.'; return false; }
    if (this.justificationRequired && !this.justification.trim()) {
      this.fieldErrors['justification'] = this.overrideOn ? 'A justification is required for an override.' : 'A justification is required when the criticality is lowered.';
      this.formError = this.fieldErrors['justification'];
      return false;
    }
    return true;
  }

  saveDraft(): void { this.persist(false); }
  submit(): void { this.persist(true); }

  private persist(thenSubmit: boolean): void {
    if (this.saving || !this.validate(thenSubmit)) return;
    this.saving = true;
    const body = this.body();
    const save$ = this.editing ? this.api.updateAssessment(this.editing.id, body) : this.api.createAssessment(body);
    save$.subscribe({
      next: (saved) => {
        if (!thenSubmit) { this.done(saved, 'Draft saved.'); return; }
        this.api.submit(saved.id, saved.concurrencyVersion).subscribe({
          next: (sent) => this.done(sent, 'Submitted for approval.'),
          error: (e) => {
            // The draft is kept: carry on editing it
            this.editing = saved;
            this.saving = false;
            this.formError = apiErrorMessage(e, 'The draft was saved but could not be submitted.');
          },
        });
      },
      error: (e) => { this.saving = false; this.formError = apiErrorMessage(e, 'The assessment could not be saved.'); },
    });
  }

  private done(a: AssessmentDetail, message: string): void {
    this.saving = false;
    pushToast(message);
    this.router.navigate(['/assets/criticality/assessments', a.id]);
  }

  cancel(): void {
    this.router.navigate(this.editing ? ['/assets/criticality/assessments', this.editing.id] : ['/assets/criticality']);
  }
}
