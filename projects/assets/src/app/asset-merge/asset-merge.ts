import { Component, HostListener, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
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
import { ASSET_RECORDS, ASSET_TYPE_ICONS, AssetIconKey, AssetRecord, AssetStatus, assetLocation } from 'shared-ui';
import DEDUP_DATA from './dedup-data.json';
import { Comparable, DEFAULT_RULES, DedupRules, MATCH_FIELDS, MatchField, confidence, matchScore } from './dedup-match';

type Confidence = 'High' | 'Medium' | 'Low';
type MergeField = 'name' | 'type' | 'model' | 'serialNumber' | 'category' | 'location' | 'status' | 'purchaseDate' | 'supplier' | 'value';

/** Purchase and audit details not held in the asset register. */
interface AssetExtras {
  serialNumber: string;
  purchaseDate: string;
  supplier: string;
  value: number;
  lastModified: string;
  modifiedBy: string;
}

/** A record from another source (legacy import, finance register...) that may duplicate a register asset. */
interface CandidateRecord extends AssetExtras {
  tag: string;
  name: string;
  type: string;
  manufacturer: string;
  model: string;
  category: string;
  location: string;
  status: string;
  source: string;
}

/** One record as compared and merged on this page. */
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

const CURRENT_USER = 'Rex Kumar';
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

/** DEMO MODE: sample records and merge state kept in memory until the Assets API is wired up. */
const DATA = DEDUP_DATA as { extras: Record<string, AssetExtras>; candidates: CandidateRecord[] };
const EXTRAS: Record<string, AssetExtras> = structuredClone(DATA.extras);
let POOL: CandidateRecord[] = structuredClone(DATA.candidates);
let RULES: DedupRules = { ...DEFAULT_RULES, fields: [...DEFAULT_RULES.fields] };
/** Pairs marked "not a duplicate" ("TAG-A|TAG-B", sorted). */
const DISMISSED = new Set<string>();
const MERGE_LOG: MergeLog[] = [];

const pad = (n: number) => String(n).padStart(2, '0');
const pairKey = (a: string, b: string) => [a, b].sort().join('|');

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

function registerRecord(a: AssetRecord): DedupRecord {
  const x = EXTRAS[a.tag];
  return {
    tag: a.tag, name: a.name, type: a.type, manufacturer: a.manufacturer, model: a.model, serialNumber: x?.serialNumber ?? a.serialNumber ?? '',
    category: a.category, location: assetLocation(a), status: a.status, purchaseDate: x?.purchaseDate ?? a.purchaseDate ?? '',
    supplier: x?.supplier ?? '', value: x?.value ?? 0, lastModified: x?.lastModified ?? '', modifiedBy: x?.modifiedBy ?? '',
    source: 'Asset List', inRegister: true, image: a.image, icon: a.icon,
  };
}

function candidateRecord(c: CandidateRecord): DedupRecord {
  // Shown with the picture of a register asset of the same type
  const like = ASSET_RECORDS.find((a) => a.type === c.type) ?? ASSET_RECORDS.find((a) => a.category === c.category);
  return { ...c, inRegister: false, image: like?.image ?? '', icon: like?.icon ?? 'transformer' };
}

/**
 * Groups possible duplicates. Each record joins the group of the register asset it matches best;
 * register assets are never chained to each other through a third record.
 */
function findGroups(rules: DedupRules): DuplicateGroup[] {
  const active = (r: DedupRecord) => rules.includeInactive || r.status !== 'Disposed';
  const register = ASSET_RECORDS.map(registerRecord).filter(active);
  const pool = POOL.map(candidateRecord).filter(active);
  const score = (a: DedupRecord, b: DedupRecord) => (DISMISSED.has(pairKey(a.tag, b.tag)) ? 0 : matchScore(a, b, rules));
  const groups = new Map<string, DuplicateGroup>();
  const member = new Set<string>();
  const join = (primary: DedupRecord, rec: DedupRecord, s: number) => {
    let g = groups.get(primary.tag);
    if (!g) groups.set(primary.tag, (g = { primary, members: [], maxScore: 0 }));
    g.members.push({ rec, score: s });
    g.maxScore = Math.max(g.maxScore, s);
    member.add(rec.tag);
  };

  // 1. Records from other sources join their best-matching register asset
  const unplaced: DedupRecord[] = [];
  for (const c of pool) {
    let best: DedupRecord | null = null;
    let bestScore = 0;
    for (const r of register) {
      const s = score(c, r);
      if (s >= rules.low && s > bestScore) [best, bestScore] = [r, s];
    }
    if (best) join(best, c, bestScore);
    else unplaced.push(c);
  }
  // 2. Duplicates within the register: the older asset (lower id) is kept
  for (let i = 0; i < register.length; i++) {
    for (let j = i + 1; j < register.length; j++) {
      const [a, b] = [register[i], register[j]];
      if (member.has(a.tag) || member.has(b.tag) || groups.has(b.tag)) continue;
      const s = score(a, b);
      if (s >= rules.low) join(a, b, s);
    }
  }
  // 3. Records that match only each other: the first one is kept
  for (let i = 0; i < unplaced.length; i++) {
    const a = unplaced[i];
    if (member.has(a.tag)) continue;
    for (let j = i + 1; j < unplaced.length; j++) {
      const b = unplaced[j];
      if (member.has(b.tag) || groups.has(b.tag)) continue;
      const s = score(a, b);
      if (s >= rules.low) join(a, b, s);
    }
  }
  for (const g of groups.values()) g.members.sort((x, y) => y.score - x.score);
  return [...groups.values()].sort((x, y) => y.maxScore - x.maxScore);
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

  rules: DedupRules = RULES;
  groups: DuplicateGroup[] = findGroups(RULES);
  scanning = false;
  lastScan = nowLocal();

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
  private scanTimer?: ReturnType<typeof setTimeout>;

  constructor() {
    if (this.groups.length) this.review(this.groups[0]);
  }

  ngOnDestroy(): void {
    clearTimeout(this.toastTimer);
    clearTimeout(this.scanTimer);
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
    this.scanTimer = setTimeout(() => {
      this.rescan();
      this.scanning = false;
      const n = this.allMembers.length;
      this.showToast(n ? `Scan complete: ${n} potential duplicate${n > 1 ? 's' : ''} in ${this.groups.length} group${this.groups.length > 1 ? 's' : ''}.` : 'Scan complete: no duplicates found.');
    }, 700);
  }

  private rescan(): void {
    this.groups = findGroups(this.rules);
    this.lastScan = nowLocal();
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
    groups.forEach((g) => g.members.forEach((m) => DISMISSED.add(pairKey(g.primary.tag, m.rec.tag))));
    tags.forEach((t) => this.checked.delete(t));
    this.rescan();
    this.showToast(groups.length === 1 ? `${groups[0].primary.tag} marked as not a duplicate.` : `${groups.length} groups marked as not duplicates.`);
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

  /** Applies the merge: the kept record takes the chosen values; the others are removed. */
  merge(): void {
    this.confirmOpen = false;
    const recs = this.cards;
    const keep = recs[0];
    const pick = (f: MergeField) => {
      const r = this.record(this.choice[f] ?? '');
      return r ? this.value(r, f) : '';
    };
    const [site, ...area] = pick('location').split(' - ');
    const removedRegister = recs.slice(1).filter((r) => r.inRegister);
    let target = ASSET_RECORDS.find((a) => a.tag === keep.tag);
    if (!target) {
      // Keeping a record from another source: it joins the register in place of the register duplicates
      const like = removedRegister[0] ? ASSET_RECORDS.find((a) => a.tag === removedRegister[0].tag) : undefined;
      target = {
        id: Math.max(0, ...ASSET_RECORDS.map((a) => a.id)) + 1, tag: keep.tag, name: '', type: '', category: '', site: '', area: '', custodian: like?.custodian ?? '',
        department: like?.department ?? '', status: 'Active', criticality: like?.criticality ?? 'Medium', manufacturer: keep.manufacturer,
        model: '', nextMaintenance: like?.nextMaintenance ?? null, icon: like?.icon ?? keep.icon, image: like?.image ?? keep.image,
      };
      ASSET_RECORDS.push(target);
    }
    Object.assign(target, {
      name: pick('name') || target.name, type: pick('type') || target.type, model: pick('model') || target.model,
      category: pick('category') || target.category, site: site || target.site, area: area.join(' - ') || target.area,
      status: (pick('status') as AssetStatus) || target.status,
    });
    if (pick('serialNumber')) target.serialNumber = pick('serialNumber');
    if (isoDate(pick('purchaseDate'))) target.purchaseDate = isoDate(pick('purchaseDate'));
    EXTRAS[target.tag] = {
      serialNumber: pick('serialNumber'), purchaseDate: isoDate(pick('purchaseDate')), supplier: pick('supplier'),
      value: Number(pick('value')) || 0, lastModified: nowLocal().slice(0, 10), modifiedBy: CURRENT_USER,
    };
    const gone = new Set(recs.slice(1).map((r) => r.tag));
    for (let i = ASSET_RECORDS.length - 1; i >= 0; i--) if (gone.has(ASSET_RECORDS[i].tag)) ASSET_RECORDS.splice(i, 1);
    POOL = POOL.filter((c) => !gone.has(c.tag) && c.tag !== keep.tag);
    MERGE_LOG.unshift({ primary: keep.tag, merged: [...gone], notes: this.notes.trim(), at: nowLocal(), by: CURRENT_USER });
    this.reviewKey = null;
    this.rescan();
    this.showToast(`${gone.size} record${gone.size > 1 ? 's' : ''} merged into ${keep.tag}.`);
  }

  get mergeLog(): MergeLog[] {
    return MERGE_LOG;
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
    RULES = this.rules = { ...f, high, medium, low };
    this.rulesForm = null;
    this.rescan();
    this.showToast(`Rules saved. ${this.allMembers.length} potential duplicates found.`);
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
