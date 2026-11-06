import { Component, EventEmitter, HostListener, Input, OnInit, Output } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
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
  ASSET_TYPES,
  ASSET_TYPE_CATEGORY,
  CustomField,
  FieldGroup,
  FieldLayout,
  FieldStatus,
  LOOKUP_SOURCES,
  LayoutSection,
  SYSTEM_FIELDS,
} from '../custom-fields.model';

interface LayoutForm {
  original: FieldLayout | null;
  name: string;
  assetType: string;
  description: string;
  status: FieldStatus;
  /** New layouts only: id of a layout to copy the structure from, or 0 for a blank layout. */
  copyFrom: number;
}

type DragState =
  | { kind: 'field'; fieldId: number; from: { s: number; i: number } | null }
  | { kind: 'section'; index: number };

/** Section header colours, by position in the layout (matches the design's purple / blue / green sequence). */
const SECTION_TONES = ['violet', 'blue', 'green', 'amber', 'pink', 'teal', 'slate', 'yellow'];
const DESCRIPTION_MAX = 250;

function cloneSections(sections: LayoutSection[]): LayoutSection[] {
  return sections.map((s) => ({ group: s.group, fieldIds: [...s.fieldIds] }));
}

@Component({
  selector: 'app-cf-field-layouts',
  standalone: true,
  imports: [CommonModule, FormsModule, LucideAngularModule],
  templateUrl: './field-layouts.html',
  styleUrls: ['../asset-custom-fields.css', './field-layouts.css'],
})
export class CfFieldLayouts implements OnInit {
  @Input({ required: true }) layouts!: FieldLayout[];
  @Input({ required: true }) fields!: CustomField[];
  @Input({ required: true }) groups!: FieldGroup[];
  @Output() notify = new EventEmitter<string>();

  readonly icons = {
    CalendarDays, ChevronDown, ChevronLeft, ChevronRight, ChevronUp, Cog, Copy, EllipsisVertical, GripVertical, Inbox, Pencil,
    Plus, Power, PowerOff, Search, SquarePen, Trash2, X,
  };
  readonly assetTypes = ASSET_TYPES;
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
    if (this.layouts[0]) this.select(this.layouts[0]);
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

  fieldById(id: number): CustomField | undefined {
    return id < 0 ? SYSTEM_FIELDS.find((f) => f.id === id) : this.fields.find((f) => f.id === id);
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

  private get usedIds(): Set<number> {
    return new Set(this.draft.flatMap((s) => s.fieldIds));
  }

  isUsed(f: CustomField): boolean {
    return this.usedIds.has(f.id);
  }

  /** Fields a layout for this asset type may use: built-ins, active asset fields and the type's category fields. */
  private poolFor(assetType: string): CustomField[] {
    const category = ASSET_TYPE_CATEGORY[assetType] ?? '';
    return [
      ...SYSTEM_FIELDS,
      ...this.fields.filter(
        (f) => f.status === 'Active' && (f.scope === 'asset' || (f.scope === 'category' && !!category && f.appliesTo === category)),
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
    const present = new Set(this.draft.map((s) => s.group));
    return [...this.groups].sort((a, b) => a.order - b.order).filter((g) => g.status === 'Active' && !present.has(g.name));
  }

  // ===== Designer edits =====

  private changed(): void {
    this.dirty = true;
    this.saveError = '';
  }

  addSection(g: FieldGroup): void {
    this.draft.push({ group: g.name, fieldIds: [] });
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
    return this.poolFor(this.selected.assetType).filter((f) => f.id > 0 && f.group === s.group && !used.has(f.id));
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

  onFieldDragStart(event: DragEvent, fieldId: number, from: { s: number; i: number } | null): void {
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

  private placeField(fieldId: number, from: { s: number; i: number } | null, to: { s: number; i: number }): void {
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
    if (!this.selected) return;
    const used = this.usedIds;
    const missing = SYSTEM_FIELDS.filter((f) => !used.has(f.id));
    if (missing.length) {
      this.saveError = `${missing.map((f) => f.label).join(' and ')} must be on the layout.`;
      return;
    }
    this.selected.sections = cloneSections(this.draft);
    this.dirty = false;
    this.notify.emit(`${this.selected.name} saved.`);
  }

  // ===== Layout details (add / edit modal) =====

  openAdd(): void {
    this.openMenu = null;
    this.layoutForm = { original: null, name: '', assetType: ASSET_TYPES[0], description: '', status: 'Active', copyFrom: 0 };
    this.layoutSubmitted = false;
  }

  openEdit(l: FieldLayout): void {
    this.openMenu = null;
    this.layoutForm = { original: l, name: l.name, assetType: l.assetType, description: l.description, status: l.status, copyFrom: 0 };
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

  saveLayoutForm(): void {
    this.layoutSubmitted = true;
    const e = this.layoutForm;
    if (!e || this.layoutNameError) return;
    const values = { name: e.name.trim(), assetType: e.assetType, description: e.description.trim(), status: e.status };
    if (e.original) {
      const typeChanged = e.original.assetType !== values.assetType;
      Object.assign(e.original, values);
      if (typeChanged) this.pruneToPool(e.original);
      this.notify.emit(`${values.name} updated.`);
    } else {
      const source = this.layouts.find((l) => l.id === e.copyFrom);
      const created: FieldLayout = {
        id: Math.max(0, ...this.layouts.map((l) => l.id)) + 1,
        ...values,
        sections: source ? cloneSections(source.sections) : [{ group: 'General Information', fieldIds: SYSTEM_FIELDS.map((f) => f.id) }],
      };
      this.pruneToPool(created);
      this.layouts.push(created);
      this.select(created);
      this.notify.emit(`${values.name} added.`);
    }
    this.layoutForm = null;
  }

  /** Category fields of another asset type can't stay on the layout after its type changes. */
  private pruneToPool(l: FieldLayout): void {
    const pool = new Set(this.poolFor(l.assetType).map((f) => f.id));
    const keep = (id: number) => pool.has(id) || this.fieldById(id)?.scope === 'asset';
    l.sections.forEach((s) => (s.fieldIds = s.fieldIds.filter(keep)));
    if (l === this.selected) this.draft.forEach((s) => (s.fieldIds = s.fieldIds.filter(keep)));
  }

  duplicate(l: FieldLayout): void {
    this.openMenu = null;
    let name = `${l.name} (Copy)`;
    for (let n = 2; this.layouts.some((x) => x.name === name); n++) name = `${l.name} (Copy ${n})`;
    const copy: FieldLayout = { ...l, id: Math.max(0, ...this.layouts.map((x) => x.id)) + 1, name, sections: cloneSections(l.sections) };
    this.layouts.splice(this.layouts.indexOf(l) + 1, 0, copy);
    this.requestSelect(copy);
    this.notify.emit(`${name} created.`);
  }

  setStatus(list: FieldLayout[], status: FieldStatus): void {
    this.openMenu = null;
    list.forEach((l) => (l.status = status));
    this.checked.clear();
    const verb = status === 'Active' ? 'activated' : 'deactivated';
    this.notify.emit(list.length === 1 ? `${list[0].name} ${verb}.` : `${list.length} layouts ${verb}.`);
  }

  askDelete(list: FieldLayout[]): void {
    this.openMenu = null;
    this.deleteTargets = list;
  }

  confirmDelete(): void {
    const targets = this.deleteTargets;
    targets.forEach((l) => {
      this.layouts.splice(this.layouts.indexOf(l), 1);
      this.checked.delete(l);
    });
    if (this.selected && targets.includes(this.selected)) this.select(this.layouts[0] ?? null);
    this.deleteTargets = [];
    this.notify.emit(targets.length === 1 ? `${targets[0].name} deleted.` : `${targets.length} layouts deleted.`);
  }

  // ===== Import / export (called from the page header) =====

  exportLayouts(): void {
    const nameOf = (id: number) => this.fieldById(id)?.name;
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

  /** Adds layouts with new names; sections keep only groups and fields that exist here. */
  importLayouts(parsed: unknown, fileName: string): void {
    const list: unknown[] = Array.isArray(parsed) ? parsed : Array.isArray((parsed as { layouts?: unknown[] })?.layouts) ? (parsed as { layouts: unknown[] }).layouts : [];
    const str = (v: unknown) => (typeof v === 'string' ? v.trim() : '');
    const obj = (v: unknown) => (v && typeof v === 'object' ? v : {}) as Record<string, unknown>;
    let added = 0;
    let skipped = 0;
    for (const raw of list) {
      const r = obj(raw);
      const name = str(r['name']);
      const assetType = ASSET_TYPES.includes(str(r['assetType'])) ? str(r['assetType']) : 'All Assets';
      if (!name || this.layouts.some((l) => l.name.toLowerCase() === name.toLowerCase())) {
        skipped++;
        continue;
      }
      const pool = this.poolFor(assetType);
      const sections = (Array.isArray(r['sections']) ? (r['sections'] as unknown[]) : [])
        .map(obj)
        .filter((s) => this.groups.some((g) => g.name === str(s['group'])))
        .map((s) => ({
          group: str(s['group']),
          fieldIds: (Array.isArray(s['fields']) ? (s['fields'] as unknown[]) : [])
            .map((n) => pool.find((f) => f.name === str(n))?.id)
            .filter((id): id is number => id !== undefined),
        }));
      this.layouts.push({
        id: Math.max(0, ...this.layouts.map((l) => l.id)) + 1,
        name,
        assetType,
        description: str(r['description']).slice(0, DESCRIPTION_MAX),
        status: r['status'] === 'Inactive' ? 'Inactive' : 'Active',
        sections,
      });
      added++;
    }
    this.notify.emit(
      list.length
        ? `Imported ${added} layout${added === 1 ? '' : 's'}${skipped ? `; skipped ${skipped} invalid or existing` : ''}.`
        : `No layouts found in ${fileName}.`,
    );
  }

  lookupPlaceholder(f: CustomField): string {
    return f.defaultValue || (LOOKUP_SOURCES[f.lookupSource] ? `Select from ${f.lookupSource}…` : 'Select…');
  }
}
