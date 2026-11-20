import { Component, EventEmitter, Input, Output } from '@angular/core';
import {
  ArrowLeftRight, Box, ChartColumn, Check, ClipboardCheck, FileText, House, Coins, LayoutDashboard, Lock, LucideAngularModule, LucideIconData,
  ChevronDown, ChevronRight, Minus, Settings, ShieldCheck, Wifi, Wrench, Database, Factory, Layers
} from 'lucide-angular';
import { CellState, PERM_ACTIONS, PermAction, ROLE_MODULES, RoleRecord } from '../role-access.data';

type Tri = 'all' | 'some' | 'none';

/** Sub-module shown nested under a matrix row; it keeps its own permission module and action names. */
interface SubModule {
  parent: string;
  key: string;
  label: string;
  /** Matrix column -> stored action + label; columns not listed don't apply */
  actions: Partial<Record<PermAction, { action: string; label: string }>>;
}

/**
 * Asset Criticality is a sub-menu of Assets with its own permission module (permissions.AssetCriticality, gateway
 * route /api/asset-criticality). Same actions as in Module Access.
 */
export const SUB_MODULES: SubModule[] = [
  {
    parent: 'Assets',
    key: 'AssetCriticality',
    label: 'Asset Criticality',
    actions: {
      view: { action: 'view', label: 'View' },
      create: { action: 'assess', label: 'Assess' },
      edit: { action: 'configure', label: 'Configure' },
      approve: { action: 'approve', label: 'Approve' },
      export: { action: 'approvecritical', label: 'Approve Critical' }
    }
  }
];

const isGranted = (s: CellState | undefined): boolean => s === 'full' || s === 'partial';

const MODULE_ICONS: Record<string, LucideIconData> = {
  Dashboard: House,
  Assets: Database,
  'Tracking & IoT': Wifi,
  'Movement & Custody': ArrowLeftRight,
  Maintenance: Wrench,
  Inspection: ClipboardCheck,
  Inventory: Box,
  Compliance: ShieldCheck,
  'Contracts & Warranty': FileText,
  Financial: Coins,
  'Reports & Analytics': ChartColumn,
  Administration: Settings,
  'WIP / Operations': Factory,
  'Master Data Management': Layers
};

/**
 * Module × action checkbox matrix for a role. Cells cycle none/partial → full → none;
 * cells inherited from the role template are locked. Edits go straight into the given draft.
 */
@Component({
  selector: 'app-permission-matrix',
  imports: [LucideAngularModule],
  templateUrl: './permission-matrix.html',
  styleUrls: ['../../shared/admin-workspace.css', './permission-matrix.css']
})
export class PermissionMatrix {
  @Input({ required: true }) role!: RoleRecord;
  /** Optional module name filter from the page search */
  @Input() filter = '';
  @Output() changed = new EventEmitter<void>();
  @Output() notice = new EventEmitter<string>();

  readonly actions = PERM_ACTIONS;
  readonly icons = { Check, Minus, Lock, LayoutDashboard, ChevronDown, ChevronRight };
  /** Parent rows whose sub-modules are collapsed */
  private readonly collapsed = new Set<string>();

  get modules(): string[] {
    const t = this.filter.trim().toLowerCase();
    return ROLE_MODULES.map((m) => m.name).filter(
      (m) => !t || m.toLowerCase().includes(t) || this.subModules(m).some((s) => s.label.toLowerCase().includes(t))
    );
  }

  // ===== Sub-modules (Assets › Asset Criticality) =====

  subModules(m: string): SubModule[] {
    return SUB_MODULES.filter((s) => s.parent === m);
  }

  isExpanded(m: string): boolean {
    return !this.collapsed.has(m);
  }

  toggleExpanded(m: string): void {
    if (!this.collapsed.delete(m)) this.collapsed.add(m);
  }

  /** A sub-module is usable only while its parent module has View */
  subBlocked(s: SubModule): boolean {
    return !isGranted(this.state(s.parent, 'view'));
  }

  subState(s: SubModule, a: PermAction): CellState {
    const def = s.actions[a];
    if (!def) return 'na';
    if (this.subBlocked(s)) return 'none';
    return ((this.role.permissions[s.key] ?? {}) as Record<string, CellState>)[def.action] ?? 'none';
  }

  subLabel(s: SubModule, a: PermAction): string {
    return s.actions[a]?.label ?? '';
  }

  subAria(s: SubModule, a: PermAction): string {
    const st = this.subState(s, a);
    return st === 'full' ? 'true' : st === 'partial' ? 'mixed' : 'false';
  }

  subTitle(s: SubModule, a: PermAction): string {
    const label = `${s.label} – ${this.subLabel(s, a)}`;
    if (this.subBlocked(s)) return `${label}: give ${s.parent} View access first`;
    const st = this.subState(s, a);
    return `${label}: ${st === 'full' ? 'Full access' : st === 'partial' ? 'Partial access (own / assigned records)' : 'No access'}`;
  }

  /** Ticking an action also ticks View; unticking View clears the other actions (as in Module Access). */
  toggleSub(s: SubModule, a: PermAction): void {
    const def = s.actions[a];
    if (!def) return;
    if (this.subBlocked(s)) {
      this.notice.emit(`Give ${s.parent} View access first.`);
      return;
    }
    const on = this.subState(s, a) !== 'full';
    const row = { ...((this.role.permissions[s.key] ?? {}) as Record<string, CellState>) };
    row[def.action] = on ? 'full' : 'none';
    if (on && a !== 'view') row['view'] = 'full';
    if (!on && a === 'view') for (const x of Object.values(s.actions)) row[x!.action] = 'none';
    this.role.permissions[s.key] = row as Record<PermAction, CellState>;
    this.changed.emit();
  }

  /** Parent without View: its sub-modules are switched off (nothing is granted when it's switched back on). */
  private syncSubModules(): void {
    for (const s of SUB_MODULES) {
      const stored = this.role.permissions[s.key] as Record<string, CellState> | undefined;
      if (!stored || !this.subBlocked(s)) continue;
      const row = { ...stored };
      for (const x of Object.values(s.actions)) if (row[x!.action] && row[x!.action] !== 'na') row[x!.action] = 'none';
      this.role.permissions[s.key] = row as Record<PermAction, CellState>;
    }
  }

  moduleIcon(m: string): LucideIconData {
    return MODULE_ICONS[m] ?? LayoutDashboard;
  }

  state(m: string, a: PermAction): CellState {
    return this.role.permissions[m]?.[a] ?? 'na';
  }

  isLocked(m: string, a: PermAction): boolean {
    return this.role.locked.includes(`${m}|${a}`);
  }

  ariaChecked(m: string, a: PermAction): string {
    const s = this.state(m, a);
    return s === 'full' ? 'true' : s === 'partial' ? 'mixed' : 'false';
  }

  cellTitle(m: string, a: PermAction): string {
    const s = this.state(m, a);
    const label = `${m} – ${a}`;
    if (this.isLocked(m, a)) return `${label}: inherited from ${this.role.template || 'template'} (locked)`;
    return `${label}: ${s === 'full' ? 'Full access' : s === 'partial' ? 'Partial access (own / assigned records)' : 'No access'}`;
  }

  toggleCell(m: string, a: PermAction): void {
    const s = this.state(m, a);
    if (s === 'na') return;
    if (this.isLocked(m, a)) {
      this.notice.emit(`${m} ${a} is inherited from the ${this.role.template || 'role'} template and can't be changed here.`);
      return;
    }
    this.set(m, a, s === 'full' ? 'none' : 'full');
    this.changed.emit();
  }

  /** Editable cells for a row, a column or the whole matrix */
  private cells(m: string | null, a: PermAction | null): [string, PermAction][] {
    const out: [string, PermAction][] = [];
    for (const mod of m ? [m] : this.modules) {
      for (const act of a ? [a] : this.actions.map((x) => x.id)) {
        if (this.state(mod, act) !== 'na' && !this.isLocked(mod, act)) out.push([mod, act]);
      }
    }
    return out;
  }

  private tri(cells: [string, PermAction][]): Tri {
    const full = cells.filter(([m, a]) => this.state(m, a) === 'full').length;
    return cells.length && full === cells.length ? 'all' : cells.some(([m, a]) => this.state(m, a) !== 'none') ? 'some' : 'none';
  }

  private setAll(cells: [string, PermAction][]): void {
    if (!cells.length) return;
    const value: CellState = this.tri(cells) === 'all' ? 'none' : 'full';
    for (const [m, a] of cells) this.set(m, a, value);
    this.changed.emit();
  }

  private set(m: string, a: PermAction, value: CellState): void {
    this.role.permissions[m] = { ...this.role.permissions[m], [a]: value };
    if (a === 'view' && value === 'none') this.syncSubModules();
  }

  rowState(m: string): Tri {
    return this.tri(this.cells(m, null));
  }

  toggleRow(m: string): void {
    this.setAll(this.cells(m, null));
  }

  colState(a: PermAction): Tri {
    return this.tri(this.cells(null, a));
  }

  toggleCol(a: PermAction): void {
    this.setAll(this.cells(null, a));
  }

  allState(): Tri {
    return this.tri(this.cells(null, null));
  }

  toggleAll(): void {
    this.setAll(this.cells(null, null));
  }
}
