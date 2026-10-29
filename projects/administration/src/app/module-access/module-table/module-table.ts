import { Component, EventEmitter, Input, Output } from '@angular/core';
import {
  ArrowLeftRight, Box, ChartColumn, ClipboardCheck, DollarSign, FileText, FolderTree, House, LayoutDashboard, Lock, LucideAngularModule, LucideIconData,
  Package, Radio, Settings, Shield, Wrench
} from 'lucide-angular';
import { MODULE_PERMS, MODULE_SCOPES, ModuleGrant, ModulePerm, PolicyRecord, featuresOf, scopeTone, standardFeatureIds } from '../module-access.data';

const MODULE_ICONS: Record<string, LucideIconData> = {
  Dashboard: House,
  Assets: Box,
  'Tracking & IoT': Radio,
  'Movement & Custody': ArrowLeftRight,
  Maintenance: Wrench,
  Inspection: ClipboardCheck,
  Inventory: Package,
  'Audit & Compliance': Shield,
  'Contracts & Warranty': FileText,
  Financial: DollarSign,
  'Reports & Analytics': ChartColumn,
  Administration: Settings,
  'Master Data Management': FolderTree
};

export function moduleIcon(module: string): LucideIconData {
  return MODULE_ICONS[module] ?? LayoutDashboard;
}

/** Switches a module on (view + standard features) or off (clears actions and features, scope Restricted). */
export function setModuleEnabled(policy: PolicyRecord, g: ModuleGrant, on: boolean): void {
  if (g.enabled === on) return;
  g.enabled = on;
  if (on) {
    if (!g.perms.view) g.perms.view = true;
    if (g.scope === 'Restricted') g.scope = 'Assigned Locations';
    const standard = standardFeatureIds(g.module);
    policy.features = [...policy.features.filter((id) => !standard.includes(id)), ...standard];
  } else {
    for (const p of MODULE_PERMS) {
      if (g.perms[p.id] !== null && !g.locked.includes(p.id)) g.perms[p.id] = false;
    }
    g.scope = 'Restricted';
    const ids = featuresOf(g.module).map((f) => f.id);
    policy.features = policy.features.filter((id) => !ids.includes(id));
  }
}

/**
 * "Modules" tab: one row per Asset360 module with the Enabled switch, action checkboxes and data scope.
 * Unticking View clears the other actions; ticking any action also ticks View. Locked cells are inherited.
 */
@Component({
  selector: 'app-module-table',
  imports: [LucideAngularModule],
  templateUrl: './module-table.html',
  styleUrls: ['../../shared/admin-workspace.css', './module-table.css']
})
export class ModuleTable {
  @Input({ required: true }) policy!: PolicyRecord;
  @Input() filter = '';
  @Output() notice = new EventEmitter<string>();

  readonly perms = MODULE_PERMS;
  readonly scopes = MODULE_SCOPES;
  readonly icons = { Lock };

  get rows(): ModuleGrant[] {
    const t = this.filter.trim().toLowerCase();
    const matching = this.policy.grants.filter((g) => g.module.toLowerCase().includes(t));
    // A search that matches no module (e.g. a policy name) leaves the table alone
    return t && matching.length ? matching : this.policy.grants;
  }

  icon(m: string): LucideIconData {
    return moduleIcon(m);
  }

  tone(scope: string): string {
    return scopeTone(scope);
  }

  isLocked(g: ModuleGrant, p: ModulePerm): boolean {
    return g.locked.includes(p);
  }

  allEnabled(): boolean {
    return this.policy.grants.every((g) => g.enabled);
  }

  someEnabled(): boolean {
    return this.policy.grants.some((g) => g.enabled) && !this.allEnabled();
  }

  toggleAllModules(): void {
    const on = !this.allEnabled();
    for (const g of this.policy.grants) setModuleEnabled(this.policy, g, on);
    this.notice.emit(on ? 'All modules enabled with View access. Review the actions per module.' : 'All modules disabled.');
  }

  toggleEnabled(g: ModuleGrant): void {
    setModuleEnabled(this.policy, g, !g.enabled);
  }

  togglePerm(g: ModuleGrant, p: ModulePerm): void {
    if (this.isLocked(g, p)) {
      this.notice.emit(`${g.module} ${p} is inherited from the parent policy and can't be changed here.`);
      return;
    }
    if (!g.enabled) {
      this.notice.emit(`Enable ${g.module} first.`);
      return;
    }
    const on = !g.perms[p];
    g.perms[p] = on;
    if (p === 'view' && !on) {
      for (const x of this.perms) if (x.id !== 'view' && g.perms[x.id] !== null && !this.isLocked(g, x.id)) g.perms[x.id] = false;
    } else if (on && p !== 'view') {
      g.perms.view = true;
    }
  }

  setScope(g: ModuleGrant, e: Event): void {
    g.scope = (e.target as HTMLSelectElement).value;
  }
}
