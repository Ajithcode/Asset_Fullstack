import { Component, DestroyRef, HostListener, OnDestroy, inject } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { forkJoin } from 'rxjs';
import {
  LucideAngularModule,
  ArrowRight,
  ChevronLeft,
  ChevronRight,
  Database,
  EllipsisVertical,
  Eye,
  Filter,
  GitMerge,
  Info,
  Link2,
  Pencil,
  RefreshCw,
  Search,
  Settings,
  ShieldAlert,
  Star,
  TriangleAlert,
  CircleDot,
  X,
} from 'lucide-angular';
import { ASSET_RECORDS, ASSET_TYPE_ICONS, AssetIconKey } from 'shared-ui';
import { Comparable, DEFAULT_RULES, DedupRules, MATCH_FIELDS, MatchField, confidence } from './dedup-match';
import { AssetDedupService, AssetMergeDto, DedupGroupDto, DedupRecordDto, MergeField, dedupErrorMessage } from './asset-dedup.service';

type Confidence = 'High' | 'Medium' | 'Low';

/** One record as compared and merged on this page (an API record plus its picture). */
interface DedupRecord extends Comparable {
  tag: string;
  type: string;
  purchaseDate: string;
  supplier: string;
  value: number;
  lastModified: string;
  modifiedBy: string;
  /** 'Asset List' for register assets, otherwise where the record came from. */
  source: string;
  inRegister: boolean;
  image: string;
  icon: AssetIconKey;
}

interface Member {
  rec: DedupRecord;
  score: number;
}

interface DuplicateGroup {
  primary: DedupRecord;
  members: Member[];
  maxScore: number;
}

interface MergeLog {
  primary: string;
  merged: string[];
  notes: string;
  at: string;
  by: string;
}

const MERGE_FIELDS: { key: MergeField; label: string }[] = [
  { key: 'name', label: 'Asset Name' },
  { key: 'type', label: 'Asset Type' },
  { key: 'model', label: 'Model' },
  { key: 'serialNumber', label: 'Serial Number' },
  { key: 'category', label: 'Category' },
  { key: 'location', label: 'Location' },
  { key: 'status', label: 'Status' },
  { key: 'purchaseDate', label: 'Purchase Date' },
  { key: 'supplier', label: 'Supplier' },
  { key: 'value', label: 'Asset Value' },
];
const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

const pad = (n: number) => String(n).padStart(2, '0');

function nowLocal(): string {
  const d = new Date();
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

/** yyyy-mm-dd or dd/mm/yyyy → yyyy-mm-dd ('' when neither). */
function isoDate(v: string): string {
  if (/^\d{4}-\d{2}-\d{2}/.test(v)) return v.slice(0, 10);
  const m = /^(\d{1,2})[/.-](\d{1,2})[/.-](\d{4})$/.exec(v);
  return m ? `${m[3]}-${pad(Number(m[2]))}-${pad(Number(m[1]))}` : '';
}

/** API record → page record; the picture is that of a sample asset of the same type (or category). */
function toRecord(r: DedupRecordDto): DedupRecord {
  const like = ASSET_RECORDS.find((a) => a.type === r.type) ?? ASSET_RECORDS.find((a) => a.category === r.category);
  return {
    tag: r.tag, name: r.name, type: r.type, manufacturer: r.manufacturer, model: r.model, serialNumber: r.serialNumber, category: r.category,
    location: r.location, status: r.status, purchaseDate: r.purchaseDate, supplier: r.supplier, value: r.value, lastModified: r.lastModified,
    modifiedBy: r.modifiedBy, source: r.source, inRegister: r.inRegister, image: like?.image ?? '', icon: like?.icon ?? 'transformer',
  };
}

function toGroup(g: DedupGroupDto): DuplicateGroup {
  return { primary: toRecord(g.primary), members: g.members.map((m) => ({ rec: toRecord(m.record), score: m.score })), maxScore: g.maxScore };
}

function toLog(m: AssetMergeDto): MergeLog {
  return { primary: m.primaryTag, merged: m.mergedTags, notes: m.notes ?? '', at: m.mergedAt ?? '', by: m.mergedBy ?? '' };
}

@Component({
  selector: 'app-asset-merge',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule],
  templateUrl: './asset-merge.html',
  // Split to keep each stylesheet under the anyComponentStyle budget
  styleUrls: ['./asset-merge.css', './asset-merge-panels.css'],
})
export class AssetMerge implements OnDestroy {
  readonly icons = {
    ArrowRight, ChevronLeft, ChevronRight, CircleDot, Database, EllipsisVertical, Eye, Filter, GitMerge, Info, Link2, Pencil, RefreshCw,
    Search, Settings, ShieldAlert, Star, TriangleAlert, X,
  };
  readonly typeIcons = ASSET_TYPE_ICONS;
  readonly mergeFields = MERGE_FIELDS;
  readonly matchFields = MATCH_FIELDS;
  readonly pageSizes = [5, 10, 20];

  rules: DedupRules = { ...DEFAULT_RULES, fields: [...DEFAULT_RULES.fields] };
  groups: DuplicateGroup[] = [];
  merges: MergeLog[] = [];
  loading = true;
  loadError = '';
  scanning = false;
  /** A merge, dismissal or rules save in progress. */
  saving = false;
  lastScan: string | null = null;

  // ===== Table =====
  level: Confidence | '' = '';
  search = '';
  categoryFilter = '';
  siteFilter = '';
  statusFilter = '';
  pageSize = 5;
  page = 1;
  readonly checked = new Set<string>();
  tableMenuOpen = false;
  rowMenuTag: string | null = null;

  // ===== Merge panel =====
  /** Group under review (its primary tag when the review started). */
  reviewKey: string | null = null;
  primaryTag = '';
  /** Records taking part in the merge (primary first). */
  mergeTags: string[] = [];
  /** Which record each merged value comes from. */
  choice: Partial<Record<MergeField, string>> = {};
  notes = '';
  confirmOpen = false;

  // ===== Rules dialog =====
  rulesForm: DedupRules | null = null;
  rulesError = '';

  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

  private readonly api = inject(AssetDedupService);
  private readonly destroyRef = inject(DestroyRef);

  constructor() {
    this.load();
  }

  /** Rules, duplicate groups and the merge log, from the Asset service. */
  load(): void {
    this.loading = true;
    this.loadError = '';
    forkJoin({ rules: this.api.getRules(), groups: this.api.getGroups(), merges: this.api.getMerges() })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ rules, groups, merges }) => {
          this.rules = { fields: rules.fields as MatchField[], high: rules.high, medium: rules.medium, low: rules.low, includeInactive: rules.includeInactive };
          this.lastScan = groups.lastScanAt ?? rules.lastScanAt;
          this.merges = merges.items.map(toLog);
          this.setGroups(groups.items.map(toGroup));
          this.loading = false;
        },
        error: (err) => {
          this.loading = false;
          this.loadError = dedupErrorMessage(err);
        },
      });
  }

  /** Reloads the groups (after a scan, dismissal, merge or rules change). */
  private refresh(after?: () => void): void {
    forkJoin({ groups: this.api.getGroups(), merges: this.api.getMerges() })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: ({ groups, merges }) => {
          this.lastScan = groups.lastScanAt ?? this.lastScan;
          this.merges = merges.items.map(toLog);
          this.setGroups(groups.items.map(toGroup));
          after?.();
        },
        error: (err) => this.showToast(dedupErrorMessage(err)),
      });
  }

  ngOnDestroy(): void {
    clearTimeout(this.toastTimer);
  }

  // ===== KPIs =====

  confidenceOf(score: number): Confidence | null {
    return confidence(score, this.rules);
  }

  get allMembers(): Member[] {
    return this.groups.flatMap((g) => g.members);
  }

  memberCount(level: Confidence): number {
    return this.allMembers.filter((m) => this.confidenceOf(m.score) === level).length;
  }

  groupCount(level: Confidence | ''): number {
    return level ? this.groups.filter((g) => this.confidenceOf(g.maxScore) === level).length : this.groups.length;
  }

  get matchFieldLabels(): string {
    return MATCH_FIELDS.filter((f) => this.rules.fields.includes(f.key)).map((f) => f.label).join(', ');
  }

  scan(): void {
    if (this.scanning) return;
    this.scanning = true;
    this.api.scan().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (res) => {
        this.lastScan = res.scannedAt;
        this.refresh(() => {
          this.scanning = false;
          const n = res.duplicates;
          this.showToast(n ? `Scan complete: ${n} potential duplicate${n > 1 ? 's' : ''} in ${res.groups} group${res.groups > 1 ? 's' : ''}.` : 'Scan complete: no duplicates found.');
        });
      },
      error: (err) => {
        this.scanning = false;
        this.showToast(dedupErrorMessage(err));
      },
    });
  }

  /** Shows new groups, keeping the group under review (and the checked rows) when they are still there. */
  private setGroups(groups: DuplicateGroup[]): void {
    this.groups = groups;
    const keep = this.groups.find((g) => g.primary.tag === this.reviewKey);
    if (keep) this.review(keep);
    else if (this.groups.length) this.review(this.groups[0]);
    else this.reviewKey = null;
    this.page = Math.min(this.page, this.pageCount);
    [...this.checked].forEach((t) => !this.groups.some((g) => g.primary.tag === t) && this.checked.delete(t));
  }

  // ===== Table =====

  locationSite(location: string): string {
    return location.split(' - ')[0];
  }

  get categories(): string[] {
    return [...new Set(this.groups.map((g) => g.primary.category))].sort();
  }

  get sites(): string[] {
    return [...new Set(this.groups.map((g) => this.locationSite(g.primary.location)))].sort();
  }

  get statuses(): string[] {
    return [...new Set(this.groups.map((g) => g.primary.status))].sort();
  }

  get filtered(): DuplicateGroup[] {
    const q = this.search.trim().toLowerCase();
    return this.groups.filter(
      (g) =>
        (!this.level || this.confidenceOf(g.maxScore) === this.level) &&
        (!this.categoryFilter || g.primary.category === this.categoryFilter) &&
        (!this.siteFilter || this.locationSite(g.primary.location) === this.siteFilter) &&
        (!this.statusFilter || g.primary.status === this.statusFilter) &&
        (!q || [g.primary, ...g.members.map((m) => m.rec)].some((r) => `${r.tag} ${r.name} ${r.serialNumber} ${r.model}`.toLowerCase().includes(q))),
    );
  }

  get pageCount(): number {
    return Math.max(1, Math.ceil(this.filtered.length / this.pageSize));
  }

  get safePage(): number {
    return Math.min(this.page, this.pageCount);
  }

  get pageNumbers(): number[] {
    return Array.from({ length: this.pageCount }, (_, i) => i + 1);
  }

  get pageRows(): DuplicateGroup[] {
    const start = (this.safePage - 1) * this.pageSize;
    return this.filtered.slice(start, start + this.pageSize);
  }

  get rangeLabel(): string {
    const total = this.filtered.length;
    if (!total) return 'No records';
    const start = (this.safePage - 1) * this.pageSize + 1;
    return `Showing ${start} - ${Math.min(start + this.pageSize - 1, total)} of ${total} records`;
  }

  rowNumber(i: number): number {
    return (this.safePage - 1) * this.pageSize + i + 1;
  }

  goToPage(p: number): void {
    this.page = Math.min(Math.max(1, p), this.pageCount);
  }

  setLevel(level: Confidence | ''): void {
    this.level = level;
    this.page = 1;
  }

  onFilterChange(): void {
    this.page = 1;
  }

  scoreTone(score: number): string {
    const c = this.confidenceOf(score);
    return c === 'High' ? 'high' : c === 'Medium' ? 'medium' : 'low';
  }

  get allPageChecked(): boolean {
    return this.pageRows.length > 0 && this.pageRows.every((g) => this.checked.has(g.primary.tag));
  }

  toggleAll(on: boolean): void {
    this.pageRows.forEach((g) => (on ? this.checked.add(g.primary.tag) : this.checked.delete(g.primary.tag)));
  }

  toggleRow(tag: string, on: boolean): void {
    if (on) this.checked.add(tag);
    else this.checked.delete(tag);
  }

  toggleTableMenu(event: Event): void {
    event.stopPropagation();
    this.rowMenuTag = null;
    this.tableMenuOpen = !this.tableMenuOpen;
  }

  toggleRowMenu(event: Event, tag: string): void {
    event.stopPropagation();
    this.tableMenuOpen = false;
    this.rowMenuTag = this.rowMenuTag === tag ? null : tag;
  }

  /** Marks every pair in the groups as "not a duplicate" so they stop appearing. */
  dismiss(tags: string[]): void {
    this.closeMenus();
    const groups = this.groups.filter((g) => tags.includes(g.primary.tag));
    if (!groups.length) {
      this.showToast('Select records first.');
      return;
    }
    if (this.saving) return;
    this.saving = true;
    forkJoin(groups.map((g) => this.api.dismiss([g.primary.tag, ...g.members.map((m) => m.rec.tag)])))
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          tags.forEach((t) => this.checked.delete(t));
          this.refresh(() => {
            this.saving = false;
            this.showToast(groups.length === 1 ? `${groups[0].primary.tag} marked as not a duplicate.` : `${groups.length} groups marked as not duplicates.`);
          });
        },
        error: (err) => {
          this.saving = false;
          this.showToast(dedupErrorMessage(err));
        },
      });
  }

  dismissSelected(): void {
    this.dismiss([...this.checked]);
  }

  exportCsv(): void {
    this.closeMenus();
    const rows = [['Primary Asset ID', 'Primary Asset Name', 'Duplicate Asset ID', 'Duplicate Asset Name', 'Source', 'Match Score', 'Confidence']];
    for (const g of this.filtered) for (const m of g.members) rows.push([g.primary.tag, g.primary.name, m.rec.tag, m.rec.name, m.rec.source, String(m.score), this.confidenceOf(m.score) ?? '']);
    const csv = rows.map((r) => r.map((v) => `"${v.replace(/"/g, '""')}"`).join(',')).join('\r\n');
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv' }));
    const a = document.createElement('a');
    a.href = url;
    a.download = 'duplicate-assets.csv';
    a.click();
    URL.revokeObjectURL(url);
  }

  // ===== Merge panel =====

  review(g: DuplicateGroup): void {
    this.closeMenus();
    this.reviewKey = g.primary.tag;
    this.primaryTag = g.primary.tag;
    this.mergeTags = [g.primary.tag, ...g.members.map((m) => m.rec.tag)];
    this.notes = '';
    this.resetChoices();
  }

  openReview(g: DuplicateGroup): void {
    this.review(g);
    document.getElementById('merge-panel')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
  }

  get reviewGroup(): DuplicateGroup | undefined {
    return this.groups.find((g) => g.primary.tag === this.reviewKey);
  }

  private get groupRecords(): DedupRecord[] {
    const g = this.reviewGroup;
    return g ? [g.primary, ...g.members.map((m) => m.rec)] : [];
  }

  record(tag: string): DedupRecord | undefined {
    return this.groupRecords.find((r) => r.tag === tag);
  }

  /** Cards in order: the record kept first, then the ones merged into it. */
  get cards(): DedupRecord[] {
    const recs = this.mergeTags.map((t) => this.record(t)).filter((r): r is DedupRecord => !!r);
    return [...recs.filter((r) => r.tag === this.primaryTag), ...recs.filter((r) => r.tag !== this.primaryTag)];
  }

  scoreOf(tag: string): number | null {
    return this.reviewGroup?.members.find((m) => m.rec.tag === tag)?.score ?? null;
  }

  value(rec: DedupRecord, f: MergeField): string {
    return f === 'value' ? (rec.value ? String(rec.value) : '') : rec[f];
  }

  display(rec: DedupRecord, f: MergeField): string {
    const v = this.value(rec, f);
    if (!v) return '-';
    if (f === 'value') return `AED ${Number(v).toLocaleString('en-US')}`;
    if (f === 'purchaseDate' && /^\d{4}-\d{2}-\d{2}$/.test(v)) return `${v.slice(8, 10)} ${MONTHS[Number(v.slice(5, 7)) - 1]} ${v.slice(0, 4)}`;
    return v;
  }

  /** True when a duplicate's value disagrees with the record being kept. */
  differs(rec: DedupRecord, f: MergeField): boolean {
    if (rec.tag === this.primaryTag) return false;
    const primary = this.record(this.primaryTag);
    if (!primary) return false;
    const a = this.value(rec, f);
    const b = this.value(primary, f);
    if (!a || !b) return false;
    if (f === 'purchaseDate') return isoDate(a) !== isoDate(b);
    return a.toLowerCase().replace(/[^a-z0-9]/g, '') !== b.toLowerCase().replace(/[^a-z0-9]/g, '');
  }

  /** Default values: the kept record's, filling its gaps from the duplicates. */
  resetChoices(): void {
    const choice: Partial<Record<MergeField, string>> = {};
    for (const f of MERGE_FIELDS) {
      const from = this.cards.find((r) => this.value(r, f.key));
      if (from) choice[f.key] = from.tag;
    }
    this.choice = choice;
  }

  use(rec: DedupRecord, f: MergeField): void {
    if (this.value(rec, f)) this.choice = { ...this.choice, [f]: rec.tag };
  }

  isChosen(rec: DedupRecord, f: MergeField): boolean {
    return this.choice[f] === rec.tag;
  }

  merged(f: MergeField): string {
    const rec = this.record(this.choice[f] ?? '');
    return rec ? this.display(rec, f) : '-';
  }

  makePrimary(tag: string): void {
    this.primaryTag = tag;
    this.resetChoices();
  }

  removeFromMerge(tag: string): void {
    this.mergeTags = this.mergeTags.filter((t) => t !== tag);
    for (const f of MERGE_FIELDS) if (this.choice[f.key] === tag) delete this.choice[f.key];
    this.choice = { ...this.choice };
    if (this.mergeTags.length < 2) this.showToast('Only one record is left; add records back with Cancel.');
  }

  cancelMerge(): void {
    const g = this.reviewGroup;
    if (g) this.review(g);
  }

  get mergeCount(): number {
    return this.mergeTags.length - 1;
  }

  get nowLabel(): string {
    const n = nowLocal();
    return `${this.display({ purchaseDate: n.slice(0, 10) } as DedupRecord, 'purchaseDate')} ${n.slice(11)}`;
  }

  askMerge(): void {
    if (this.mergeCount < 1) {
      this.showToast('Keep at least one duplicate in the merge.');
      return;
    }
    if (this.notes.length > 500) return;
    this.confirmOpen = true;
  }

  /**
   * Applies the merge on the server: the kept record takes the chosen values (each field names the record it comes
   * from) and the merged records are retired from the register.
   */
  merge(): void {
    this.confirmOpen = false;
    if (this.saving) return;
    const recs = this.cards;
    const keep = recs[0];
    const mergedTags = recs.slice(1).map((r) => r.tag);
    const inMerge = new Set(recs.map((r) => r.tag));
    const fieldSources: Partial<Record<MergeField, string>> = {};
    for (const f of MERGE_FIELDS) {
      const tag = this.choice[f.key];
      if (tag && inMerge.has(tag)) fieldSources[f.key] = tag;
    }
    this.saving = true;
    this.api
      .merge({ primaryTag: keep.tag, mergedTags, fieldSources, notes: this.notes.trim() })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: () => {
          this.reviewKey = null;
          this.refresh(() => {
            this.saving = false;
            this.showToast(`${mergedTags.length} record${mergedTags.length > 1 ? 's' : ''} merged into ${keep.tag}.`);
          });
        },
        error: (err) => {
          this.saving = false;
          this.showToast(dedupErrorMessage(err));
        },
      });
  }

  get mergeLog(): MergeLog[] {
    return this.merges;
  }

  // ===== Rules =====

  openRules(): void {
    this.closeMenus();
    this.rulesError = '';
    this.rulesForm = { ...this.rules, fields: [...this.rules.fields] };
  }

  toggleRuleField(f: MatchField, on: boolean): void {
    const form = this.rulesForm;
    if (!form) return;
    form.fields = on ? [...form.fields, f] : form.fields.filter((x) => x !== f);
  }

  saveRules(): void {
    const f = this.rulesForm;
    if (!f) return;
    const [high, medium, low] = [Number(f.high), Number(f.medium), Number(f.low)];
    if (!f.fields.length) {
      this.rulesError = 'Choose at least one matching field.';
      return;
    }
    if (!(high <= 100 && high > medium && medium > low && low >= 1)) {
      this.rulesError = 'Thresholds must go High > Medium > Low, between 1 and 100.';
      return;
    }
    if (this.saving) return;
    this.saving = true;
    this.api
      .saveRules({ ...f, high, medium, low })
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: (saved) => {
          this.rules = { fields: saved.fields as MatchField[], high: saved.high, medium: saved.medium, low: saved.low, includeInactive: saved.includeInactive };
          this.rulesForm = null;
          this.refresh(() => {
            this.saving = false;
            this.showToast(`Rules saved. ${this.allMembers.length} potential duplicates found.`);
          });
        },
        error: (err) => {
          this.saving = false;
          this.rulesError = dedupErrorMessage(err);
        },
      });
  }

  // ===== Misc =====

  @HostListener('document:click')
  closeMenus(): void {
    this.tableMenuOpen = false;
    this.rowMenuTag = null;
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closeMenus();
    this.rulesForm = null;
    this.confirmOpen = false;
  }

  private showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 3500);
  }
}
