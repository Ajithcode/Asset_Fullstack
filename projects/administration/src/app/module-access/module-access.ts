import { Component, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import {
  BadgeCheck, Box, Building2, ChevronRight, Coins, Copy, Download, Ellipsis, Eye, Funnel, Hammer, List, Lock, LucideAngularModule, LucideIconData,
  Plus, Search, Server, Shield, Upload, User, Users, Wrench, X
} from 'lucide-angular';
import { ASSET360_MODULES, AdminPage } from '../shared/admin-common';
import { demoUsers } from '../role-access-control/role-access.data';
import { UsersDialog } from '../role-access-control/users-dialog/users-dialog';
import { ExceptionList } from './exception-list/exception-list';
import { FeatureList } from './feature-list/feature-list';
import { FEATURES, PolicyIcon, PolicyRecord, PolicyStatus, PolicyType, blankPolicy } from './module-access.data';
import { ModuleAccessService } from './module-access.service';
import { ModuleTable } from './module-table/module-table';
import { PolicyDrawer, policyErrors } from './policy-drawer/policy-drawer';

type Tab = 'modules' | 'features' | 'exceptions';

interface Filters {
  type: '' | PolicyType;
  status: '' | PolicyStatus;
  module: string;
}

interface Confirm {
  title: string;
  message: string;
  action: string;
  run: () => void;
}

const POLICY_ICONS: Record<PolicyIcon, LucideIconData> = {
  user: User, users: Users, wrench: Wrench, box: Box, shield: Shield, eye: Eye, coins: Coins, server: Server, hammer: Hammer,
  badge: BadgeCheck, search: Search, building: Building2
};

/** Administration > Module Access: access policies, their module / feature access and per-user exceptions. */
@Component({
  standalone: true,
  selector: 'app-admin-module-access',
  imports: [FormsModule, RouterLink, LucideAngularModule, ModuleTable, FeatureList, ExceptionList, PolicyDrawer, UsersDialog],
  templateUrl: './module-access.html',
  styleUrls: ['../shared/admin-workspace.css', './module-access.css']
})
export class ModuleAccessPage extends AdminPage {
  private readonly service = inject(ModuleAccessService);

  readonly icons = { Box, List, Lock, Users, Plus, Copy, Upload, Download, Search, Funnel, ChevronRight, Ellipsis, X };
  readonly modules = ASSET360_MODULES;
  readonly tabs: { id: Tab; label: string }[] = [
    { id: 'modules', label: 'Modules' },
    { id: 'features', label: 'Features' },
    { id: 'exceptions', label: 'Exceptions' }
  ];

  search = '';
  listSearch = '';
  filters: Filters = { type: '', status: '', module: '' };
  showFilters = false;
  tab: Tab = 'modules';

  selectedId = '';
  draft: PolicyRecord = blankPolicy();
  private snapshot = '';
  isNew = false;
  submitted = false;
  drawerOpen = true;
  menuOpen = false;
  usersOpen = false;
  confirm: Confirm | null = null;

  constructor() {
    super();
    const all = this.service.all();
    this.load(all.find((p) => p.code === 'OPS-MGR-001') ?? all[0]);
  }

  // ===== Stats =====
  get policies(): PolicyRecord[] {
    return this.service.all();
  }

  readonly featureCount = FEATURES.length;
  readonly restrictedCount = FEATURES.filter((f) => f.restricted).length;

  get usersCovered(): number {
    return this.policies.reduce((sum, p) => sum + p.users, 0);
  }

  // ===== List =====
  get filtered(): PolicyRecord[] {
    const terms = [this.search, this.listSearch].map((t) => t.trim().toLowerCase()).filter(Boolean);
    const f = this.filters;
    return this.policies.filter(
      (p) =>
        (!f.type || p.type === f.type) &&
        (!f.status || p.status === f.status) &&
        (!f.module || p.grants.some((g) => g.module === f.module && g.enabled)) &&
        terms.every((t) => this.matches(p, t))
    );
  }

  /** Name, code, type, or a module the policy has enabled */
  private matches(p: PolicyRecord, t: string): boolean {
    return [p.name, p.code, p.type, p.description].some((v) => v.toLowerCase().includes(t)) || p.grants.some((g) => g.enabled && g.module.toLowerCase().includes(t));
  }

  get filterCount(): number {
    return [this.filters.type, this.filters.status, this.filters.module].filter(Boolean).length;
  }

  clearFilters(): void {
    this.filters = { type: '', status: '', module: '' };
  }

  policyIcon(p: PolicyRecord): LucideIconData {
    return POLICY_ICONS[p.icon] ?? Users;
  }

  typeTone(t: PolicyType): string {
    return t === 'Role' ? 'blue' : t === 'Team' ? 'purple' : 'pink';
  }

  get dirty(): boolean {
    return this.isNew || JSON.stringify(this.draft) !== this.snapshot;
  }

  lastUpdated(): string {
    if (!this.draft.updatedAt) return 'Not saved yet';
    const d = new Date(this.draft.updatedAt);
    return `Last updated ${d.toLocaleString('en-GB', { day: '2-digit', month: 'short', year: 'numeric', hour: '2-digit', minute: '2-digit', hour12: true })}`;
  }

  private load(p: PolicyRecord | undefined): void {
    this.draft = p ? structuredClone(p) : blankPolicy();
    this.snapshot = JSON.stringify(this.draft);
    this.selectedId = p?.id ?? '';
    this.isNew = false;
    this.submitted = false;
  }

  private startDraft(p: PolicyRecord): void {
    this.draft = p;
    this.snapshot = JSON.stringify(p);
    this.isNew = true;
    this.submitted = false;
    this.drawerOpen = true;
  }

  /** Runs the action now, or after the user agrees to drop unsaved edits */
  private guard(run: () => void): void {
    if (!this.dirty) {
      run();
      return;
    }
    this.confirm = {
      title: 'Discard unsaved changes?',
      message: `Your changes to ${this.draft.name || 'the new policy'} haven't been saved.`,
      action: 'Discard',
      run
    };
  }

  select(p: PolicyRecord): void {
    if (p.id === this.selectedId && !this.isNew) {
      this.drawerOpen = true;
      return;
    }
    this.guard(() => {
      this.load(p);
      this.drawerOpen = true;
    });
  }

  // ===== Toolbar actions =====
  createPolicy(): void {
    this.guard(() => {
      this.startDraft(blankPolicy());
      this.tab = 'modules';
    });
  }

  clonePolicy(): void {
    this.menuOpen = false;
    const source = this.service.byId(this.selectedId);
    if (!source) {
      this.notify('Select a policy to clone.');
      return;
    }
    this.guard(() => {
      const copy = structuredClone(source);
      copy.id = '';
      copy.name = `${source.name} (Copy)`.slice(0, 60);
      copy.code = this.service.uniqueCode(`${source.code}-COPY`.slice(0, 17));
      copy.status = 'Active';
      copy.users = 0;
      copy.exceptions = [];
      copy.updatedAt = '';
      this.startDraft(copy);
      this.notify(`Cloned ${source.name}. Review and save the new policy.`);
    });
  }

  exportPolicies(): void {
    const rows = this.filtered;
    const blob = new Blob([JSON.stringify(rows, null, 2)], { type: 'application/json' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `access-policies-${new Date().toISOString().slice(0, 10)}.json`;
    a.click();
    URL.revokeObjectURL(url);
    this.notify(`Exported ${rows.length} polic${rows.length === 1 ? 'y' : 'ies'}.`);
  }

  importPolicies(input: HTMLInputElement): void {
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;
    file.text().then((text) => {
      let rows: Partial<PolicyRecord>[];
      try {
        const parsed = JSON.parse(text);
        rows = Array.isArray(parsed) ? parsed : [parsed];
      } catch {
        this.notify('Import failed: the file is not valid JSON.');
        return;
      }
      let added = 0;
      for (const row of rows) {
        if (!row || typeof row.name !== 'string' || typeof row.code !== 'string') continue;
        const base = blankPolicy();
        // Only take module grants that line up with the 13 Asset360 modules
        const grants = Array.isArray(row.grants) && row.grants.length === base.grants.length ? row.grants : base.grants;
        const policy = { ...base, ...row, grants, id: '', users: 0 } as PolicyRecord;
        policy.code = this.service.uniqueCode(policy.code.toUpperCase());
        this.service.save(policy);
        added++;
      }
      this.notify(added ? `Imported ${added} polic${added === 1 ? 'y' : 'ies'}.` : 'No policies found in the file (each needs a name and code).');
    });
  }

  // ===== Save / cancel / delete =====
  savePolicy(): void {
    this.submitted = true;
    const errors = policyErrors(this.draft, this.service.codeTaken(this.draft.code, this.draft.id), this.service.nameTaken(this.draft.name, this.draft.id));
    const first = Object.values(errors)[0];
    if (first) {
      this.drawerOpen = true;
      this.notify(`Fix the highlighted fields: ${first}`);
      return;
    }
    this.draft.name = this.draft.name.trim();
    this.draft.code = this.draft.code.trim();
    const wasNew = this.isNew;
    const stored = this.service.save(this.draft);
    this.load(stored);
    this.notify(`${stored.name} ${wasNew ? 'created' : 'saved'}.`);
  }

  cancelEdit(): void {
    if (this.isNew) {
      this.load(this.service.byId(this.selectedId) ?? this.policies[0]);
      this.notify('New policy discarded.');
      return;
    }
    const wasDirty = this.dirty;
    this.load(this.service.byId(this.selectedId));
    if (wasDirty) this.notify('Changes discarded.');
  }

  toggleStatus(): void {
    this.menuOpen = false;
    this.draft.status = this.draft.status === 'Active' ? 'Inactive' : 'Active';
    this.notify(`${this.draft.name} set to ${this.draft.status}. Save the policy to apply it.`);
  }

  askDelete(): void {
    this.menuOpen = false;
    const p = this.service.byId(this.selectedId);
    if (!p || this.isNew) return;
    this.confirm = {
      title: `Delete ${p.name}?`,
      message: p.users
        ? `${p.users} user${p.users === 1 ? '' : 's'} lose the module access this policy grants. This can't be undone.`
        : "No users are covered by this policy. This can't be undone.",
      action: 'Delete Policy',
      run: () => {
        this.service.remove(p.id);
        this.load(this.policies[0]);
        this.notify(`${p.name} deleted.`);
      }
    };
  }

  confirmYes(): void {
    const run = this.confirm?.run;
    this.confirm = null;
    run?.();
  }

  toggleMenu(e: Event): void {
    e.stopPropagation();
    this.menuOpen = !this.menuOpen;
  }

  closeMenu(): void {
    this.menuOpen = false;
  }

  allUsers(): string[] {
    return demoUsers(this.draft.id || this.draft.name, this.draft.users);
  }
}
