import { Component, ElementRef, ViewChild, inject } from '@angular/core';
import { ExportMenu, ExportRequest } from 'shared-ui';
import { FormsModule } from '@angular/forms';
import { Observable, concat, forkJoin, of, toArray } from 'rxjs';
import { RouterLink } from '@angular/router';
import {
  BookOpen, Building2, ChartNoAxesColumnIncreasing, ChevronDown, ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight, CircleCheck,
  ClipboardList, Download, EllipsisVertical, Eye, FileText, Flame, Droplets, HardHat, ListChecks, LucideAngularModule, Plus, Save, Search,
  Send, Server, Settings, ShieldCheck, Truck, Upload, X, Zap
} from 'lucide-angular';
import { ADMIN_LOCATIONS, ADMIN_PEOPLE, AdminPage, CURRENT_USER, formatDate, todayIso } from '../shared/admin-common';
import { ChecklistPreview } from './checklist-preview/checklist-preview';
import { ChecklistSections, DeleteRequest, ItemRef } from './checklist-sections/checklist-sections';
import { LIBRARY, LibraryEntry, categoryAssets, categoryTeam, librarySections } from './checklist-templates.data';
import {
  ASSET_TYPES, CATEGORIES, ChecklistItem, ChecklistSection, ChecklistTemplate, FREQUENCIES, STATUSES, TEAMS, TemplateIcon, TemplateStatus,
  blankItem, itemCount, itemErrors, maxScore, statusTone
} from './checklist-templates.model';
import { ChecklistTemplatesService, templateKey } from './checklist-templates.service';
import { ItemDrawer, ItemSave } from './item-drawer/item-drawer';

type Tab = 'items' | 'settings' | 'history';

interface DrawerState {
  sectionId: string;
  item: ChecklistItem;
  isNew: boolean;
}

/** Persisted items grouped by their section name, offered in the Template Library. */
interface SavedSection {
  title: string;
  items: ChecklistItem[];
}

interface ConfirmState {
  title: string;
  message: string;
  action: 'template' | 'item' | 'section';
  ref?: DeleteRequest;
}

const PAGE_SIZE = 10;

/** Administration > Checklist Templates: template list, section/item builder and item settings drawer. */
@Component({
  standalone: true,
  selector: 'app-admin-checklist-templates',
  imports: [FormsModule, RouterLink, LucideAngularModule, ChecklistSections, ItemDrawer, ChecklistPreview, ExportMenu],
  templateUrl: './checklist-templates.html',
  styleUrls: ['../shared/admin-workspace.css', './checklist-templates.css']
})
export class ChecklistTemplatesPage extends AdminPage {
  private readonly service = inject(ChecklistTemplatesService);
  @ViewChild('importInput') importInput?: ElementRef<HTMLInputElement>;
  @ViewChild(ChecklistSections) sectionsView?: ChecklistSections;

  readonly icons = {
    BookOpen, ChartNoAxesColumnIncreasing, ChevronDown, ChevronLeft, ChevronRight, ChevronsLeft, ChevronsRight, CircleCheck, Download,
    EllipsisVertical, Eye, FileText, ListChecks, Plus, Save, Search, Send, Settings, Upload, X
  };
  private readonly templateIcons: Record<TemplateIcon, typeof FileText> = {
    file: FileText, flame: Flame, settings: Settings, hardhat: HardHat, shield: ShieldCheck, clipboard: ClipboardList, truck: Truck,
    server: Server, building: Building2, zap: Zap, droplets: Droplets
  };
  readonly categories = CATEGORIES;
  readonly statuses = STATUSES;
  readonly frequencies = FREQUENCIES;
  readonly assetTypes = ASSET_TYPES;
  readonly locations = ADMIN_LOCATIONS;
  readonly teams = TEAMS;
  readonly library = LIBRARY;
  readonly formatDate = formatDate;

  search = '';
  listSearch = '';
  page = 1;
  listCollapsed = false;

  selectedId: string | null = null;
  /** Working copy; the builder, settings and drawer edit it and Save Draft / Publish store it */
  draft!: ChecklistTemplate;
  isNew = false;
  dirty = false;
  showErrors = false;
  tab: Tab = 'items';
  drawer: DrawerState | null = null;

  headMenu = false;
  bulkOpen = false;
  scoringOpen = false;
  previewOpen = false;
  libraryOpen = false;
  confirm: ConfirmState | null = null;
  savingItem = false;

  constructor() {
    super();
    // Until the saved items are loaded: a blank, unsaved draft
    this.createTemplate();
    this.dirty = false;
    this.toast = null;
    // The template list is built from the saved items, so it fills once they are loaded.
    this.service.loadItems().subscribe({
      next: () => {
        const pristine = this.isNew && !this.draft.name && !this.draftItems;
        const first = this.service.templates()[0];
        if (first && (pristine || (!this.isNew && !this.selectedId))) this.select(first);
      },
      error: (err: Error) => this.notify(`Saved checklist items could not be loaded. ${err.message}`)
    });
  }

  // ===== Stats =====
  get all(): ChecklistTemplate[] {
    return this.service.templates();
  }

  get totalItems(): number {
    return this.all.reduce((sum, t) => sum + itemCount(t), 0);
  }

  get criticalItems(): number {
    return this.all.reduce((sum, t) => sum + t.sections.reduce((a, sec) => a + sec.items.filter((i) => i.critical).length, 0), 0);
  }

  // ===== List =====
  get filtered(): ChecklistTemplate[] {
    const terms = (this.search + ' ' + this.listSearch).trim().toLowerCase().split(/\s+/).filter(Boolean);
    return this.all.filter((t) => {
      const hay = `${t.name} ${t.code} ${t.owner}`.toLowerCase();
      return terms.every((q) => hay.includes(q));
    });
  }

  get pageCount(): number {
    return Math.max(1, Math.ceil(this.filtered.length / PAGE_SIZE));
  }

  get pages(): number[] {
    return Array.from({ length: this.pageCount }, (_, i) => i + 1);
  }

  get paged(): ChecklistTemplate[] {
    const page = Math.min(this.page, this.pageCount);
    return this.filtered.slice((page - 1) * PAGE_SIZE, page * PAGE_SIZE);
  }

  goTo(page: number): void {
    this.page = Math.min(Math.max(page, 1), this.pageCount);
  }

  onFilter(): void {
    this.page = 1;
  }

  get hasFilters(): boolean {
    return !!(this.search || this.listSearch);
  }

  clearFilters(): void {
    this.search = '';
    this.listSearch = '';
    this.page = 1;
  }

  iconFor(t: ChecklistTemplate) {
    return this.templateIcons[t.icon];
  }

  templateIcon(icon: TemplateIcon) {
    return this.templateIcons[icon];
  }

  tone(status: TemplateStatus): string {
    return statusTone(status);
  }

  items(t: ChecklistTemplate): number {
    return itemCount(t);
  }

  select(t: ChecklistTemplate): void {
    this.selectedId = t.id;
    this.draft = structuredClone(t);
    this.isNew = false;
    this.dirty = false;
    this.showErrors = false;
    this.drawer = null;
    this.closeMenus();
  }

  // ===== Header =====
  get owners(): string[] {
    const set = new Set(ADMIN_PEOPLE);
    this.all.forEach((t) => set.add(t.owner));
    return [...set].filter(Boolean).sort((a, b) => a.localeCompare(b));
  }

  get draftMaxScore(): number {
    return maxScore(this.draft);
  }

  get draftItems(): number {
    return itemCount(this.draft);
  }

  markDirty(): void {
    this.dirty = true;
  }

  /** A new template's single section carries the template name, so items show under it. */
  onNameChange(): void {
    this.markDirty();
    if (this.isNew && this.draft.sections[0]) this.draft.sections[0].title = this.draft.name.trim();
  }

  closeMenus(): void {
    this.headMenu = false;
    this.bulkOpen = false;
    this.scoringOpen = false;
  }

  get errors(): Record<string, string> {
    const e: Record<string, string> = {};
    const name = this.draft.name.trim();
    if (!name) e['name'] = 'Template name is required.';
    else if (name.length > 60) e['name'] = 'Keep the template name under 60 characters.';
    else if (this.service.nameTaken(name, this.isNew ? null : this.draft.id)) e['name'] = 'Another template already uses this name.';
    return e;
  }

  err(field: string): string | null {
    return this.showErrors ? (this.errors[field] ?? null) : null;
  }

  /**
   * Saves the template. A new template creates its items (section = template name); an existing one whose name changed
   * moves its items to the new section name. Item edits of an existing template are saved when the item is saved.
   */
  commit(): void {
    this.closeMenus();
    this.showErrors = true;
    const errs = Object.values(this.errors);
    if (errs.length) {
      this.tab = 'settings';
      this.notify(errs[0]);
      return;
    }
    const name = this.draft.name.trim();
    const items = this.draft.sections.flatMap((s) => s.items);
    if (this.savingTemplate) return;
    if (this.isNew) {
      if (!items.length) {
        this.tab = 'items';
        this.notify('Add at least one checklist item; a template is its items.');
        return;
      }
      for (const i of items) {
        if (Object.keys(itemErrors(i)).length) {
          this.tab = 'items';
          this.notify(`Fix "${i.text || 'untitled item'}" before saving.`);
          return;
        }
      }
      this.runSave(this.service.createTemplate(name, items), name, `${name} created with ${items.length} ${items.length === 1 ? 'item' : 'items'}.`);
      return;
    }
    const current = this.selectedId ? this.service.get(this.selectedId) : null;
    if (current && current.name !== name) this.runSave(this.service.renameTemplate(current.id, name), name, `Renamed to ${name}.`);
    else {
      this.dirty = false;
      this.showErrors = false;
      this.notify('Nothing to save: item changes are saved as you save each item.');
    }
  }

  savingTemplate = false;

  private runSave(request: Observable<unknown>, name: string, message: string): void {
    this.savingTemplate = true;
    request.subscribe({
      next: () => {
        this.savingTemplate = false;
        const saved = this.service.get(templateKey(name));
        if (saved) this.select(saved);
        this.notify(message);
      },
      error: (err: Error) => {
        this.savingTemplate = false;
        // Items created before the failure are saved; reload so the list shows what exists.
        this.service.loadItems().subscribe({ error: () => undefined });
        this.notify(`Template not saved. ${err.message}`);
      }
    });
  }

  private findDrawer(itemId: string): DrawerState | null {
    for (const s of this.draft.sections) {
      const item = s.items.find((i) => i.id === itemId);
      if (item) return { sectionId: s.id, item, isNew: false };
    }
    return null;
  }

  discard(): void {
    const saved = this.selectedId ? this.service.get(this.selectedId) : null;
    const fallback = saved ?? this.all[0];
    if (fallback) this.select(fallback);
    this.notify('Changes discarded.');
  }

  createTemplate(): void {
    this.closeMenus();
    this.selectedId = null;
    this.isNew = true;
    this.dirty = true;
    this.showErrors = false;
    this.drawer = null;
    this.tab = 'settings';
    this.draft = {
      id: '',
      name: '',
      code: '',
      version: 'v1.0',
      category: 'Inspection',
      owner: '',
      updated: todayIso(),
      status: 'Draft',
      icon: 'file',
      tone: 'purple',
      description: '',
      frequency: '',
      assetTypes: [],
      locations: [],
      teams: [],
      requireApproval: false,
      passThreshold: 80,
      scoringMethod: 'Weighted',
      failOnCritical: true,
      sections: [{ id: this.service.newId('S'), title: '', items: [] }],
      uses: 0,
      versions: [],
      executions: []
    };
    this.notify('Name the template, add its items, then Save.');
  }

  duplicateTemplate(): void {
    this.closeMenus();
    const source = this.selectedId ? this.service.get(this.selectedId) : null;
    if (!source) return;
    this.selectedId = null;
    this.draft = this.service.cloneOf(source);
    this.isNew = true;
    this.dirty = true;
    this.drawer = null;
    this.notify(`Duplicated ${source.name}. Change the name if needed, then Save to create its items.`);
  }

  askDeleteTemplate(): void {
    this.closeMenus();
    if (this.isNew) {
      this.discard();
      return;
    }
    this.confirm = {
      title: 'Delete template?',
      message: `${this.draft.name} and its ${this.draftItems} ${this.draftItems === 1 ? 'item' : 'items'} will be deleted.`,
      action: 'template'
    };
  }

  // ===== Template library =====
  useLibrary(entry: LibraryEntry, index: number): void {
    this.libraryOpen = false;
    this.createTemplate();
    this.tab = 'items';
    Object.assign(this.draft, {
      name: entry.name,
      category: entry.category,
      icon: entry.icon,
      tone: entry.tone,
      description: entry.description,
      frequency: entry.frequency,
      assetTypes: categoryAssets(entry.category),
      teams: [categoryTeam(entry.category)],
      sections: [{ id: this.service.newId('S'), title: entry.name, items: librarySections(this.service.newId('LIB'), entry, index).flatMap((sec) => sec.items) }]
    });
    this.notify(`${entry.name} added from the library. Edit it if needed, then Save to create its items.`);
  }

  // ===== Items & sections =====
  openItem(ref: ItemRef): void {
    const section = this.draft.sections.find((s) => s.id === ref.sectionId);
    const item = section?.items.find((i) => i.id === ref.itemId);
    if (section && item) this.drawer = { sectionId: section.id, item, isNew: false };
  }

  addItem(sectionId?: string): void {
    const target = sectionId ?? this.drawer?.sectionId ?? this.draft.sections[this.draft.sections.length - 1].id;
    this.drawer = { sectionId: target, item: blankItem(this.service.newId('I')), isNew: true };
    this.sectionsView?.expand(target);
  }

  /**
   * New items are created through the API; edits to server-backed items go through PUT. Items seeded
   * with the demo templates have no server itemId, so their edits stay in the draft.
   */
  onItemSave(e: ItemSave): void {
    const target = this.draft.sections.find((s) => s.id === e.sectionId);
    if (!target) return;
    const isNew = !!this.drawer?.isNew;
    // A template that is not saved yet keeps its items in the draft; Save creates them.
    if (this.isNew || (!isNew && !this.service.isPersisted(e.item.id))) {
      this.storeItem(e, isNew);
      return;
    }
    if (this.savingItem) return;
    this.savingItem = true;
    const request = isNew ? this.service.createItem(e.item, this.sectionTitle(target)) : this.service.updateItem(e.item, this.sectionTitle(target));
    request.subscribe({
      next: (saved) => {
        this.savingItem = false;
        this.storeItem({ ...e, item: saved }, isNew);
      },
      error: (err: Error) => {
        this.savingItem = false;
        this.notify(`${isNew ? 'Item not added' : 'Item not updated'}. ${err.message}`);
      }
    });
  }

  /** Section name items are saved under: the saved template name (a renamed draft only changes on Save). */
  private sectionTitle(section: ChecklistSection): string {
    return section.title;
  }

  private storeItem(e: ItemSave, wasNew: boolean): void {
    const target = this.draft.sections.find((s) => s.id === e.sectionId);
    if (!target) return;
    const from = this.draft.sections.find((s) => s.items.some((i) => i.id === e.item.id));
    if (from && from.id === target.id) {
      const idx = from.items.findIndex((i) => i.id === e.item.id);
      from.items[idx] = e.item;
    } else {
      if (from) from.items = from.items.filter((i) => i.id !== e.item.id);
      target.items.push(e.item);
    }
    this.drawer = { sectionId: target.id, item: e.item, isNew: false };
    this.markDirty();
    this.notify(this.isNew ? 'Item added. Save the template to create it.' : wasNew ? 'Item created.' : 'Item updated.');
  }

  duplicateItem(ref: ItemRef): void {
    const section = this.draft.sections.find((s) => s.id === ref.sectionId);
    if (!section) return;
    const idx = section.items.findIndex((i) => i.id === ref.itemId);
    if (idx < 0) return;
    const copy: ChecklistItem = { ...section.items[idx], id: this.service.newId('I'), text: `${section.items[idx].text} (copy)` };
    section.items.splice(idx + 1, 0, copy);
    this.markDirty();
    this.notify('Item duplicated.');
  }

  askDelete(ref: DeleteRequest): void {
    const section = this.draft.sections.find((s) => s.id === ref.sectionId);
    if (!section) return;
    if (ref.kind === 'section') {
      const saved = section.items.filter((i) => this.service.isPersisted(i.id)).length;
      this.confirm = {
        title: 'Delete section?',
        message:
          `"${section.title}" and its ${section.items.length} items will be removed from this template.` +
          (saved ? ` ${saved} saved ${saved === 1 ? 'item is' : 'items are'} deleted permanently.` : ''),
        action: 'section',
        ref
      };
    } else {
      const item = section.items.find((i) => i.id === ref.itemId);
      this.confirm = { title: 'Delete item?', message: `"${item?.text ?? ''}" will be removed from ${section.title}.`, action: 'item', ref };
    }
  }

  confirmAction(): void {
    const c = this.confirm;
    this.confirm = null;
    if (!c) return;
    if (c.action === 'template') {
      const name = this.draft.name;
      const id = this.draft.id;
      this.service.deleteTemplate(id).subscribe({
        next: () => {
          const next = this.all[0];
          if (next) this.select(next);
          else this.createTemplate();
          this.goTo(this.page);
          this.notify(`${name} deleted.`);
        },
        error: (err: Error) => this.notify(`${name} not fully deleted. ${err.message}`)
      });
      return;
    }
    const ref = c.ref;
    if (!ref) return;
    const section = this.draft.sections.find((s) => s.id === ref.sectionId);
    if (!section) return;
    // Server-backed items are deleted through the API first; the draft only changes once that succeeds.
    const ids = c.action === 'section' ? section.items.map((i) => i.id) : [ref.itemId ?? ''];
    this.deleteSaved(ids).subscribe({
      next: () => {
        if (c.action === 'section') {
          this.draft.sections = this.draft.sections.filter((s) => s.id !== ref.sectionId);
          if (this.drawer?.sectionId === ref.sectionId) this.drawer = null;
          this.notify('Section deleted.');
        } else {
          section.items = section.items.filter((i) => i.id !== ref.itemId);
          if (this.drawer?.item.id === ref.itemId) this.drawer = null;
          this.notify('Item deleted.');
        }
        this.markDirty();
        // A saved template whose last item is gone no longer exists (a template is its items).
        if (!this.isNew && this.selectedId && !this.service.get(this.selectedId)) {
          const next = this.all[0];
          if (next) this.select(next);
          else this.createTemplate();
        }
      },
      error: (err: Error) => this.notify(`${c.action === 'section' ? 'Section' : 'Item'} not deleted. ${err.message}`)
    });
  }

  /** DELETE every server-backed id in the list; completes immediately when none are. */
  private deleteSaved(ids: string[]): Observable<unknown> {
    const saved = ids.filter((id) => this.service.isPersisted(id));
    return saved.length ? forkJoin(saved.map((id) => this.service.deleteItem(id))) : of(null);
  }

  // ===== Bulk actions / scoring =====
  bulk(action: 'required' | 'optional' | 'clearCritical' | 'score10' | 'collapse' | 'expand'): void {
    this.bulkOpen = false;
    if (action === 'collapse') {
      this.sectionsView?.collapseAll();
      return;
    }
    if (action === 'expand') {
      this.sectionsView?.expandAll();
      return;
    }
    const change: Partial<ChecklistItem> =
      action === 'required' ? { required: true } : action === 'optional' ? { required: false } : action === 'clearCritical' ? { critical: false } : { score: 10 };
    const all = this.draft.sections.flatMap((s) => s.items);
    // Server-backed items are updated through PUT; the rest only change in the draft.
    const updates = this.draft.sections.flatMap((s) =>
      s.items.filter((i) => this.service.isPersisted(i.id)).map((i) => this.service.updateItem({ ...i, ...change }, s.title))
    );
    const done: Observable<unknown> = updates.length ? forkJoin(updates) : of(null);
    done.subscribe({
      next: () => {
        all.forEach((i) => Object.assign(i, change));
        this.markDirty();
        this.notify(`Applied to ${all.length} items.`);
      },
      error: (err: Error) => this.notify(`Bulk change not applied. ${err.message}`)
    });
  }

  toggleBulk(): void {
    this.bulkOpen = !this.bulkOpen;
    this.scoringOpen = false;
  }

  toggleScoring(): void {
    this.scoringOpen = !this.scoringOpen;
    this.bulkOpen = false;
  }

  toggleHeadMenu(): void {
    this.headMenu = !this.headMenu;
  }

  // ===== Settings tab =====
  hasValue(list: string[], value: string): boolean {
    return list.includes(value);
  }

  toggleValue(list: string[], value: string): void {
    const i = list.indexOf(value);
    if (i >= 0) list.splice(i, 1);
    else list.push(value);
    this.markDirty();
  }

  // ===== Usage =====
  get passRate(): number {
    const ex = this.draft.executions;
    return ex.length ? Math.round((ex.filter((e) => e.result === 'Passed').length / ex.length) * 100) : 0;
  }

  // ===== Import / export (JSON keeps sections and items intact) =====
  /** Single-template JSON export (definition with sections and items). */
  exportJson(only: ChecklistTemplate): void {
    this.closeMenus();
    const data = [only];
    const url = URL.createObjectURL(new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' }));
    const a = document.createElement('a');
    a.href = url;
    a.download = `${only.code}.json`;
    a.click();
    URL.revokeObjectURL(url);
    this.notify(`Exported ${data.length} ${data.length === 1 ? 'template' : 'templates'}.`);
  }

  /** Export menu (CSV / PDF): every template matching the current search and filters. */
  readonly exportRequest = (): ExportRequest<ChecklistTemplate> => ({
    title: 'Checklist Templates',
    fileBase: 'checklist-templates',
    columns: [
      { label: 'Code', key: 'code' }, { label: 'Name', key: 'name' }, { label: 'Items', value: (t) => itemCount(t) },
      { label: 'Owner', key: 'owner' }, { label: 'Updated', key: 'updated' }
    ],
    filters: { Search: `${this.search} ${this.listSearch}`.trim() },
    rows: () => this.filtered
  });

  openImport(): void {
    this.importInput?.nativeElement.click();
  }

  onImportFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    file.text().then((text) => {
      let rows: unknown;
      try {
        rows = JSON.parse(text);
      } catch {
        this.notify('That file is not valid JSON. Use a file from Export.');
        return;
      }
      const list = (Array.isArray(rows) ? rows : [rows]) as Partial<ChecklistTemplate>[];
      const valid = list.filter((row) => row && typeof row.name === 'string' && row.name.trim() && Array.isArray(row.sections));
      const taken = new Set<string>();
      const todo = valid.filter((row) => {
        const key = templateKey(row.name as string);
        if (taken.has(key) || this.service.nameTaken(row.name as string, null)) return false;
        taken.add(key);
        return true;
      });
      if (!todo.length) {
        this.notify(valid.length ? 'Those templates already exist.' : 'No valid templates found in that file.');
        return;
      }
      // Each template's items are created under its name, one template after the other.
      const created = todo.map((row) => {
        const name = (row.name as string).trim();
        const items = (row.sections ?? []).flatMap((sec) => sec.items ?? []).map((i) => ({ ...blankItem(this.service.newId('I')), ...i, id: this.service.newId('I') }));
        return this.service.createTemplate(name, items);
      });
      concat(...created).pipe(toArray()).subscribe({
        next: () => this.notify(`Imported ${todo.length} ${todo.length === 1 ? 'template' : 'templates'}${valid.length > todo.length ? ` (${valid.length - todo.length} already existed)` : ''}.`),
        error: (err: Error) => {
          this.service.loadItems().subscribe({ error: () => undefined });
          this.notify(`Import stopped. ${err.message}`);
        }
      });
    });
  }
}
