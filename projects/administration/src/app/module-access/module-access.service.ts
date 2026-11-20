import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, map, tap, throwError } from 'rxjs';
import { ASSET360_MODULES } from '../shared/admin-common';
import { RoleAccessService } from '../role-access-control/role-access.service';
import { CellState, PermAction, ROLE_MODULES, RoleRecord } from '../role-access-control/role-access.data';
import { UserAdminService } from '../user-management/user-management.service';
import { MODULE_PERMS, ModuleGrant, ModulePerm, PolicyRecord, blankPolicy, standardFeatureIds } from './module-access.data';

/**
 * Licensed modules of this installation. Mirrors LICENSED_MODULES in the shell (src/app/licence/licence.config.ts),
 * which decides what the sidebar opens; keep both lists the same.
 */
export const LICENSED_MODULES: readonly string[] = ['Dashboard', 'Assets', 'Tracking & IoT', 'Reports & Analytics', 'Administration', 'Master Data Management'];

/** Asset Criticality has its own permission module (gateway route /api/asset-criticality), licensed with Assets. */
export const CRITICALITY_MODULE = 'AssetCriticality';

/** Module Access column -> Asset Criticality permission (view, assess, configure, approve, approvecritical). */
export const CRITICALITY_ACTIONS: Record<ModulePerm, { action: string; label: string }> = {
  view: { action: 'view', label: 'View' },
  create: { action: 'assess', label: 'Assess' },
  edit: { action: 'configure', label: 'Configure' },
  approve: { action: 'approve', label: 'Approve' },
  export: { action: 'approvecritical', label: 'Approve Critical' }
};

/** Module Access names whose permission-matrix module has another name. */
const MATRIX_MODULE: Record<string, string> = { 'Audit & Compliance': 'Compliance' };

const matrixName = (module: string): string => MATRIX_MODULE[module] ?? module;
const granted = (state: string | undefined): boolean => state === 'full' || state === 'partial';

function isLicensed(module: string): boolean {
  return LICENSED_MODULES.includes(module === CRITICALITY_MODULE ? 'Assets' : module);
}

/** One module row built from the role's permission matrix. */
function toGrant(role: RoleRecord, module: string): ModuleGrant {
  const stored = (role.permissions[matrixName(module)] ?? {}) as Record<string, CellState | undefined>;
  const na = module === CRITICALITY_MODULE ? [] : (ROLE_MODULES.find((m) => m.name === matrixName(module))?.na ?? []);
  const perms = {} as Record<ModulePerm, boolean | null>;
  for (const p of MODULE_PERMS) {
    const action = module === CRITICALITY_MODULE ? CRITICALITY_ACTIONS[p.id].action : p.id;
    perms[p.id] = na.includes(p.id as PermAction) ? null : granted(stored[action]);
  }
  const unlicensed = !isLicensed(module);
  const values = MODULE_PERMS.map((p) => perms[p.id]).filter((v) => v !== null);
  const enabled = values.some(Boolean);
  const readOnly = !!perms.view && !perms.create && !perms.edit && !perms.approve;
  const scope = !enabled ? 'Restricted' : values.every(Boolean) ? 'All Data' : readOnly ? 'Read Only' : 'Assigned Locations';
  return { module, enabled, perms, locked: unlicensed ? MODULE_PERMS.map((p) => p.id) : [], scope, unlicensed };
}

/**
 * Module Access shows one access policy per role (Administration › Role & Access Control). Its module grants are the
 * role's permission matrix, so saving here updates the role: GET /api/users/me and the gateway read the same matrix.
 * Features, exceptions, landing module and session settings have no backend fields and stay client-side.
 */
function toPolicy(role: RoleRecord, extras?: PolicyRecord): PolicyRecord {
  const base = extras ? structuredClone(extras) : blankPolicy();
  const grants = [...ASSET360_MODULES, CRITICALITY_MODULE].map((m) => toGrant(role, m));
  return {
    ...base,
    id: role.id,
    name: role.name,
    code: role.code,
    description: role.description,
    type: 'Role',
    status: role.status,
    icon: role.type === 'System' ? 'shield' : 'users',
    roles: [role.name],
    grants,
    features: extras ? base.features : grants.filter((g) => g.enabled).flatMap((g) => standardFeatureIds(g.module)),
    updatedAt: role.updatedOn,
    updatedBy: role.createdBy
  };
}

/** Writes the policy's module grants back onto (a copy of) the role's permission matrix. Unlicensed modules stay as they are. */
function applyGrants(role: RoleRecord, policy: PolicyRecord): RoleRecord {
  const next = structuredClone(role);
  for (const g of policy.grants) {
    if (g.unlicensed) continue;
    const name = matrixName(g.module);
    const row = { ...(next.permissions[name] ?? {}) } as Record<string, CellState>;
    for (const p of MODULE_PERMS) {
      if (g.perms[p.id] === null) continue;
      const action = g.module === CRITICALITY_MODULE ? CRITICALITY_ACTIONS[p.id].action : p.id;
      row[action] = g.enabled && g.perms[p.id] ? 'full' : 'none';
    }
    // Delete isn't a Module Access column: switching a module off removes it too
    if (g.module !== CRITICALITY_MODULE && !g.enabled && row['delete'] && row['delete'] !== 'na') row['delete'] = 'none';
    next.permissions[name] = row as Record<PermAction, CellState>;
  }
  next.status = policy.status;
  next.description = policy.description;
  // System roles keep their name and code (as in Role & Access Control)
  if (role.type !== 'System') {
    next.name = policy.name;
    next.code = policy.code;
  }
  return next;
}

/** Module access per role, read from and saved to the roles API (GET / PUT api/roles). */
@Injectable({ providedIn: 'root' })
export class ModuleAccessService {
  private readonly roles = inject(RoleAccessService);
  private readonly users = inject(UserAdminService);

  private readonly policies = signal<PolicyRecord[]>([]);
  /** Policies with the number of users holding the role (primary or secondary) */
  private readonly counted = computed(() => {
    const users = this.users.all();
    return this.policies().map((p) => ({
      ...p,
      users: users.filter((u) => p.roles.includes(u.primaryRole) || u.secondaryRoles.some((r) => p.roles.includes(r))).length
    }));
  });
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  all(): PolicyRecord[] {
    return this.counted();
  }

  byId(id: string): PolicyRecord | undefined {
    return this.counted().find((p) => p.id === id);
  }

  /** Users holding one of the policy's roles, by name. */
  userNames(policy: PolicyRecord): string[] {
    return this.users
      .all()
      .filter((u) => policy.roles.includes(u.primaryRole) || u.secondaryRoles.some((r) => policy.roles.includes(r)))
      .map((u) => u.name);
  }

  /** GET /api/roles: one policy per role. */
  load(): Observable<PolicyRecord[]> {
    this.loading.set(true);
    this.error.set(null);
    this.users.load();
    return this.roles.load().pipe(
      map((roles) => roles.map((r) => toPolicy(r, this.policies().find((p) => p.id === r.id)))),
      tap({
        next: (policies) => {
          this.policies.set(policies);
          this.loading.set(false);
        },
        error: () => this.loading.set(false)
      }),
      map(() => this.counted())
    );
  }

  codeTaken(code: string, exceptId: string): boolean {
    const c = code.trim().toUpperCase();
    return this.policies().some((p) => p.id !== exceptId && p.code.toUpperCase() === c);
  }

  uniqueCode(base: string): string {
    let code = base;
    for (let n = 2; this.codeTaken(code, ''); n++) code = `${base}-${n}`;
    return code;
  }

  nameTaken(name: string, exceptId: string): boolean {
    const n = name.trim().toLowerCase();
    return this.policies().some((p) => p.id !== exceptId && p.name.toLowerCase() === n);
  }

  /** PUT /api/roles/{roleId} with the policy's module grants as the role's permissions. */
  save(policy: PolicyRecord): Observable<PolicyRecord> {
    const role = this.roles.byId(policy.id);
    if (!role) return throwError(() => new Error('Access is set per role. Create the role in Role & Access Control first.'));
    return this.roles.save(applyGrants(role, policy)).pipe(
      map((stored) => toPolicy(stored, policy)),
      tap((stored) => this.policies.update((list) => list.map((p) => (p.id === stored.id ? stored : p)))),
      map((stored) => this.byId(stored.id) ?? stored)
    );
  }

  /** Roles are deleted in Role & Access Control. */
  remove(_id: string): Observable<void> {
    return throwError(() => new Error('Delete the role in Role & Access Control.'));
  }
}
