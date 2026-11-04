import { Component, HostListener, OnInit, ViewChild, inject } from '@angular/core';
import { catchError, forkJoin, map, of } from 'rxjs';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import {
  LucideAngularModule,
  CalendarDays,
  Check,
  ChevronDown,
  ChevronLeft,
  ChevronRight,
  ChevronUp,
  Cog,
  Combine,
  Copy,
  Download,
  EllipsisVertical,
  Eye,
  FolderPlus,
  GripVertical,
  Inbox,
  LayoutList,
  Layers,
  ListTree,
  Plus,
  Power,
  PowerOff,
  Search,
  Settings,
  SquarePen,
  Trash2,
  Upload,
  X,
} from 'lucide-angular';
import {
  BUILT_IN_FIELDS,
  CustomField,
  FieldGroup,
  FieldLayout,
  FieldScope,
  FieldStatus,
  FieldType,
  LOOKUP_SOURCES,
  typeClass,
} from './custom-fields.model';
import { CfFieldLayouts } from './field-layouts/field-layouts';
import {
  CategoryOption,
  CustomFieldsService,
  ImportResult,
  SaveFieldGroupRequest,
  runAll,
  toFieldGroup,
  toFieldLayout,
  toFieldRequest,
} from './custom-fields.service';
import { apiErrorMessage } from '../services/asset-api';


type ListTab = FieldScope | 'compound';
type PageTab = ListTab | 'groups' | 'layouts';

type FieldForm = Omit<CustomField, 'id' | 'order' | 'options' | 'parts' | 'partDefs' | 'appliesToId' | 'groupId'> & { optionsText: string; partsText: string };

interface GroupForm {
  original: FieldGroup | null;
  name: string;
  description: string;
  status: FieldStatus;
  /** Assigned fields in display order; applied to the fields on save. */
  fieldIds: string[];
}

interface TabMeta {
  label: string;
  /** Header title suffix and add-button noun; null keeps the plain "Custom Fields" header. */
  noun: string | null;
  subtitle: string;
}

const FIELD_TYPES: FieldType[] = ['Text', 'Text Area', 'Number', 'Date', 'Dropdown', 'Checkbox', 'Compound', 'Lookup'];
const NAME_PATTERN = /^[a-z][a-z0-9_]*$/;
const DESCRIPTION_MAX = 500;
const TOOLTIP_MAX = 250;
const GROUP_DESCRIPTION_MAX = 250;
const ALL_COMPONENTS = 'All Components';
/** The API keeps component type as free text and has no list of types; these are offered, plus any already in use. */
const COMPONENT_TYPES = ['Engine', 'Alternator', 'Control Panel', 'Cooling System', 'Fuel System', 'Battery', 'Exhaust System'];
/** Group new asset fields go into by default, when it exists. */
const DEFAULT_GROUP = 'General Information';

function importSummary(result: ImportResult, noun: string): string {
  const parts = [`${result.created} ${noun}${result.created === 1 ? '' : 's'} created`, `${result.updated} updated`];
  const errors = result.errors ?? [];
  if (errors.length) parts.push(`${errors.length} skipped (${errors[0].name ? errors[0].name + ': ' : ''}${errors[0].message})`);
  return `Import finished: ${parts.join(', ')}.`;
}

const TAB_META: Record<PageTab, TabMeta> = {
  asset: { label: 'Asset Fields', noun: null, subtitle: 'Create and manage additional fields for asset records to capture industry or organization specific information.' },
  component: { label: 'Component Fields', noun: 'Component', subtitle: 'Create and manage custom fields for asset components to capture component specific information.' },
  category: { label: 'Category Specific Fields', noun: 'Category', subtitle: 'Create fields that only appear on assets of a specific asset category.' },
  compound: { label: 'Compound Fields', noun: 'Compound', subtitle: 'Combine several related values into one field, such as dimensions or coordinates.' },
  groups: { label: 'Field Groups', noun: 'Groups', subtitle: 'Organize custom fields into logical groups for easier data entry and management.' },
  layouts: { label: 'Field Layouts', noun: 'Layouts', subtitle: 'Design and manage field layouts to organize how custom fields are presented for different asset types.' },
};

/** "Warranty Expiry Date" → "warranty_expiry_date". */
function toFieldName(label: string): string {
  return label
    .toLowerCase()
    .replace(/\(.*?\)/g, ' ')
    .replace(/[^a-z0-9]+/g, '_')
    .replace(/^_+|_+$/g, '')
    .replace(/^(\d)/, 'f_$1');
}

function splitLines(text: string): string[] {
  return [...new Set(text.split('\n').map((o) => o.trim()).filter(Boolean))];
}

function downloadJson(data: unknown, fileName: string): void {
  const url = URL.createObjectURL(new Blob([JSON.stringify(data, null, 2)], { type: 'application/json' }));
  const link = document.createElement('a');
  link.href = url;
  link.download = fileName;
  link.click();
  URL.revokeObjectURL(url);
}

@Component({
  selector: 'app-asset-custom-fields',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, LucideAngularModule, CfFieldLayouts],
  templateUrl: './asset-custom-fields.html',
  styleUrl: './asset-custom-fields.css',
})
export class AssetCustomFields implements OnInit {
  private readonly api = inject(CustomFieldsService);

  readonly icons = {
    CalendarDays, Check, ChevronDown, ChevronLeft, ChevronRight, ChevronUp, Cog, Combine, Copy, Download, EllipsisVertical, Eye,
    FolderPlus, GripVertical, Inbox, LayoutList, Layers, ListTree, Plus, Power, PowerOff, Search, Settings, SquarePen, Trash2, Upload, X,
  };

  @ViewChild(CfFieldLayouts) private layoutsView?: CfFieldLayouts;

  // Compound Fields is not shown as its own tab; compound fields are listed with the other fields of their scope
  readonly tabs = (Object.keys(TAB_META) as PageTab[]).filter((key) => key !== 'compound').map((key) => ({ key, label: TAB_META[key].label }));
  readonly fieldTypes = FIELD_TYPES;
  readonly allComponents = ALL_COMPONENTS;
  readonly lookupSources = Object.keys(LOOKUP_SOURCES);
  readonly pageSizeOptions = [10, 25, 50];
  readonly descriptionMax = DESCRIPTION_MAX;
  readonly tooltipMax = TOOLTIP_MAX;
  readonly groupDescriptionMax = GROUP_DESCRIPTION_MAX;
  readonly typeClass = typeClass;
  readonly scopeOptions: { value: FieldScope; label: string }[] = [
    { value: 'asset', label: 'Asset' },
    { value: 'component', label: 'Component' },
    { value: 'category', label: 'Asset Category' },
  ];

  fields: CustomField[] = [];
  groups: FieldGroup[] = [];
  layouts: FieldLayout[] = [];
  /** Standard asset fields layouts can place (from the API; the built-in list until it loads). */
  builtIns: CustomField[] = BUILT_IN_FIELDS;
  categories: CategoryOption[] = [];
  activeTab: PageTab = 'asset';

  loading = true;
  loadError = '';
  /** A save or delete is in flight; repeat submits are ignored. */
  saving = false;

  // List filters (shared by the field and group lists)
  search = '';
  typeFilter: '' | FieldType = '';
  statusFilter: '' | FieldStatus = '';
  groupFilter = '';
  /** Asset category (category tab) or component type (component tab). */
  appliesToFilter = '';
  usedInFilter: '' | FieldScope = '';
  page = 1;
  pageSize = 10;
  checked = new Set<string>();
  checkedGroups = new Set<FieldGroup>();
  openMenu: string | null = null;

  // Field side panel (opens on the first field once loaded, as in the design)
  panelOpen = false;
  editing: CustomField | null = null;
  form: FieldForm = this.emptyForm('asset');
  formSubmitted = false;
  private nameTouched = false;
  addingGroup = false;
  newGroupName = '';

  // Group side panel
  groupForm: GroupForm | null = null;
  groupSubmitted = false;
  assignSearch = '';
  private dragIndex: number | null = null;
  dropIndex: number | null = null;
  // "Add Fields" picker
  pickerOpen = false;
  pickerSearch = '';
  pickerChecked = new Set<string>();

  deleteTargets: CustomField[] = [];
  groupDeleteTargets: FieldGroup[] = [];

  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;

  ngOnInit(): void {
    this.api.categories().subscribe({
      next: (categories) => (this.categories = categories),
      error: (err) => this.showToast(apiErrorMessage(err, 'Could not load asset categories.')),
    });
    this.load(true);
  }

  // ===== Loading =====

  /** Loads fields, groups and layouts; open panels are pointed at the reloaded records. */
  load(openFirst = false): void {
    this.loading = true;
    this.loadError = '';
    forkJoin({
      fields: this.api.listFields(),
      groups: this.api.listGroups(),
      layouts: this.api.listLayouts(),
      builtIns: this.api.builtInFields().pipe(catchError(() => of(BUILT_IN_FIELDS))),
    }).subscribe({
      next: ({ fields, groups, layouts, builtIns }) => {
        this.fields = fields;
        this.groups = groups;
        this.builtIns = builtIns.length ? builtIns : BUILT_IN_FIELDS;
        this.layouts = layouts.map((l) => toFieldLayout(l, [...this.builtIns, ...fields]));
        this.loading = false;
        this.resyncSelection();
        if (openFirst) this.openFirstOfTab();
      },
      error: (err) => {
        this.loading = false;
        this.loadError = apiErrorMessage(err, 'Could not load custom fields.');
      },
    });
  }

  reload(): void {
    this.load(!this.panelOpen && !this.groupForm);
  }

  /** After a reload, panels and selections refer to the new objects (by id). */
  private resyncSelection(): void {
    if (this.editing) {
      const current = this.fields.find((f) => f.id === this.editing!.id);
      if (current) this.editing = current;
      else this.closePanel();
    }
    if (this.groupForm?.original) {
      const current = this.groups.find((g) => g.id === this.groupForm!.original!.id);
      if (current) this.groupForm.original = current;
      else this.groupForm = null;
    }
    const ids = new Set(this.fields.map((f) => f.id));
    this.checked.forEach((id) => !ids.has(id) && this.checked.delete(id));
    this.checkedGroups.clear();
  }

  // ===== Lookups =====

  /** Asset category names for the category selects (category fields are saved by category id). */
  get assetCategories(): string[] {
    return [...new Set(this.categories.map((c) => c.name))];
  }

  get componentTypes(): string[] {
    const inUse = this.fields.filter((f) => f.scope === 'component' && f.appliesTo && f.appliesTo !== ALL_COMPONENTS).map((f) => f.appliesTo);
    return [...new Set([...COMPONENT_TYPES, ...inUse])];
  }

  private categoryIdOf(name: string): string {
    return this.categories.find((c) => c.name === name)?.id ?? '';
  }

  private groupIdOf(name: string): string {
    return this.groups.find((g) => g.name === name)?.id ?? '';
  }

  private get defaultGroupName(): string {
    return this.groups.find((g) => g.name === DEFAULT_GROUP)?.name ?? '';
  }

  // ===== Tabs =====

  get meta(): TabMeta {
    return TAB_META[this.activeTab];
  }

  get isFieldTab(): boolean {
    return this.activeTab !== 'groups' && this.activeTab !== 'layouts';
  }

  /** Scope new fields get on this tab (the compound tab defaults to asset). */
  get scope(): FieldScope {
    return this.activeTab === 'component' || this.activeTab === 'category' ? this.activeTab : 'asset';
  }

  get listNoun(): string {
    return this.activeTab === 'asset' ? 'custom' : this.activeTab;
  }

  /** "Fields", "Groups" or "Layouts", for the import/export buttons. */
  get headerNoun(): string {
    return this.isFieldTab ? 'Fields' : this.meta.noun!;
  }

  get addLabel(): string {
    if (this.activeTab === 'groups') return 'Add Field Group';
    if (this.activeTab === 'layouts') return 'Add Field Layout';
    return this.meta.noun ? `Add ${this.meta.noun} Field` : 'Add Custom Field';
  }

  selectTab(tab: PageTab): void {
    if (tab === this.activeTab) return;
    this.activeTab = tab;
    this.search = '';
    this.typeFilter = '';
    this.statusFilter = '';
    this.groupFilter = '';
    this.appliesToFilter = '';
    this.usedInFilter = '';
    this.page = 1;
    this.checked.clear();
    this.checkedGroups.clear();
    this.openMenu = null;
    this.openFirstOfTab();
  }

  /** Each tab opens with a record in its side panel, as in the designs. */
  private openFirstOfTab(): void {
    this.closePanel();
    this.groupForm = null;
    if (this.isFieldTab) {
      const first = this.tabFields[0];
      if (first) this.openEdit(first);
    } else if (this.activeTab === 'groups') {
      const g = this.sortedGroups[0];
      if (g) this.openGroupEdit(g);
    }
  }

  onAdd(): void {
    if (this.activeTab === 'groups') this.openGroupEdit();
    else if (this.activeTab === 'layouts') this.layoutsView?.openAdd();
    else this.openAdd();
  }

  onExport(): void {
    if (this.activeTab === 'groups') this.exportGroups();
    else if (this.activeTab === 'layouts') this.layoutsView?.exportLayouts();
    else this.exportFields();
  }

  async onImportPicked(event: Event): Promise<void> {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    let parsed: unknown;
    try {
      parsed = JSON.parse(await file.text());
    } catch {
      this.showToast(`${file.name} isn't valid JSON.`);
      return;
    }
    if (this.activeTab === 'groups') this.importGroups(parsed, file.name);
    else if (this.activeTab === 'layouts') this.layoutsView?.importLayouts(parsed, file.name);
    else this.importFields(parsed, file.name);
  }

  // ===== Field list =====

  get tabFields(): CustomField[] {
    return this.activeTab === 'compound'
      ? this.fields.filter((f) => f.type === 'Compound')
      : this.fields.filter((f) => f.scope === this.activeTab);
  }

  get filtered(): CustomField[] {
    const q = this.search.trim().toLowerCase();
    return this.tabFields.filter(
      (f) =>
        (!q || [f.name, f.label, f.group, f.description, f.appliesTo, ...f.parts].some((v) => v.toLowerCase().includes(q))) &&
        (!this.typeFilter || f.type === this.typeFilter) &&
        (!this.statusFilter || f.status === this.statusFilter) &&
        (!this.groupFilter || f.group === this.groupFilter) &&
        // A field for all components also applies to the filtered component type.
        (!this.appliesToFilter || f.appliesTo === this.appliesToFilter || f.appliesTo === ALL_COMPONENTS) &&
        (!this.usedInFilter || f.scope === this.usedInFilter),
    );
  }

  /** Rows of whichever list the tab shows; drives paging for both fields and groups. */
  private get listLength(): number {
    return this.activeTab === 'groups' ? this.filteredGroups.length : this.filtered.length;
  }

  get totalPages(): number {
    return Math.max(1, Math.ceil(this.listLength / this.pageSize));
  }

  get currentPage(): number {
    return Math.min(this.page, this.totalPages);
  }

  get pageRows(): CustomField[] {
    const start = (this.currentPage - 1) * this.pageSize;
    return this.filtered.slice(start, start + this.pageSize);
  }

  get rangeStart(): number {
    return this.listLength ? (this.currentPage - 1) * this.pageSize + 1 : 0;
  }

  get rangeEnd(): number {
    return Math.min(this.currentPage * this.pageSize, this.listLength);
  }

  get pageList(): number[] {
    return Array.from({ length: this.totalPages }, (_, i) => i + 1);
  }

  goToPage(p: number): void {
    if (p >= 1 && p <= this.totalPages) this.page = p;
  }

  onFilterChange(): void {
    this.page = 1;
  }

  get allChecked(): boolean {
    return this.pageRows.length > 0 && this.pageRows.every((f) => this.checked.has(f.id));
  }

  get someChecked(): boolean {
    return !this.allChecked && this.pageRows.some((f) => this.checked.has(f.id));
  }

  toggleAll(): void {
    const on = !this.allChecked;
    this.pageRows.forEach((f) => (on ? this.checked.add(f.id) : this.checked.delete(f.id)));
  }

  toggleRow(id: string): void {
    if (this.checked.has(id)) this.checked.delete(id);
    else this.checked.add(id);
  }

  get checkedFields(): CustomField[] {
    return this.fields.filter((f) => this.checked.has(f.id));
  }

  setStatusForChecked(status: FieldStatus): void {
    const rows = this.checkedFields.filter((f) => f.status !== status);
    this.checked.clear();
    if (!rows.length) return;
    this.saving = true;
    runAll(rows, (f) => this.api.updateField(f.id, toFieldRequest({ ...f, status }))).subscribe((result) => {
      this.saving = false;
      result.ok.forEach((saved) => this.replaceField(saved));
      const verb = status === 'Active' ? 'activated' : 'deactivated';
      const n = result.ok.length;
      this.showToast(`${n} field${n === 1 ? '' : 's'} ${verb}.${result.errors.length ? ` ${result.errors.length} failed: ${result.errors[0]}` : ''}`);
    });
  }

  usedIn(f: CustomField): string {
    return f.scope === 'asset' ? 'Asset' : f.scope === 'component' ? `Component · ${f.appliesTo}` : `Category · ${f.appliesTo}`;
  }

  /** Puts a saved field in place of its old copy (or adds it), keeping the open panel on it. */
  private replaceField(saved: CustomField): void {
    const index = this.fields.findIndex((f) => f.id === saved.id);
    if (index >= 0) this.fields[index] = saved;
    else this.fields.push(saved);
    if (this.editing?.id === saved.id) {
      this.editing = saved;
      this.form.status = saved.status;
    }
  }

  // ===== Menus =====

  toggleMenu(key: string, event: MouseEvent): void {
    event.stopPropagation();
    this.openMenu = this.openMenu === key ? null : key;
  }

  @HostListener('document:click')
  closeMenu(): void {
    this.openMenu = null;
  }

  @HostListener('document:keydown.escape')
  onEscape(): void {
    this.openMenu = null;
    this.deleteTargets = [];
    this.groupDeleteTargets = [];
    this.pickerOpen = false;
  }

  // ===== Field side panel =====

  private defaultAppliesTo(scope: FieldScope): string {
    if (scope === 'category') return (this.activeTab === 'category' && this.appliesToFilter) || this.assetCategories[0] || '';
    if (scope === 'component') return (this.activeTab === 'component' && this.appliesToFilter) || ALL_COMPONENTS;
    return '';
  }

  private emptyForm(scope: FieldScope, type: FieldType = 'Text'): FieldForm {
    return {
      scope,
      appliesTo: this.defaultAppliesTo(scope),
      name: '',
      label: '',
      type,
      group: scope === 'component' ? '' : this.defaultGroupName,
      required: false,
      showInDetails: true,
      searchable: false,
      defaultValue: '',
      optionsText: '',
      partsText: '',
      lookupSource: type === 'Lookup' ? this.lookupSources[0] : '',
      description: '',
      tooltip: '',
      status: 'Active',
    };
  }

  get panelTitle(): string {
    const noun = this.activeTab === 'compound' ? 'Compound' : this.form.scope === 'asset' ? 'Custom' : this.form.scope === 'component' ? 'Component' : 'Category';
    return `${this.editing ? 'Edit' : 'Add'} ${noun} Field`;
  }

  /** The API keeps a field's type once it is created; the compound tab only holds compound fields. */
  get typeLocked(): boolean {
    return this.activeTab === 'compound' || !!this.editing;
  }

  get isComponentForm(): boolean {
    return this.form.scope === 'component';
  }

  openAdd(): void {
    this.openMenu = null;
    if (!this.isFieldTab) this.selectTab('asset');
    this.editing = null;
    this.form = this.emptyForm(this.scope, this.activeTab === 'compound' ? 'Compound' : 'Text');
    this.nameTouched = false;
    this.formSubmitted = false;
    this.addingGroup = false;
    this.panelOpen = true;
  }

  openEdit(f: CustomField, duplicate = false): void {
    this.openMenu = null;
    this.editing = duplicate ? null : f;
    this.form = {
      scope: f.scope,
      appliesTo: f.appliesTo,
      name: duplicate ? this.uniqueCopyName(f) : f.name,
      label: duplicate ? `${f.label} (Copy)` : f.label,
      type: f.type,
      group: f.group,
      required: f.required,
      showInDetails: f.showInDetails,
      searchable: f.searchable,
      defaultValue: f.defaultValue,
      optionsText: f.options.join('\n'),
      partsText: f.parts.join('\n'),
      lookupSource: f.lookupSource,
      description: f.description,
      tooltip: f.tooltip,
      status: f.status,
    };
    this.nameTouched = true;
    this.formSubmitted = false;
    this.addingGroup = false;
    this.panelOpen = true;
  }

  private uniqueCopyName(f: CustomField): string {
    let n = 2;
    while (this.isNameTaken(`${f.name}_${n}`, f.scope, f.appliesTo, null)) n++;
    return `${f.name}_${n}`;
  }

  closePanel(): void {
    this.panelOpen = false;
    this.editing = null;
  }

  onLabelChange(): void {
    if (!this.nameTouched) this.form.name = toFieldName(this.form.label);
  }

  onNameInput(): void {
    this.nameTouched = true;
  }

  onScopeChange(): void {
    this.form.appliesTo = this.defaultAppliesTo(this.form.scope);
    this.form.group = this.form.scope === 'component' ? '' : this.form.group || this.defaultGroupName;
  }

  onTypeChange(): void {
    // A default that doesn't fit the new type would be rejected later; clear it.
    this.form.defaultValue = this.form.type === 'Checkbox' ? 'false' : '';
    if (this.form.type === 'Lookup' && !this.form.lookupSource) this.form.lookupSource = this.lookupSources[0];
  }

  get formOptions(): string[] {
    return splitLines(this.form.optionsText);
  }

  get formParts(): string[] {
    return splitLines(this.form.partsText);
  }

  lookupValues(source: string): string[] {
    return LOOKUP_SOURCES[source] ?? [];
  }

  /**
   * Same rule as the API: asset and category fields share one set of names (they appear on the same asset
   * forms) and can't reuse a standard asset field's name; component fields have their own set.
   */
  isNameTaken(name: string, scope: FieldScope, _appliesTo: string, except: CustomField | null): boolean {
    const componentSide = scope === 'component';
    if (!componentSide && this.builtIns.some((f) => f.name === name)) return true;
    return this.fields.some((f) => f.id !== except?.id && (f.scope === 'component') === componentSide && f.name === name);
  }

  get nameError(): string | null {
    const name = this.form.name.trim();
    if (!name) return 'Field name is required.';
    if (!NAME_PATTERN.test(name)) return 'Use lowercase letters, digits and underscores, starting with a letter.';
    if (this.isNameTaken(name, this.form.scope, this.form.appliesTo, this.editing)) return 'Another field already uses this name.';
    return null;
  }

  get defaultError(): string | null {
    const v = this.form.defaultValue.trim();
    if (!v) return null;
    if (this.form.type === 'Number' && !Number.isFinite(Number(v))) return 'Default must be a number.';
    if (this.form.type === 'Dropdown' && !this.formOptions.includes(v)) return 'Default must be one of the options.';
    return null;
  }

  get optionsError(): string | null {
    return this.form.type === 'Dropdown' && this.formOptions.length < 2 ? 'Add at least two options, one per line.' : null;
  }

  get partsError(): string | null {
    return this.form.type === 'Compound' && this.formParts.length < 2 ? 'Add at least two sub-fields, one per line.' : null;
  }

  saveField(): void {
    this.formSubmitted = true;
    const f = this.form;
    if (this.saving) return;
    if (this.nameError || !f.label.trim() || this.defaultError || this.optionsError || this.partsError) return;
    if (f.description.length > DESCRIPTION_MAX || f.tooltip.length > TOOLTIP_MAX) return;
    if (f.scope !== 'asset' && !f.appliesTo) return;

    const appliesToId = f.scope === 'category' ? this.categoryIdOf(f.appliesTo) : '';
    if (f.scope === 'category' && !appliesToId) {
      this.showToast(`Asset category "${f.appliesTo}" was not found. Pick another category.`);
      return;
    }
    const group = f.scope === 'component' ? '' : f.group;

    const values: CustomField = {
      id: this.editing?.id ?? '',
      scope: f.scope,
      appliesTo: f.scope === 'asset' ? '' : f.appliesTo,
      appliesToId,
      name: f.name.trim(),
      label: f.label.trim(),
      type: f.type,
      group,
      groupId: group ? this.groupIdOf(group) : '',
      required: f.required,
      showInDetails: f.showInDetails,
      searchable: f.searchable,
      defaultValue: f.type === 'Compound' ? '' : f.defaultValue.trim(),
      options: f.type === 'Dropdown' ? this.formOptions : [],
      parts: f.type === 'Compound' ? this.formParts : [],
      partDefs: this.editing?.partDefs ?? [],
      lookupSource: f.type === 'Lookup' ? f.lookupSource : '',
      description: f.description.trim(),
      tooltip: f.tooltip.trim(),
      status: f.status,
      order: this.editing?.order ?? 0,
    };

    const editing = this.editing;
    const request = toFieldRequest(values);
    this.saving = true;
    (editing ? this.api.updateField(editing.id, request) : this.api.createField(request)).subscribe({
      next: (saved) => {
        this.saving = false;
        this.replaceField(saved);
        // Still showing the field that was saved: keep the panel on it.
        if (this.panelOpen && this.editing === editing) {
          this.editing = saved;
          this.nameTouched = true;
        }
        this.formSubmitted = false;
        this.showToast(`${saved.label} ${editing ? 'saved' : 'added'}.`);
      },
      error: (err) => {
        this.saving = false;
        this.showToast(apiErrorMessage(err, 'Could not save the field.'));
      },
    });
  }

  addGroupInline(): void {
    const name = this.newGroupName.trim();
    if (!name || this.saving) return;
    const existing = this.groups.find((g) => g.name.toLowerCase() === name.toLowerCase());
    if (existing) {
      this.form.group = existing.name;
      this.newGroupName = '';
      this.addingGroup = false;
      return;
    }
    this.saving = true;
    this.api.createGroup({ groupName: name, description: null, status: 'Active', fieldIds: null }).subscribe({
      next: (dto) => {
        this.saving = false;
        this.groups.push(toFieldGroup(dto, this.groups.length));
        this.form.group = dto.groupName;
        this.newGroupName = '';
        this.addingGroup = false;
      },
      error: (err) => {
        this.saving = false;
        this.showToast(apiErrorMessage(err, 'Could not add the group.'));
      },
    });
  }

  toggleStatus(f: CustomField): void {
    this.openMenu = null;
    const status: FieldStatus = f.status === 'Active' ? 'Inactive' : 'Active';
    this.api.updateField(f.id, toFieldRequest({ ...f, status })).subscribe({
      next: (saved) => {
        this.replaceField(saved);
        this.showToast(`${saved.label} ${saved.status === 'Active' ? 'activated' : 'deactivated'}.`);
      },
      error: (err) => this.showToast(apiErrorMessage(err, `Could not update ${f.label}.`)),
    });
  }

  // ===== Delete fields =====

  askDelete(targets: CustomField[]): void {
    this.openMenu = null;
    this.deleteTargets = targets;
  }

  confirmDelete(): void {
    const targets = this.deleteTargets;
    this.deleteTargets = [];
    if (!targets.length) return;
    this.saving = true;
    runAll(targets, (f) => this.api.deleteField(f.id).pipe(map(() => f.id))).subscribe((result) => {
      this.saving = false;
      const ids = new Set(result.ok);
      this.fields = this.fields.filter((f) => !ids.has(f.id));
      ids.forEach((id) => this.checked.delete(id));
      if (this.groupForm) this.groupForm.fieldIds = this.groupForm.fieldIds.filter((id) => !ids.has(id));
      if (this.editing && ids.has(this.editing.id)) this.closePanel();
      const deleted = targets.filter((f) => ids.has(f.id));
      const done = deleted.length === 1 ? `${deleted[0].label} deleted.` : deleted.length ? `${deleted.length} fields deleted.` : '';
      this.showToast([done, ...result.errors.slice(0, 1)].filter(Boolean).join(' '));
    });
  }

  // ===== Field import / export =====

  private exportFields(): void {
    const tab = this.activeTab;
    this.api.exportFields(tab === 'compound' ? undefined : this.scope).subscribe({
      next: (items) => {
        const rows = tab === 'compound' ? items.filter((i) => i.fieldType === 'Compound') : items;
        downloadJson(rows, `custom-fields-${tab}.json`);
        this.showToast(`Exported ${rows.length} field${rows.length === 1 ? '' : 's'}.`);
      },
      error: (err) => this.showToast(apiErrorMessage(err, 'Could not export fields.')),
    });
  }

  /** Imports a file produced by Export Fields; fields are matched by name (new ones created, existing ones updated). */
  private importFields(parsed: unknown, fileName: string): void {
    const list: unknown[] = Array.isArray(parsed) ? parsed : Array.isArray((parsed as { fields?: unknown[] })?.fields) ? (parsed as { fields: unknown[] }).fields : [];
    if (!list.length) {
      this.showToast(`No fields found in ${fileName}.`);
      return;
    }
    this.api.importFields(list, this.scope).subscribe({
      next: (result) => {
        this.showToast(importSummary(result, 'field'));
        this.load();
      },
      error: (err) => this.showToast(apiErrorMessage(err, `Could not import ${fileName}.`)),
    });
  }

  // ===== Field groups list =====

  get sortedGroups(): FieldGroup[] {
    return [...this.groups].sort((a, b) => a.order - b.order);
  }

  get filteredGroups(): FieldGroup[] {
    const q = this.search.trim().toLowerCase();
    return this.sortedGroups.filter(
      (g) => (!q || [g.name, g.description].some((v) => v.toLowerCase().includes(q))) && (!this.statusFilter || g.status === this.statusFilter),
    );
  }

  get groupPageRows(): FieldGroup[] {
    const start = (this.currentPage - 1) * this.pageSize;
    return this.filteredGroups.slice(start, start + this.pageSize);
  }

  get allGroupsChecked(): boolean {
    return this.groupPageRows.length > 0 && this.groupPageRows.every((g) => this.checkedGroups.has(g));
  }

  get someGroupsChecked(): boolean {
    return !this.allGroupsChecked && this.groupPageRows.some((g) => this.checkedGroups.has(g));
  }

  toggleAllGroups(): void {
    const on = !this.allGroupsChecked;
    this.groupPageRows.forEach((g) => (on ? this.checkedGroups.add(g) : this.checkedGroups.delete(g)));
  }

  get checkedGroupList(): FieldGroup[] {
    return [...this.checkedGroups];
  }

  toggleGroupRow(g: FieldGroup): void {
    if (this.checkedGroups.has(g)) this.checkedGroups.delete(g);
    else this.checkedGroups.add(g);
  }

  /** Field groups organise the asset form, so component fields aren't counted. */
  groupFields(group: string): CustomField[] {
    return this.fields.filter((f) => f.scope !== 'component' && f.group === group).sort((a, b) => a.order - b.order);
  }

  setGroupStatus(groups: FieldGroup[], status: FieldStatus): void {
    this.openMenu = null;
    this.checkedGroups.clear();
    const targets = groups.filter((g) => g.status !== status);
    if (!targets.length) return;
    const body = (g: FieldGroup): SaveFieldGroupRequest => ({ groupName: g.name, description: g.description || null, status, fieldIds: null });
    runAll(targets, (g) => this.api.updateGroup(g.id, body(g))).subscribe((result) => {
      result.ok.forEach((dto) => {
        const g = this.groups.find((x) => x.id === dto.id);
        if (g) g.status = dto.status === 'Inactive' ? 'Inactive' : 'Active';
        if (this.groupForm?.original === g) this.groupForm!.status = status;
      });
      const verb = status === 'Active' ? 'activated' : 'deactivated';
      const n = result.ok.length;
      const done = n === 1 ? `${result.ok[0].groupName} ${verb}.` : `${n} groups ${verb}.`;
      this.showToast([n ? done : '', ...result.errors.slice(0, 1)].filter(Boolean).join(' '));
    });
  }

  // ===== Group side panel =====

  openGroupEdit(g?: FieldGroup): void {
    this.openMenu = null;
    this.groupForm = {
      original: g ?? null,
      name: g?.name ?? '',
      description: g?.description ?? '',
      status: g?.status ?? 'Active',
      fieldIds: g ? this.groupFields(g.name).map((f) => f.id) : [],
    };
    this.groupSubmitted = false;
    this.assignSearch = '';
  }

  closeGroupPanel(): void {
    this.groupForm = null;
  }

  get groupNameError(): string | null {
    const e = this.groupForm;
    if (!e) return null;
    const name = e.name.trim();
    if (!name) return 'Group name is required.';
    if (this.groups.some((g) => g !== e.original && g.name.toLowerCase() === name.toLowerCase())) return 'A group with this name already exists.';
    return null;
  }

  fieldById(id: string): CustomField | undefined {
    return this.fields.find((f) => f.id === id);
  }

  /** Assigned fields in draft order, narrowed by the panel search; `index` is the position in the full draft. */
  get assignedRows(): { field: CustomField; index: number }[] {
    const q = this.assignSearch.trim().toLowerCase();
    return (this.groupForm?.fieldIds ?? [])
      .map((id, index) => ({ field: this.fieldById(id)!, index }))
      .filter((r) => r.field && (!q || [r.field.name, r.field.label, r.field.type].some((v) => v.toLowerCase().includes(q))));
  }

  unassign(index: number): void {
    this.groupForm?.fieldIds.splice(index, 1);
  }

  moveAssigned(from: number, to: number): void {
    const ids = this.groupForm?.fieldIds;
    if (!ids || to < 0 || to >= ids.length || from === to) return;
    const [id] = ids.splice(from, 1);
    ids.splice(to, 0, id);
  }

  onAssignDragStart(event: DragEvent, index: number): void {
    this.dragIndex = index;
    event.dataTransfer?.setData('text/plain', String(index));
    if (event.dataTransfer) event.dataTransfer.effectAllowed = 'move';
  }

  onAssignDragOver(event: DragEvent, index: number): void {
    if (this.dragIndex === null) return;
    event.preventDefault();
    this.dropIndex = index;
  }

  onAssignDrop(event: DragEvent, index: number): void {
    event.preventDefault();
    if (this.dragIndex !== null) this.moveAssigned(this.dragIndex, index);
    this.onAssignDragEnd();
  }

  onAssignDragEnd(): void {
    this.dragIndex = null;
    this.dropIndex = null;
  }

  /** Saves the group and its field assignment (in order); fields removed from the draft become unassigned. */
  saveGroup(): void {
    this.groupSubmitted = true;
    const e = this.groupForm;
    if (!e || this.saving || this.groupNameError || e.description.length > GROUP_DESCRIPTION_MAX) return;
    const body: SaveFieldGroupRequest = {
      groupName: e.name.trim(),
      description: e.description.trim() || null,
      status: e.status,
      fieldIds: [...e.fieldIds],
    };
    this.saving = true;
    (e.original ? this.api.updateGroup(e.original.id, body) : this.api.createGroup(body)).subscribe({
      next: (dto) => {
        this.saving = false;
        const saved = toFieldGroup(dto, e.original?.order ?? this.groups.length);
        if (e.original) Object.assign(e.original, saved);
        else this.groups.push(saved);
        if (this.groupForm === e) e.original = e.original ?? saved;
        this.groupSubmitted = false;
        this.showToast(`Group "${saved.name}" saved.`);
        // Field assignments (and layout section names on a rename) changed on the server.
        this.load();
      },
      error: (err) => {
        this.saving = false;
        this.showToast(apiErrorMessage(err, 'Could not save the group.'));
      },
    });
  }

  // ===== "Add Fields" picker =====

  openPicker(): void {
    this.pickerOpen = true;
    this.pickerSearch = '';
    this.pickerChecked.clear();
  }

  get pickerRows(): CustomField[] {
    const assigned = new Set(this.groupForm?.fieldIds ?? []);
    const q = this.pickerSearch.trim().toLowerCase();
    return this.fields.filter(
      (f) =>
        f.scope !== 'component' &&
        !assigned.has(f.id) &&
        (!q || [f.name, f.label, f.group, f.appliesTo].some((v) => v.toLowerCase().includes(q))),
    );
  }

  togglePick(id: string): void {
    if (this.pickerChecked.has(id)) this.pickerChecked.delete(id);
    else this.pickerChecked.add(id);
  }

  addPicked(): void {
    if (!this.groupForm) return;
    this.groupForm.fieldIds.push(...this.pickerRows.filter((f) => this.pickerChecked.has(f.id)).map((f) => f.id));
    this.pickerOpen = false;
  }

  // ===== Delete groups =====

  askDeleteGroups(groups: FieldGroup[]): void {
    this.openMenu = null;
    this.groupDeleteTargets = groups;
  }

  get groupDeleteFieldCount(): number {
    return this.groupDeleteTargets.reduce((n, g) => n + this.groupFields(g.name).length, 0);
  }

  /** Deleting a group unassigns its fields; the API refuses groups still used by a field layout. */
  confirmDeleteGroups(): void {
    const targets = this.groupDeleteTargets;
    this.groupDeleteTargets = [];
    if (!targets.length) return;
    this.saving = true;
    runAll(targets, (g) => this.api.deleteGroup(g.id).pipe(map(() => g))).subscribe((result) => {
      this.saving = false;
      const deleted = result.ok;
      if (this.groupForm?.original && deleted.includes(this.groupForm.original)) this.groupForm = null;
      if (deleted.some((g) => g.name === this.form.group)) this.form.group = '';
      const what = deleted.length === 1 ? `Group "${deleted[0].name}" deleted.` : deleted.length ? `${deleted.length} groups deleted.` : '';
      this.showToast([what, ...result.errors.slice(0, 1)].filter(Boolean).join(' '));
      if (deleted.length) this.load();
    });
  }

  // ===== Group import / export =====

  private exportGroups(): void {
    this.api.exportGroups().subscribe({
      next: (rows) => {
        downloadJson(rows, 'field-groups.json');
        this.showToast(`Exported ${rows.length} group${rows.length === 1 ? '' : 's'}.`);
      },
      error: (err) => this.showToast(apiErrorMessage(err, 'Could not export groups.')),
    });
  }

  /** Imports a file produced by Export Groups; groups are matched by name and their listed fields assigned in order. */
  private importGroups(parsed: unknown, fileName: string): void {
    const list: unknown[] = Array.isArray(parsed) ? parsed : Array.isArray((parsed as { groups?: unknown[] })?.groups) ? (parsed as { groups: unknown[] }).groups : [];
    if (!list.length) {
      this.showToast(`No groups found in ${fileName}.`);
      return;
    }
    this.api.importGroups(list).subscribe({
      next: (result) => {
        this.showToast(importSummary(result, 'group'));
        this.load();
      },
      error: (err) => this.showToast(apiErrorMessage(err, `Could not import ${fileName}.`)),
    });
  }

  trackById(_: number, row: { id: string }): string {
    return row.id;
  }

  showToast(message: string): void {
    if (!message) return;
    this.toast = message;
    clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => (this.toast = ''), 4000);
  }
}
