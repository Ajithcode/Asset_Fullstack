import { Component, HostListener, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import * as XLSX from 'xlsx';
import {
  LucideAngularModule,
  ArrowDown,
  ArrowLeftRight,
  ArrowUp,
  Ban,
  Barcode,
  Bluetooth,
  Box,
  Calendar,
  ChevronLeft,
  ChevronRight,
  CircleCheck,
  Clock,
  Download,
  Ellipsis,
  ExternalLink,
  FileText,
  Info,
  Layers,
  MapPin,
  Plus,
  QrCode,
  Radio,
  Search,
  SlidersHorizontal,
  Tag,
  Upload,
  X,
} from 'lucide-angular';
import { DEFAULT_TYPE, LinkedAsset, lastActivity, PREFIX, SITES, STATUSES, TAGS, TECHS, TYPES, TagId, TagStatus, TagType, Tech, codeFor, knownAssets, nextNumber, readerFor, tagIdOf } from './tag-data';

type SortKey = 'lastSeen' | 'id';
type Dialog =
  | { kind: 'register'; tech: Tech; type: TagType; number: string; code: string; site: string; location: string; asset: string }
  | { kind: 'assign'; tagId: string; asset: string; site: string; location: string; note: string }
  | { kind: 'unassign'; tagId: string; reason: string }
  | { kind: 'status'; tagId: string; to: 'Inactive' | 'Available'; reason: string }
  | { kind: 'import'; rows: ImportRow[]; fileName: string; error: string };

interface ImportRow {
  line: number;
  tech: Tech | null;
  id: string;
  type: TagType;
  code: string;
  site: string;
  location: string;
  asset: string;
  error: string;
}

const CURRENT_USER = 'John Doe';
const MONTHS = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];
const TECH_ALIASES: Record<string, Tech> = { barcode: 'Barcode', qr: 'QR Code', 'qr code': 'QR Code', rfid: 'Passive RFID', 'passive rfid': 'Passive RFID', ble: 'BLE', beacon: 'BLE' };

@Component({
  selector: 'app-tag-ids',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule],
  templateUrl: './tag-ids.html',
  styleUrls: ['./tag-ids.css', './tag-ids-panel.css'],
})
export class TagIds implements OnDestroy {
  readonly icons = { ArrowDown, ArrowLeftRight, ArrowUp, Ban, Barcode, Bluetooth, Box, Calendar, ChevronLeft, ChevronRight, CircleCheck, Clock, Download, Ellipsis, ExternalLink, FileText, Info, Layers, MapPin, Plus, QrCode, Radio, Search, SlidersHorizontal, Tag, Upload, X };
  readonly techs = TECHS;
  readonly types = TYPES;
  readonly statuses = STATUSES;
  readonly sites = SITES;
  readonly pageSizes = [10, 25, 50, 100];

  search = '';
  techFilter: Tech | '' = '';
  statusFilter: TagStatus | '' = '';
  siteFilter = '';
  typeFilter: TagType | '' = '';
  seenFilter: '' | '1' | '7' | '30' | 'never' = '';
  moreOpen = false;
  sortKey: SortKey = 'lastSeen';
  sortDesc = true;
  pageSize = 10;
  page = 0;

  /** Wide screens open with a tag selected; narrower ones keep the drawer closed until a row is clicked. */
  selectedId: string | null = typeof window !== 'undefined' && window.innerWidth >= 1200 ? 'RFID-009183' : null;
  panelTab: 'details' | 'activity' = 'details';
  menuOpen = '';
  dialog: Dialog | null = null;
  formError = '';
  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;
  /** Bumped on every change so the cached rows recompute. */
  private version = 0;
  private cacheKey = '';
  private cacheRows: TagId[] = [];

  constructor(private router: Router) {}

  ngOnDestroy(): void {
    clearTimeout(this.toastTimer);
  }

  // ===== KPIs =====

  get counts(): Record<TagStatus | 'Total', number> {
    const c = { Total: TAGS.length, Assigned: 0, Available: 0, Inactive: 0 };
    for (const t of TAGS) c[t.status]++;
    return c;
  }

  setStatus(s: TagStatus | ''): void {
    this.statusFilter = this.statusFilter === s ? '' : s;
    this.onFilter();
  }

  // ===== Table =====

  get rows(): TagId[] {
    const key = JSON.stringify([this.search, this.techFilter, this.statusFilter, this.siteFilter, this.typeFilter, this.seenFilter, this.sortKey, this.sortDesc, this.version]);
    if (key === this.cacheKey) return this.cacheRows;
    const q = this.search.trim().toLowerCase();
    const now = Date.now();
    const seenSince = this.seenFilter && this.seenFilter !== 'never' ? now - Number(this.seenFilter) * 86_400_000 : 0;
    const rows = TAGS.filter((t) =>
      (!this.techFilter || t.tech === this.techFilter) && (!this.statusFilter || t.status === this.statusFilter) && (!this.siteFilter || t.site === this.siteFilter) &&
      (!this.typeFilter || t.type === this.typeFilter) &&
      (!this.seenFilter || (this.seenFilter === 'never' ? t.lastSeen === null : (t.lastSeen ?? 0) >= seenSince)) &&
      (!q || `${t.id} ${t.code} ${t.asset ? t.asset.kind + ' ' + t.asset.tag : ''} ${t.site} ${t.location}`.toLowerCase().includes(q)));
    const dir = this.sortDesc ? -1 : 1;
    rows.sort((a, b) => this.sortKey === 'id' ? dir * a.id.localeCompare(b.id) : dir * (lastActivity(a) - lastActivity(b)) || a.id.localeCompare(b.id));
    this.cacheKey = key;
    this.cacheRows = rows;
    return rows;
  }

  get pageRows(): TagId[] {
    return this.rows.slice(this.page * this.pageSize, (this.page + 1) * this.pageSize);
  }

  get pageCount(): number {
    return Math.max(1, Math.ceil(this.rows.length / this.pageSize));
  }

  /** Page buttons with gaps, e.g. 1 2 3 … 249. */
  get pages(): (number | null)[] {
    const n = this.pageCount;
    const p = this.page;
    const set = [...new Set([0, 1, p - 1, p, p + 1, n - 2, n - 1])].filter((x) => x >= 0 && x < n).sort((a, b) => a - b);
    const out: (number | null)[] = [];
    set.forEach((x, i) => {
      if (i && x - set[i - 1] > 1) out.push(null);
      out.push(x);
    });
    return out;
  }

  get showing(): string {
    const n = this.rows.length;
    return n ? `Showing ${(this.page * this.pageSize + 1).toLocaleString()}–${Math.min(n, (this.page + 1) * this.pageSize).toLocaleString()} of ${n.toLocaleString()}` : 'No tags';
  }

  get moreCount(): number {
    return (this.typeFilter ? 1 : 0) + (this.seenFilter ? 1 : 0);
  }

  onFilter(): void {
    this.page = 0;
  }

  sortBy(key: SortKey): void {
    if (this.sortKey === key) this.sortDesc = !this.sortDesc;
    else {
      this.sortKey = key;
      this.sortDesc = key === 'lastSeen';
    }
    this.onFilter();
  }

  clearFilters(): void {
    this.search = '';
    this.techFilter = this.statusFilter = this.typeFilter = this.seenFilter = '';
    this.siteFilter = '';
    this.onFilter();
  }

  techIcon(t: Tech) {
    return t === 'Barcode' ? Barcode : t === 'QR Code' ? QrCode : t === 'BLE' ? Bluetooth : Radio;
  }

  techClass(t: Tech): string {
    return 'tech--' + PREFIX[t].toLowerCase();
  }

  siteLabel(t: TagId): string {
    return t.location ? `${t.site} / ${t.location}` : t.site;
  }

  assetLabel(a: LinkedAsset): string {
    return `${a.kind} ${a.tag}`;
  }

  fmtSeen(ms: number | null): string {
    if (ms === null) return '—';
    const now = new Date();
    const d = new Date(ms);
    const mins = Math.round((now.getTime() - ms) / 60_000);
    if (mins < 1) return 'Just now';
    if (mins < 60) return `${mins} min ago`;
    const time = d.toLocaleTimeString('en-US', { hour: 'numeric', minute: '2-digit' });
    const startOfToday = new Date(now.getFullYear(), now.getMonth(), now.getDate()).getTime();
    if (ms >= startOfToday) return `Today ${time}`;
    if (ms >= startOfToday - 86_400_000) return `Yesterday ${time}`;
    return this.fmtDate(ms);
  }

  fmtDate(ms: number | null, withTime = false): string {
    if (ms === null) return '—';
    const d = new Date(ms);
    const date = `${d.getDate()} ${MONTHS[d.getMonth()]} ${d.getFullYear()}`;
    if (!withTime) return date;
    const p = (n: number) => String(n).padStart(2, '0');
    return `${date}, ${p(d.getHours())}:${p(d.getMinutes())}`;
  }

  // ===== Panel =====

  get selected(): TagId | null {
    return this.selectedId ? TAGS.find((t) => t.id === this.selectedId) ?? null : null;
  }

  select(t: TagId): void {
    this.selectedId = t.id;
    this.panelTab = 'details';
    this.menuOpen = '';
  }

  /** Other identifiers on the same asset ("one asset can have multiple identifiers"). */
  siblings(t: TagId): TagId[] {
    return t.asset ? TAGS.filter((x) => x !== t && x.status === 'Assigned' && x.asset?.tag === t.asset!.tag) : [];
  }

  viewAsset(t: TagId): void {
    this.menuOpen = '';
    if (!t.asset) return;
    if (t.asset.real) {
      this.router.navigate(['/assets/status-condition'], { queryParams: { tag: t.asset.tag } });
      return;
    }
    this.showAssetTags(t.asset.tag);
    this.showToast(`${t.asset.tag} isn't in the demo asset register. Showing its identifiers instead.`);
  }

  showAssetTags(assetTag: string): void {
    this.clearFilters();
    this.search = assetTag;
  }

  // ===== Changes =====

  private log(t: TagId, text: string): void {
    t.history.unshift({ at: Date.now(), text, by: CURRENT_USER });
    t.touched = Date.now();
    this.version++;
  }

  openAssign(t: TagId): void {
    this.menuOpen = '';
    this.formError = '';
    this.dialog = { kind: 'assign', tagId: t.id, asset: t.asset?.tag ?? '', site: t.site, location: t.location, note: '' };
  }

  private assetCache: { version: number; list: LinkedAsset[] } = { version: -1, list: [] };

  get assetOptions(): LinkedAsset[] {
    if (this.assetCache.version !== this.version) this.assetCache = { version: this.version, list: knownAssets() };
    return this.assetCache.list;
  }

  locationsOf(site: string): string[] {
    return SITES.find((s) => s.name === site)?.locations ?? [];
  }

  private resolveAsset(code: string): LinkedAsset | null {
    const c = code.trim().toUpperCase();
    if (!c) return null;
    return knownAssets().find((a) => a.tag.toUpperCase() === c) ?? { tag: c, kind: 'Asset', real: false };
  }

  saveAssign(): void {
    const d = this.dialog;
    if (d?.kind !== 'assign') return;
    const t = TAGS.find((x) => x.id === d.tagId);
    const asset = this.resolveAsset(d.asset);
    if (!t) return;
    if (!asset) return void (this.formError = 'Choose the asset to link.');
    if (t.status === 'Assigned' && t.asset?.tag === asset.tag) return void (this.formError = `${t.id} is already linked to ${asset.tag}.`);
    const previous = t.asset;
    t.asset = asset;
    t.status = 'Assigned';
    t.assignedOn = Date.now();
    t.site = d.site;
    t.location = d.location;
    this.log(t, previous ? `Reassigned from ${this.assetLabel(previous)} to ${this.assetLabel(asset)}${d.note.trim() ? ': ' + d.note.trim() : ''}` : `Assigned to ${this.assetLabel(asset)}`);
    this.dialog = null;
    this.selectedId = t.id;
    this.showToast(`${t.id} ${previous ? 'reassigned' : 'assigned'} to ${asset.tag}.`);
  }

  openUnassign(t: TagId): void {
    this.menuOpen = '';
    this.formError = '';
    this.dialog = { kind: 'unassign', tagId: t.id, reason: '' };
  }

  saveUnassign(): void {
    const d = this.dialog;
    if (d?.kind !== 'unassign') return;
    const t = TAGS.find((x) => x.id === d.tagId);
    if (!t?.asset) return;
    const from = t.asset;
    t.asset = null;
    t.status = 'Available';
    t.assignedOn = null;
    this.log(t, `Unassigned from ${this.assetLabel(from)}${d.reason.trim() ? ': ' + d.reason.trim() : ''}`);
    this.dialog = null;
    this.showToast(`${t.id} is now available. ${from.tag}'s history keeps the link.`);
  }

  openStatus(t: TagId, to: 'Inactive' | 'Available'): void {
    this.menuOpen = '';
    this.formError = '';
    this.dialog = { kind: 'status', tagId: t.id, to, reason: '' };
  }

  saveStatus(): void {
    const d = this.dialog;
    if (d?.kind !== 'status') return;
    const t = TAGS.find((x) => x.id === d.tagId);
    if (!t) return;
    if (d.to === 'Inactive' && !d.reason.trim()) return void (this.formError = 'Enter why the tag is being deactivated.');
    const from = t.asset;
    if (d.to === 'Inactive') {
      t.asset = null;
      t.assignedOn = null;
    }
    t.status = d.to;
    this.log(t, d.to === 'Inactive' ? `Deactivated${from ? ' and unlinked from ' + this.assetLabel(from) : ''}: ${d.reason.trim()}` : `Reactivated${d.reason.trim() ? ': ' + d.reason.trim() : ''}`);
    this.dialog = null;
    this.showToast(`${t.id} ${d.to === 'Inactive' ? 'deactivated' : 'reactivated'}.`);
  }

  // ===== Register =====

  openRegister(): void {
    this.menuOpen = '';
    this.formError = '';
    const tech: Tech = 'Passive RFID';
    this.dialog = { kind: 'register', tech, type: DEFAULT_TYPE[tech], number: String(nextNumber(tech)).padStart(6, '0'), code: '', site: SITES[0].name, location: SITES[0].locations[0], asset: '' };
    this.fillCode();
  }

  onRegisterTech(): void {
    const d = this.dialog;
    if (d?.kind !== 'register') return;
    d.type = DEFAULT_TYPE[d.tech];
    d.number = String(nextNumber(d.tech)).padStart(6, '0');
    this.fillCode();
  }

  /** Suggest a code value (EPC / barcode / MAC) for the number; the user can overwrite it. */
  fillCode(): void {
    const d = this.dialog;
    if (d?.kind === 'register') d.code = codeFor(d.tech, Number(d.number) || 0);
  }

  get registerPreview(): string {
    const d = this.dialog;
    return d?.kind === 'register' ? tagIdOf(d.tech, Number(d.number) || 0) : '';
  }

  saveRegister(): void {
    const d = this.dialog;
    if (d?.kind !== 'register') return;
    const n = Number(d.number);
    if (!Number.isInteger(n) || n <= 0) return void (this.formError = 'Enter a tag number.');
    const id = tagIdOf(d.tech, n);
    if (TAGS.some((t) => t.id === id)) return void (this.formError = `${id} already exists.`);
    if (!d.code.trim()) return void (this.formError = 'Enter the encoded value.');
    if (TAGS.some((t) => t.code === d.code.trim())) return void (this.formError = 'Another tag already uses this encoded value.');
    const t = this.createTag(d.tech, id, d.type, d.code.trim(), d.site, d.location, d.asset);
    this.selectedId = t.id;
    this.dialog = null;
    this.clearFilters();
    this.sortKey = 'lastSeen';
    this.showToast(`${t.id} registered${t.asset ? ' and assigned to ' + t.asset.tag : ''}.`);
  }

  private createTag(tech: Tech, id: string, type: TagType, code: string, site: string, location: string, asset: string): TagId {
    const now = Date.now();
    const linked = this.resolveAsset(asset);
    const siteCode = SITES.find((s) => s.name === site)?.code ?? 'CST';
    const t: TagId = {
      id, tech, type, code, asset: linked, site, location, status: linked ? 'Assigned' : 'Available', assignedOn: linked ? now : null,
      lastSeen: null, reader: readerFor(tech, siteCode), registeredOn: now,
      history: [...(linked ? [{ at: now, text: `Assigned to ${this.assetLabel(linked)}`, by: CURRENT_USER }] : []), { at: now, text: `Registered as ${tech} ${type.toLowerCase()}`, by: CURRENT_USER }],
    };
    TAGS.unshift(t);
    this.version++;
    return t;
  }

  // ===== Bulk import =====

  openImport(): void {
    this.menuOpen = '';
    this.dialog = { kind: 'import', rows: [], fileName: '', error: '' };
  }

  downloadTemplate(): void {
    const sample = [
      { Technology: 'Passive RFID', 'Tag Number': nextNumber('Passive RFID'), Type: 'Hard Tag', 'Encoded Value': '', Site: 'Dubai Plant', Location: 'Warehouse', 'Asset Code': 'FL-008' },
      { Technology: 'QR Code', 'Tag Number': nextNumber('QR Code'), Type: 'Label', 'Encoded Value': '', Site: 'Central Store', Location: '', 'Asset Code': '' },
    ];
    const book = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(book, XLSX.utils.json_to_sheet(sample), 'Tag IDs');
    XLSX.writeFile(book, 'Tag_IDs_Import_Template.xlsx');
  }

  onImportFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    const d = this.dialog;
    if (!file || d?.kind !== 'import') return;
    d.fileName = file.name;
    d.error = '';
    const reader = new FileReader();
    reader.onload = () => {
      try {
        const book = XLSX.read(reader.result, { type: 'array' });
        const sheet = book.Sheets[book.SheetNames[0]];
        const raw = XLSX.utils.sheet_to_json<Record<string, unknown>>(sheet, { defval: '' });
        d.rows = this.validate(raw);
        if (!d.rows.length) d.error = 'The file has no rows.';
      } catch {
        d.error = 'This file could not be read. Use the Excel or CSV template.';
        d.rows = [];
      }
      input.value = '';
    };
    reader.readAsArrayBuffer(file);
  }

  private validate(raw: Record<string, unknown>[]): ImportRow[] {
    const ids = new Set(TAGS.map((t) => t.id));
    const codes = new Set(TAGS.map((t) => t.code));
    const get = (r: Record<string, unknown>, ...keys: string[]) => {
      const k = Object.keys(r).find((x) => keys.includes(x.trim().toLowerCase()));
      return k ? String(r[k]).trim() : '';
    };
    return raw.map((r, i) => {
      const techText = get(r, 'technology', 'tech');
      const tech = TECH_ALIASES[techText.toLowerCase()] ?? null;
      const num = Number(get(r, 'tag number', 'number', 'tag no'));
      const id = tech && num > 0 ? tagIdOf(tech, num) : '';
      const typeText = get(r, 'type');
      const type = (TYPES.find((x) => x.toLowerCase() === typeText.toLowerCase()) ?? (tech ? DEFAULT_TYPE[tech] : 'Label')) as TagType;
      const code = get(r, 'encoded value', 'epc', 'code') || (tech && num > 0 ? codeFor(tech, num) : '');
      const site = SITES.find((s) => s.name.toLowerCase() === get(r, 'site').toLowerCase())?.name ?? '';
      const location = get(r, 'location');
      let error = '';
      if (!tech) error = techText ? `Unknown technology "${techText}"` : 'Technology missing';
      else if (!(num > 0)) error = 'Tag number missing';
      else if (ids.has(id)) error = `${id} already exists`;
      else if (codes.has(code)) error = 'Encoded value already used';
      else if (!site) error = 'Unknown site';
      if (!error) {
        ids.add(id);
        codes.add(code);
      }
      return { line: i + 2, tech, id, type, code, site, location, asset: get(r, 'asset code', 'asset'), error };
    });
  }

  get importValid(): number {
    const d = this.dialog;
    return d?.kind === 'import' ? d.rows.filter((r) => !r.error).length : 0;
  }

  saveImport(): void {
    const d = this.dialog;
    if (d?.kind !== 'import') return;
    const ok = d.rows.filter((r) => !r.error && r.tech);
    for (const r of ok) this.createTag(r.tech!, r.id, r.type, r.code, r.site, r.location, r.asset);
    const skipped = d.rows.length - ok.length;
    this.dialog = null;
    this.clearFilters();
    this.showToast(`${ok.length} tag${ok.length === 1 ? '' : 's'} imported${skipped ? `, ${skipped} skipped` : ''}.`);
  }

  // ===== Export / menus =====

  exportRows(): void {
    this.menuOpen = '';
    const rows = this.rows.map((t) => ({
      'Tag ID': t.id, Type: t.type, Technology: t.tech, 'Encoded Value': t.code, 'Linked Asset': t.asset ? this.assetLabel(t.asset) : '',
      Site: t.site, Location: t.location, 'Assignment Status': t.status, 'Assigned On': t.assignedOn ? this.fmtDate(t.assignedOn, true) : '', 'Last Seen': t.lastSeen ? this.fmtDate(t.lastSeen, true) : '',
    }));
    const book = XLSX.utils.book_new();
    XLSX.utils.book_append_sheet(book, XLSX.utils.json_to_sheet(rows), 'Tag IDs');
    XLSX.writeFile(book, 'Tag_IDs.xlsx');
  }

  toggleMenu(name: string, event: Event): void {
    event.stopPropagation();
    this.menuOpen = this.menuOpen === name ? '' : name;
  }

  @HostListener('document:click')
  closeMenus(): void {
    this.menuOpen = '';
    this.moreOpen = false;
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closeMenus();
    if (this.dialog) this.dialog = null;
    // drawer mode: Esc also closes the details
    else if (window.innerWidth < 1200) this.selectedId = null;
  }

  private showToast(message: string): void {
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 4000);
  }

  /** Narrow a dialog for the template (strict templates cannot narrow a union on their own). */
  dlg<K extends Dialog['kind']>(kind: K): Extract<Dialog, { kind: K }> | null {
    return this.dialog?.kind === kind ? (this.dialog as Extract<Dialog, { kind: K }>) : null;
  }

  tagById(id: string): TagId | undefined {
    return TAGS.find((t) => t.id === id);
  }
}
