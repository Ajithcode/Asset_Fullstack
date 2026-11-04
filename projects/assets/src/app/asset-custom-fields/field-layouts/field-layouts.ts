import { Component, EventEmitter, HostListener, Input, OnChanges, OnInit, Output, SimpleChanges, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { map } from 'rxjs';
import {
  LucideAngularModule,
  CalendarDays,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  ChevronUp,
  Cog,
  Copy,
  EllipsisVertical,
  GripVertical,
  Inbox,
  Pencil,
  Plus,
  Power,
  PowerOff,
  Search,
  SquarePen,
  Trash2,
  X,
} from 'lucide-angular';
import {
  ALL_ASSETS,
  BUILT_IN_FIELDS,
  CustomField,
  FieldGroup,
  FieldLayout,
  FieldStatus,
  LOOKUP_SOURCES,
  LayoutSection,
  SYSTEM_FIELDS,
  isBuiltIn,
} from '../custom-fields.model';
import { AssetTypeOption, CategoryOption, CustomFieldsService, FieldLayoutDto, runAll, toFieldLayout, toLayoutRequest } from '../custom-fields.service';
import { apiErrorMessage } from '../../services/asset-api';

interface LayoutForm {
  original: FieldLayout | null;
  name: string;
  /** Asset type name, or "All Assets". */
  assetType: string;
  description: string;
  status: FieldStatus;
  /** New layouts only: id of a layout to copy the structure from, or '' for a blank layout. */
  copyFrom: string;
}

type DragState =
  | { kind: 'field'; fieldId: string; from: { s: number; i: number } | null }
  | { kind: 'section'; index: number };

/** Section header colours, by position in the layout (matches the design's purple / blue / green sequence). */
const SECTION_TONES = ['violet', 'blue', 'green', 'amber', 'pink', 'teal', 'slate', 'yellow'];
const DESCRIPTION_MAX = 250;
/** Group a blank layout starts with, when it exists. */
const DEFAULT_GROUP = 'General Information';

function cloneSections(sections: LayoutSection[]): LayoutSection[] {
  return sections.map((s) => ({ groupId: s.groupId, group: s.group, fieldIds: [...s.fieldIds] }));
}

@Component({
  selector: 'app-cf-field-layouts',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideAngularModule],
  templateUrl: './field-layouts.html',
  styleUrls: ['../asset-custom-fields.css', './field-layouts.css'],
})
export class CfFieldLayouts implements OnInit, OnChanges {
  private readonly api = inject(CustomFieldsService);

  @Input({ required: true }) layouts!: FieldLayout[];
  @Input({ required: true }) fields!: CustomField[];
  @Input({ required: true }) groups!: FieldGroup[];
  /** Standard asset fields layouts can place. */
  @Input() builtIns: CustomField[] = BUILT_IN_FIELDS;
  /** The page is still loading its data. */
  @Input() loading = false;
  @Output() notify = new EventEmitter<string>();

  readonly icons = {
    CalendarDays, ChevronDown, ChevronLeft, ChevronRight, ChevronUp, Cog, Copy, EllipsisVertical, GripVertical, Inbox, Pencil,
    Plus, Power, PowerOff, Search, SquarePen, Trash2, X,
  };
  types: AssetTypeOption[] = [];
  categories: CategoryOption[] = [];
  /** A save or delete is in flight. */
  saving = false;
  readonly pageSizeOptions = [10, 25, 50];
  readonly descriptionMax = DESCRIPTION_MAX;

  // List
  search = '';
  typeFilter = '';
  page = 1;
  pageSize = 10;
  checked = new Set<FieldLayout>();
  openMenu: string | null = null;

  // Detail / designer
  selected: FieldLayout | null = null;
  draft: LayoutSection[] = [];
  dirty = false;
  view: 'designer' | 'preview' = 'designer';
  availSearch = '';
  collapsed = new Set<string>();
  addGroupOpen = false;
  drag: DragState | null = null;
  /** Where a dragged field would land: section index and position within it. */
  dropAt: { s: number; i: number } | null = null;
  sectionDropAt: number | null = null;
  availDropActive = false;
  saveError = '';

  // Modals
  layoutForm: LayoutForm | null = null;
  layoutSubmitted = false;
  pendingSelect: FieldLayout | null = null;
  deleteTargets: FieldLayout[] = [];

  ngOnInit(): void {
    this.api.assetTypes().subscribe({
      next: (types) => (this.types = types),
      error: (err) => this.notify.emit(apiErrorMessage(err, 'Could not load asset types.')),
    });
    this.api.categories().subscribe({ next: (categories) => (this.categories = categories), error: () => undefined });
    if (!this.selected && this.layouts[0]) this.select(this.layouts[0]);
  }

  /** When the page reloads its layouts, the selection follows the same layout (by id); unsaved designer edits are kept. */
  ngOnChanges(changes: SimpleChanges): void {
    if (!changes['layouts'] || changes['layouts'].firstChange) return;
    const current = this.selected && this.layouts.find((l) => l.id === this.selected!.id);
    if (current && this.dirty) this.selected = current;
    else if (current) this.select(current);
    else this.select(this.layouts[0] ?? null);
    this.checked.clear();
  }

  /** Asset type names for the selects; "All Assets" is for layouts without an asset type. */
  get assetTypes(): string[] {
    const names = this.types.map((t) => t.typeName);
    this.layouts.forEach((l) => l.assetTypeId && !names.includes(l.assetType) && names.push(l.assetType));
    return [...new Set(names), ALL_ASSETS];
  }

  private typeIdOf(name: string): string {
    if (name === ALL_ASSETS) return '';
    return this.types.find((t) => t.typeName === name)?.id ?? this.layouts.find((l) => l.assetType === name)?.assetTypeId ?? '';
  }

  private get allFields(): CustomField[] {
    return [...this.builtIns, ...this.fields];
  }

  private fromDto(dto: FieldLayoutDto): FieldLayout {
    return toFieldLayout(dto, this.allFields);
  }

  // ===== List =====

  get filtered(): FieldLayout[] {
    const q = this.search.trim().toLowerCase();
    return this.layouts.filter(
      (l) =>
        (!q || [l.name, l.assetType, l.description].some((v) => v.toLowerCase().includes(q))) &&
        (!this.typeFilter || l.assetType === this.typeFilter),
    );
  }

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.filtered.length / this.pageSize));
  }

  get currentPage(): number {
    return Math.min(this.page, this.totalPages);
  }

  get pageRows(): FieldLayout[] {
    const start = (this.currentPage - 1) * this.pageSize;
    return this.filtered.slice(start, start + this.pageSize);
  }

  get rangeStart(): number {
    return this.filtered.length ? (this.currentPage - 1) * this.pageSize + 1 : 0;
  }

  get rangeEnd(): number {
    return Math.min(this.currentPage * this.pageSize, this.filtered.length);
  }

  get pageList(): number[] {
    return Array.from({ length: this.totalPages }, (_, i) => i + 1);
  }

  goToPage(p: number): void {
    if (p >= 1 && p <= this.totalPages) this.page = p;
  }

  get allChecked(): boolean {
    return this.pageRows.length > 0 && this.pageRows.every((l) => this.checked.has(l));
  }

  get someChecked(): boolean {
    return !this.allChecked && this.pageRows.some((l) => this.checked.has(l));
  }

  toggleAll(): void {
    const on = !this.allChecked;
    this.pageRows.forEach((l) => (on ? this.checked.add(l) : this.checked.delete(l)));
  }

  toggleRow(l: FieldLayout): void {
    if (this.checked.has(l)) this.checked.delete(l);
    else this.checked.add(l);
  }

  get checkedList(): FieldLayout[] {
    return [...this.checked];
  }

  toggleMenu(key: string, event: MouseEvent): void {
    event.stopPropagation();
    this.openMenu = this.openMenu === key ? null : key;
  }

  @HostListener('document:click')
  closeMenus(): void {
    this.openMenu = null;
    this.addGroupOpen = false;
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.closeMenus();
    this.layoutForm = null;
    this.pendingSelect = null;
    this.deleteTargets = [];
  }

  // ===== Selection =====

  /** Switching layouts with unsaved designer changes asks first. */
  requestSelect(l: FieldLayout): void {
    if (l === this.selected) return;
    if (this.dirty) this.pendingSelect = l;
    else this.select(l);
  }

  select(l: FieldLayout | null): void {
    this.pendingSelect = null;
    this.selected = l;
    this.draft = l ? cloneSections(l.sections) : [];
    this.dirty = false;
    this.saveError = '';
    this.collapsed.clear();
    this.availSearch = '';
  }

  // ===== Field lookup =====

  fieldById(id: string): CustomField | undefined {
    return isBuiltIn({ id }) ? this.builtIns.find((f) => f.id === id) ?? SYSTEM_FIELDS.find((f) => f.id === id) : this.fields.find((f) => f.id === id);
  }

  sectionFields(s: LayoutSection): CustomField[] {
    return s.fieldIds.map((id) => this.fieldById(id)).filter((f): f is CustomField => !!f);
  }

  groupStatus(name: string): FieldStatus | null {
    return this.groups.find((g) => g.name === name)?.status ?? null;
  }

  tone(index: number): string {
    return 'tone--' + SECTION_TONES[index % SECTION_TONES.length];
  }

  get totalFields(): number {
    return this.draft.reduce((n, s) => n + s.fieldIds.length, 0);
  }

  private get usedIds(): Set<string> {
    return new Set(this.draft.flatMap((s) => s.fieldIds));
  }

  isUsed(f: CustomField): boolean {
    return this.usedIds.has(f.id);
  }

  /** The asset type's category and every category above it (category fields of any of them apply). */
  private categoryLineage(assetType: string): Set<string> {
    const ids = new Set<string>();
    const typeId = this.typeIdOf(assetType);
    let id: string | null | undefined = this.types.find((t) => t.id === typeId)?.categoryId;
    while (id && !ids.has(id)) {
      ids.add(id);
      id = this.categories.find((c) => c.id === id)?.parentCategoryId;
    }
    return ids;
  }

  /**
   * Fields a layout for this asset type may use, as the API's available-fields: built-ins, active asset fields,
   * and active category fields for the type's category or any category above it.
   */
  private poolFor(assetType: string): CustomField[] {
    const lineage = this.categoryLineage(assetType);
    return [
      ...this.builtIns,
      ...this.fields.filter(
        (f) => f.status === 'Active' && (f.scope === 'asset' || (f.scope === 'category' && lineage.has(f.appliesToId))),
      ),
    ];
  }

  get availableFields(): CustomField[] {
    if (!this.selected) return [];
    const q = this.availSearch.trim().toLowerCase();
    return this.poolFor(this.selected.assetType).filter((f) => !q || [f.name, f.label, f.type].some((v) => v.toLowerCase().includes(q)));
  }

  /** Active groups without a section yet, for "Add Group". */
  get addableGroups(): FieldGroup[] {
    const present = new Set(this.draft.map((s) => s.groupId));
    return [...this.groups].sort((a, b) => a.order - b.order).filter((g) => g.status === 'Active' && !present.has(g.id));
  }

  // ===== Designer edits =====

  private changed(): void {
    this.dirty = true;
    this.saveError = '';
  }

  addSection(g: FieldGroup): void {
    this.draft.push({ groupId: g.id, group: g.name, fieldIds: [] });
    this.addGroupOpen = false;
    this.collapsed.delete(g.name);
    this.changed();
  }

  removeSection(index: number): void {
    this.draft.splice(index, 1);
    this.changed();
  }

  toggleCollapse(group: string): void {
    if (this.collapsed.has(group)) this.collapsed.delete(group);
    else this.collapsed.add(group);
  }

  /** Unused pool fields that belong to the section's group, for the "add all" shortcut. */
  suggestedFor(s: LayoutSection): CustomField[] {
    if (!this.selected) return [];
    const used = this.usedIds;
    return this.poolFor(this.selected.assetType).filter((f) => !isBuiltIn(f) && f.groupId === s.groupId && !used.has(f.id));
  }

  addSuggested(s: LayoutSection): void {
    s.fieldIds.push(...this.suggestedFor(s).map((f) => f.id));
    this.changed();
  }

  /** Click alternative to dragging: adds the field to the last expanded section. */
  quickAdd(f: CustomField): void {
    const target = [...this.draft].reverse().find((s) => !this.collapsed.has(s.group));
    if (!target) {
      this.notify.emit('Add a group to the layout first.');
      return;
    }
    target.fieldIds.push(f.id);
    this.changed();
  }

  removeField(s: number, i: number): void {
    this.draft[s].fieldIds.splice(i, 1);
    this.changed();
  }

  // ===== Drag and drop =====

  onFieldDragStart(event: DragEvent, fieldId: string, from: { s: number; i: number } | null): void {
    event.stopPropagation();
    this.drag = { kind: 'field', fieldId, from };
    event.dataTransfer?.setData('text/plain', String(fieldId));
    if (event.dataTransfer) event.dataTransfer.effectAllowed = 'move';
  }

  onSectionDragStart(event: DragEvent, index: number): void {
    this.drag = { kind: 'section', index };
    event.dataTransfer?.setData('text/plain', 'section');
    if (event.dataTransfer) event.dataTransfer.effectAllowed = 'move';
  }

  /** Over a field cell: the dragged field would go before it. */
  onCellDragOver(event: DragEvent, s: number, i: number): void {
    if (this.drag?.kind !== 'field') return;
    event.preventDefault();
    event.stopPropagation();
    this.dropAt = { s, i };
  }

  /** Over a section's empty space: the dragged field would go at its end; a dragged section would go before it. */
  onSectionDragOver(event: DragEvent, s: number): void {
    if (!this.drag) return;
    event.preventDefault();
    if (this.drag.kind === 'field') this.dropAt = { s, i: this.draft[s].fieldIds.length };
    else this.sectionDropAt = s;
  }

  onSectionDrop(event: DragEvent, s: number): void {
    event.preventDefault();
    if (this.drag?.kind === 'section') this.moveSection(this.drag.index, s);
    else if (this.drag?.kind === 'field' && this.dropAt) this.placeField(this.drag.fieldId, this.drag.from, this.dropAt);
    this.onDragEnd();
  }

  onCellDrop(event: DragEvent): void {
    event.preventDefault();
    event.stopPropagation();
    if (this.drag?.kind === 'field' && this.dropAt) this.placeField(this.drag.fieldId, this.drag.from, this.dropAt);
    this.onDragEnd();
  }

  /** Dropping a placed field back on the list removes it from the layout. */
  onAvailDragOver(event: DragEvent): void {
    if (this.drag?.kind !== 'field' || !this.drag.from) return;
    event.preventDefault();
    this.availDropActive = true;
  }

  onAvailDrop(event: DragEvent): void {
    event.preventDefault();
    if (this.drag?.kind === 'field' && this.drag.from) this.removeField(this.drag.from.s, this.drag.from.i);
    this.onDragEnd();
  }

  onDragEnd(): void {
    this.drag = null;
    this.dropAt = null;
    this.sectionDropAt = null;
    this.availDropActive = false;
  }

  private placeField(fieldId: string, from: { s: number; i: number } | null, to: { s: number; i: number }): void {
    let index = to.i;
    if (from) {
      if (from.s === to.s && (from.i === to.i || from.i + 1 === to.i)) return;
      this.draft[from.s].fieldIds.splice(from.i, 1);
      if (from.s === to.s && from.i < index) index--;
    } else if (this.usedIds.has(fieldId)) {
      return;
    }
    this.draft[to.s].fieldIds.splice(index, 0, fieldId);
    this.changed();
  }

  moveSection(from: number, to: number): void {
    if (to < 0 || to >= this.draft.length || from === to) return;
    const [s] = this.draft.splice(from, 1);
    this.draft.splice(to, 0, s);
    this.changed();
  }

  // ===== Save / cancel =====

  cancelChanges(): void {
    this.select(this.selected);
  }

  saveLayout(): void {
    const layout = this.selected;
    if (!layout || this.saving) return;
    const used = this.usedIds;
    const missing = SYSTEM_FIELDS.filter((f) => !used.has(f.id));
    if (missing.length) {
      this.saveError = `${missing.map((f) => f.label).join(' and ')} must be on the layout.`;
      return;
    }
    this.saving = true;
    this.api.updateLayout(layout.id, toLayoutRequest({ ...layout, sections: this.draft }, this.allFields)).subscribe({
      next: (dto) => {
        this.saving = false;
        Object.assign(layout, this.fromDto(dto));
        if (this.selected === layout) this.select(layout);
        this.notify.emit(`${layout.name} saved.`);
      },
      error: (err) => {
        this.saving = false;
        this.saveError = apiErrorMessage(err, 'Could not save the layout.');
      },
    });
  }

  // ===== Layout details (add / edit modal) =====

  openAdd(): void {
    this.openMenu = null;
    this.layoutForm = { original: null, name: '', assetType: this.assetTypes[0], description: '', status: 'Active', copyFrom: '' };
    this.layoutSubmitted = false;
  }

  openEdit(l: FieldLayout): void {
    this.openMenu = null;
    this.layoutForm = { original: l, name: l.name, assetType: l.assetType, description: l.description, status: l.status, copyFrom: '' };
    this.layoutSubmitted = false;
  }

  get layoutNameError(): string | null {
    const e = this.layoutForm;
    if (!e) return null;
    const name = e.name.trim();
    if (!name) return 'Layout name is required.';
    if (this.layouts.some((l) => l !== e.original && l.name.toLowerCase() === name.toLowerCase())) return 'A layout with this name already exists.';
    return null;
  }

  /** Sections a new blank layout starts with: the required built-ins in the default (or first active) group. */
  private blankSections(): LayoutSection[] {
    const g = this.groups.find((x) => x.name === DEFAULT_GROUP) ?? [...this.groups].sort((a, b) => a.order - b.order).find((x) => x.status === 'Active');
    return g ? [{ groupId: g.id, group: g.name, fieldIds: SYSTEM_FIELDS.map((f) => f.id) }] : [];
  }

  saveLayoutForm(): void {
    this.layoutSubmitted = true;
    const e = this.layoutForm;
    if (!e || this.layoutNameError || this.saving) return;
    const values = {
      name: e.name.trim(),
      assetType: e.assetType,
      assetTypeId: this.typeIdOf(e.assetType),
      description: e.description.trim(),
      status: e.status,
    };
    if (e.assetType !== ALL_ASSETS && !values.assetTypeId) {
      this.notify.emit(`Asset type "${e.assetType}" was not found.`);
      return;
    }
    const original = e.original;
    let layout: FieldLayout;
    if (original) {
      layout = { ...original, ...values, sections: cloneSections(original.sections) };
      if (original.assetType !== values.assetType) this.pruneToPool(layout);
    } else {
      const source = this.layouts.find((l) => l.id === e.copyFrom);
      layout = { id: '', ...values, sections: source ? cloneSections(source.sections) : this.blankSections() };
      this.pruneToPool(layout);
    }
    const request = toLayoutRequest(layout, this.allFields);
    this.saving = true;
    (original ? this.api.updateLayout(original.id, request) : this.api.createLayout(request)).subscribe({
      next: (dto) => {
        this.saving = false;
        const saved = this.fromDto(dto);
        if (original) {
          const keepDraft = this.selected === original && this.dirty;
          Object.assign(original, saved);
          if (this.selected === original) {
            if (keepDraft) this.pruneDraft(original.assetType);
            else this.select(original);
          }
          this.notify.emit(`${saved.name} updated.`);
        } else {
          this.layouts.push(saved);
          this.select(saved);
          this.notify.emit(`${saved.name} added.`);
        }
        if (this.layoutForm === e) this.layoutForm = null;
      },
      error: (err) => {
        this.saving = false;
        this.notify.emit(apiErrorMessage(err, 'Could not save the layout.'));
      },
    });
  }

  /** Fields that can stay on a layout of this asset type. */
  private keeperFor(assetType: string): (id: string) => boolean {
    const pool = new Set(this.poolFor(assetType).map((f) => f.id));
    return (id: string) => pool.has(id) || this.fieldById(id)?.scope === 'asset';
  }

  /** Category fields of another asset type can't stay on the layout after its type changes. */
  private pruneToPool(l: FieldLayout): void {
    const keep = this.keeperFor(l.assetType);
    l.sections.forEach((s) => (s.fieldIds = s.fieldIds.filter(keep)));
  }

  private pruneDraft(assetType: string): void {
    const keep = this.keeperFor(assetType);
    this.draft.forEach((s) => (s.fieldIds = s.fieldIds.filter(keep)));
  }

  duplicate(l: FieldLayout): void {
    this.openMenu = null;
    if (this.saving) return;
    let name = `${l.name} (Copy)`;
    for (let n = 2; this.layouts.some((x) => x.name.toLowerCase() === name.toLowerCase()); n++) name = `${l.name} (Copy ${n})`;
    this.saving = true;
    this.api.createLayout(toLayoutRequest({ ...l, name }, this.allFields)).subscribe({
      next: (dto) => {
        this.saving = false;
        const copy = this.fromDto(dto);
        const index = this.layouts.indexOf(l);
        this.layouts.splice(index >= 0 ? index + 1 : this.layouts.length, 0, copy);
        this.requestSelect(copy);
        this.notify.emit(`${name} created.`);
      },
      error: (err) => {
        this.saving = false;
        this.notify.emit(apiErrorMessage(err, 'Could not duplicate the layout.'));
      },
    });
  }

  setStatus(list: FieldLayout[], status: FieldStatus): void {
    this.openMenu = null;
    this.checked.clear();
    const targets = list.filter((l) => l.status !== status);
    if (!targets.length) return;
    runAll(targets, (l) => this.api.updateLayout(l.id, toLayoutRequest({ ...l, status }, this.allFields)).pipe(map(() => l))).subscribe((result) => {
      result.ok.forEach((l) => (l.status = status));
      const verb = status === 'Active' ? 'activated' : 'deactivated';
      const n = result.ok.length;
      const done = n === 1 ? `${result.ok[0].name} ${verb}.` : n ? `${n} layouts ${verb}.` : '';
      this.notify.emit([done, ...result.errors.slice(0, 1)].filter(Boolean).join(' '));
    });
  }

  askDelete(list: FieldLayout[]): void {
    this.openMenu = null;
    this.deleteTargets = list;
  }

  confirmDelete(): void {
    const targets = this.deleteTargets;
    this.deleteTargets = [];
    if (!targets.length) return;
    this.saving = true;
    runAll(targets, (l) => this.api.deleteLayout(l.id).pipe(map(() => l))).subscribe((result) => {
      this.saving = false;
      const deleted = result.ok;
      deleted.forEach((l) => {
        const index = this.layouts.indexOf(l);
        if (index >= 0) this.layouts.splice(index, 1);
        this.checked.delete(l);
      });
      if (this.selected && deleted.includes(this.selected)) this.select(this.layouts[0] ?? null);
      const done = deleted.length === 1 ? `${deleted[0].name} deleted.` : deleted.length ? `${deleted.length} layouts deleted.` : '';
      this.notify.emit([done, ...result.errors.slice(0, 1)].filter(Boolean).join(' '));
    });
  }

  // ===== Import / export (called from the page header) =====

  /** The API has no layout export, so the file is built from the loaded layouts (groups and fields by name). */
  exportLayouts(): void {
    const nameOf = (id: string) => this.fieldById(id)?.name;
    const rows = this.layouts.map((l) => ({
      name: l.name,
      assetType: l.assetType,
      description: l.description,
      status: l.status,
      sections: l.sections.map((s) => ({ group: s.group, fields: s.fieldIds.map(nameOf).filter(Boolean) })),
    }));
    const url = URL.createObjectURL(new Blob([JSON.stringify({ version: 1, layouts: rows }, null, 2)], { type: 'application/json' }));
    const link = document.createElement('a');
    link.href = url;
    link.download = 'field-layouts.json';
    link.click();
    URL.revokeObjectURL(url);
    this.notify.emit(`Exported ${rows.length} layout${rows.length === 1 ? '' : 's'}.`);
  }

  /** Creates layouts with new names through the API; sections keep only groups and fields that exist here. */
  importLayouts(parsed: unknown, fileName: string): void {
    const list: unknown[] = Array.isArray(parsed) ? parsed : Array.isArray((parsed as { layouts?: unknown[] })?.layouts) ? (parsed as { layouts: unknown[] }).layouts : [];
    const str = (v: unknown) => (typeof v === 'string' ? v.trim() : '');
    const obj = (v: unknown) => (v && typeof v === 'object' ? v : {}) as Record<string, unknown>;
    if (!list.length) {
      this.notify.emit(`No layouts found in ${fileName}.`);
      return;
    }
    const taken = new Set(this.layouts.map((l) => l.name.toLowerCase()));
    const toCreate: FieldLayout[] = [];
    let skipped = 0;
    for (const raw of list) {
      const r = obj(raw);
      const name = str(r['name']);
      if (!name || taken.has(name.toLowerCase())) {
        skipped++;
        continue;
      }
      taken.add(name.toLowerCase());
      const assetTypeId = this.typeIdOf(str(r['assetType']));
      const assetType = assetTypeId ? str(r['assetType']) : ALL_ASSETS;
      const pool = this.poolFor(assetType);
      const sections = (Array.isArray(r['sections']) ? (r['sections'] as unknown[]) : [])
        .map(obj)
        .map((s) => ({ s, g: this.groups.find((g) => g.name === str(s['group'])) }))
        .filter((x): x is { s: Record<string, unknown>; g: FieldGroup } => !!x.g)
        .map(({ s, g }) => ({
          groupId: g.id,
          group: g.name,
          fieldIds: (Array.isArray(s['fields']) ? (s['fields'] as unknown[]) : [])
            .map((n) => pool.find((f) => f.name === str(n))?.id)
            .filter((id): id is string => id !== undefined),
        }));
      toCreate.push({
        id: '',
        name,
        assetTypeId,
        assetType,
        description: str(r['description']).slice(0, DESCRIPTION_MAX),
        status: r['status'] === 'Inactive' ? 'Inactive' : 'Active',
        sections,
      });
    }
    if (!toCreate.length) {
      this.notify.emit(`Imported 0 layouts; skipped ${skipped} invalid or existing.`);
      return;
    }
    runAll(toCreate, (l) => this.api.createLayout(toLayoutRequest(l, this.allFields))).subscribe((result) => {
      result.ok.forEach((dto) => this.layouts.push(this.fromDto(dto)));
      if (!this.selected && this.layouts[0]) this.select(this.layouts[0]);
      skipped += result.errors.length;
      const added = result.ok.length;
      this.notify.emit(
        `Imported ${added} layout${added === 1 ? '' : 's'}${skipped ? `; skipped ${skipped} invalid or existing` : ''}.` +
          (result.errors.length ? ` ${result.errors[0]}` : ''),
      );
    });
  }

  lookupPlaceholder(f: CustomField): string {
    return f.defaultValue || (LOOKUP_SOURCES[f.lookupSource] ? `Select from ${f.lookupSource}…` : 'Select…');
  }
}
