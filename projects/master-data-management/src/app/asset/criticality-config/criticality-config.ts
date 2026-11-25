import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { LucideAngularModule, Plus, SlidersHorizontal } from 'lucide-angular';
import { AccessService, AuthService, MASTER_DATA_BASE_PATH, pushToast } from 'shared-ui';
import { apiErrorMessage } from '../../shared/mdm-api';
import {
  ApprovalRuleDto,
  CriterionDto,
  CriticalityConfigService,
  LevelDto,
  ReassessmentRuleDto,
  TemplateDto,
  TemplateSave
} from './criticality-config.service';

type Tab = 'levels' | 'criteria' | 'templates' | 'approvals' | 'reassessment';

interface LevelRow extends LevelDto {
  /** Editable copies of the range fields (empty string = not set). */
  min: number | null;
  max: number | null;
  order: number | null;
  colour: string;
}

interface TemplateForm extends TemplateSave {
  id: string | null;
  version: number | null;
  rows: { criterionId: string; weight: number | null }[];
}

interface RuleRow extends ApprovalRuleDto {
  rolesText: string;
}

const FLOOR = 20;
const CEILING = 100;

/** Master Data Management > Asset > Asset Criticality Configuration. Everyone with Assets View reads; Assets Approve changes. */
@Component({
  selector: 'app-mdm-criticality-config',
  imports: [FormsModule, RouterLink, LucideAngularModule],
  templateUrl: './criticality-config.html',
  styleUrls: ['../../shared/styles/mdm-page.css', '../level-page.css', '../../shared/styles/mdm-purple-theme.css', './criticality-config.css']
})
export class CriticalityConfigPage implements OnInit {
  private readonly api = inject(CriticalityConfigService);
  private readonly access = inject(AccessService);
  private readonly auth = inject(AuthService);

  readonly basePath = MASTER_DATA_BASE_PATH;
  readonly icons = { SlidersHorizontal, Plus };
  readonly tabs: { key: Tab; label: string }[] = [
    { key: 'levels', label: 'Levels' },
    { key: 'criteria', label: 'Criteria' },
    { key: 'templates', label: 'Templates' },
    { key: 'approvals', label: 'Approval Matrix' },
    { key: 'reassessment', label: 'Reassessment Rules' }
  ];

  tab: Tab = 'levels';
  toast = '';
  error = '';
  busy = false;
  private toastTimer?: ReturnType<typeof setTimeout>;

  levels: LevelRow[] = [];
  criteria: CriterionDto[] = [];
  templates: TemplateDto[] = [];
  showRetired = false;
  rules: RuleRow[] = [];
  reviews: (ReassessmentRuleDto & { months: number | null })[] = [];

  criterionForm: { id: string | null; name: string; code: string; description: string; isActive: boolean } | null = null;
  templateForm: TemplateForm | null = null;
  versions: TemplateDto[] = [];
  versionsOf = '';

  /** Assets Approve = may change the configuration (the API checks it too). */
  get canEdit(): boolean {
    return this.access.can('Assets', 'approve');
  }

  ngOnInit(): void {
    this.reloadAll();
  }

  private reloadAll(): void {
    this.loadLevels();
    this.loadCriteria();
    this.loadTemplates();
    this.api.approvalRules().subscribe({
      next: (rules) => (this.rules = rules.map((r) => ({ ...r, rolesText: r.approverRoles.join(', ') }))),
      error: (e) => this.fail('Could not load the approval matrix', e)
    });
    this.api.reassessmentRules().subscribe({
      next: (rules) => (this.reviews = rules.map((r) => ({ ...r, months: r.reviewIntervalMonths }))),
      error: (e) => this.fail('Could not load the reassessment rules', e)
    });
  }

  select(tab: Tab): void {
    this.tab = tab;
    this.error = '';
  }

  // ---------------------------------------------------------------- levels

  private loadLevels(): void {
    this.api.levels().subscribe({
      next: (list) =>
        (this.levels = list
          .filter((l) => (l.status ?? 'Active') !== 'Inactive')
          .map((l) => ({ ...l, min: l.minScore, max: l.maxScore, order: l.displayOrder, colour: l.color ?? '#757575' }))
          .sort((a, b) => (a.order ?? 99) - (b.order ?? 99) || a.criticalityName.localeCompare(b.criticalityName))),
      error: (e) => this.fail('Could not load the levels', e)
    });
  }

  get scoreLevels(): LevelRow[] {
    return this.levels.filter((l) => l.minScore !== null || l.maxScore !== null || (l.min !== null && l.max !== null));
  }

  /** Overlaps and gaps in the edited ranges, so the problem shows before saving. */
  get rangeProblems(): string[] {
    const rows = this.scoreLevels.filter((l) => l.min !== null && l.max !== null).sort((a, b) => a.min! - b.min!);
    const problems: string[] = [];
    for (const r of rows) {
      if (r.min! > r.max!) problems.push(`${r.criticalityName}: the minimum is above the maximum.`);
    }
    if (problems.length) return problems;
    for (let i = 1; i < rows.length; i++) {
      const prev = rows[i - 1];
      const cur = rows[i];
      if (cur.min! <= prev.max!) problems.push(`${prev.criticalityName} (${prev.min}-${prev.max}) overlaps ${cur.criticalityName} (${cur.min}-${cur.max}).`);
      else if (cur.min! > prev.max! + 1) problems.push(`Scores ${prev.max! + 1}-${cur.min! - 1} between ${prev.criticalityName} and ${cur.criticalityName} belong to no level.`);
    }
    if (rows.length && rows[0].min! > FLOOR) problems.push(`Scores ${FLOOR}-${rows[0].min! - 1} belong to no level.`);
    if (rows.length && rows[rows.length - 1].max! < CEILING) problems.push(`Scores ${rows[rows.length - 1].max! + 1}-${CEILING} belong to no level.`);
    return problems;
  }

  saveLevels(): void {
    const rows = this.scoreLevels;
    if (rows.some((l) => l.min === null || l.max === null)) {
      this.error = 'Enter the minimum and maximum score of every level.';
      return;
    }
    this.run(
      this.api.saveRanges(
        rows.map((l, i) => ({ id: l.id, minScore: l.min!, maxScore: l.max!, color: l.colour, displayOrder: l.order ?? i + 1 })),
        this.auth.getUserEmail() || 'unknown'
      ),
      'Levels saved.',
      () => this.loadLevels()
    );
  }

  // ---------------------------------------------------------------- criteria

  private loadCriteria(): void {
    this.api.criteria().subscribe({ next: (c) => (this.criteria = c), error: (e) => this.fail('Could not load the criteria', e) });
  }

  get activeCriteria(): CriterionDto[] {
    return this.criteria.filter((c) => c.isActive);
  }

  newCriterion(): void {
    this.criterionForm = { id: null, name: '', code: '', description: '', isActive: true };
    this.error = '';
  }

  editCriterion(c: CriterionDto): void {
    this.criterionForm = { id: c.id, name: c.name, code: c.code, description: c.description ?? '', isActive: c.isActive };
    this.error = '';
  }

  saveCriterion(): void {
    const f = this.criterionForm;
    if (!f) return;
    if (!f.name.trim()) {
      this.error = 'Name is required.';
      return;
    }
    const body = { name: f.name.trim(), code: f.code.trim() || undefined, description: f.description.trim(), isActive: f.isActive };
    this.run(f.id ? this.api.updateCriterion(f.id, body) : this.api.createCriterion(body), 'Criterion saved.', () => {
      this.criterionForm = null;
      this.loadCriteria();
    });
  }

  deleteCriterion(c: CriterionDto): void {
    if (!confirm(`Delete the criterion "${c.name}"? Existing template versions keep their copy.`)) return;
    this.run(this.api.deleteCriterion(c.id), 'Criterion deleted.', () => this.loadCriteria());
  }

  // ---------------------------------------------------------------- templates

  private loadTemplates(): void {
    this.api.templates().subscribe({ next: (t) => (this.templates = t), error: (e) => this.fail('Could not load the templates', e) });
  }

  /** Retired templates are hidden unless the "Show retired" toggle is on. */
  get visibleTemplates(): TemplateDto[] {
    return this.showRetired ? this.templates : this.templates.filter((t) => t.isActive);
  }

  newTemplate(): void {
    this.templateForm = { id: null, version: null, name: '', industry: '', description: '', isDefault: false, criteria: [], rows: [{ criterionId: '', weight: null }] };
    this.versions = [];
    this.error = '';
  }

  editTemplate(t: TemplateDto): void {
    this.templateForm = {
      id: t.id,
      version: t.version,
      name: t.name,
      industry: t.industry ?? '',
      description: t.description ?? '',
      isDefault: t.isDefault,
      criteria: [],
      rows: t.criteria.map((c) => ({ criterionId: c.criterionId, weight: c.weight }))
    };
    this.error = '';
    this.versionsOf = t.name;
    this.api.templateVersions(t.id).subscribe({ next: (v) => (this.versions = v), error: () => (this.versions = []) });
  }

  addRow(): void {
    this.templateForm?.rows.push({ criterionId: '', weight: null });
  }

  removeRow(i: number): void {
    this.templateForm?.rows.splice(i, 1);
  }

  get totalWeight(): number {
    return Math.round((this.templateForm?.rows.reduce((s, r) => s + (Number(r.weight) || 0), 0) ?? 0) * 100) / 100;
  }

  usable(row: { criterionId: string }): CriterionDto[] {
    const taken = new Set((this.templateForm?.rows ?? []).filter((r) => r !== row).map((r) => r.criterionId));
    return this.activeCriteria.filter((c) => !taken.has(c.id));
  }

  saveTemplate(): void {
    const f = this.templateForm;
    if (!f) return;
    if (!f.name.trim()) {
      this.error = 'Name is required.';
      return;
    }
    if (f.rows.some((r) => !r.criterionId || !r.weight || r.weight <= 0)) {
      this.error = 'Every row needs a criterion and a weight above 0.';
      return;
    }
    if (this.totalWeight !== 100) {
      this.error = `The weights must total exactly 100 (now ${this.totalWeight}).`;
      return;
    }
    const body: TemplateSave = {
      name: f.name.trim(),
      industry: f.industry.trim(),
      description: f.description.trim(),
      isDefault: f.isDefault,
      criteria: f.rows.map((r) => ({ criterionId: r.criterionId, weight: Number(r.weight) }))
    };
    this.run(f.id ? this.api.updateTemplate(f.id, body) : this.api.createTemplate(body), f.id ? 'Saved as a new version.' : 'Template created.', () => {
      this.templateForm = null;
      this.loadTemplates();
    });
  }

  retireTemplate(t: TemplateDto): void {
    if (!confirm(`Retire the template "${t.name}"? All its versions are kept for existing assessments.`)) return;
    this.run(this.api.retireTemplate(t.id), 'Template retired.', () => this.loadTemplates());
  }

  // ---------------------------------------------------------------- approval matrix / reassessment

  saveRule(r: RuleRow): void {
    r.approverRoles = r.rolesText.split(',').map((x) => x.trim()).filter(Boolean);
    this.run(this.api.saveApprovalRule(r), `${r.label} saved.`);
  }

  saveReview(r: ReassessmentRuleDto & { months: number | null }): void {
    if (!r.months || r.months < 1 || r.months > 120) {
      this.error = 'The review interval must be between 1 and 120 months.';
      return;
    }
    this.run(this.api.saveReassessmentRule(r.levelName, r.months), `${r.levelName} review interval saved.`);
  }

  // ---------------------------------------------------------------- helpers

  private run<T>(call: import('rxjs').Observable<T>, done: string, after?: () => void): void {
    this.busy = true;
    this.error = '';
    call.subscribe({
      next: () => {
        this.busy = false;
        this.notify(done);
        after?.();
      },
      error: (e) => {
        this.busy = false;
        this.error = apiErrorMessage(e);
      }
    });
  }

  private fail(message: string, err: unknown): void {
    this.error = `${message}: ${apiErrorMessage(err)}`;
  }

  private notify(message: string): void {
    // shared toast: durations, hover-pause and close button live in shared-ui (toast.ts)
    pushToast(message);
  }
}
