import { Injectable } from '@angular/core';
import { CodeStore } from '../shared/code-store';
import { initialModules, initialPermissions, initialRoles, initialUsers } from './security.data';
import { ModuleRecord, PermissionRecord, RoleRecord, UserRecord } from './security.model';

/*
 * DEMO MODE: in-memory security masters; replace with API calls when the backend is ready.
 * Permissions point at modules, roles at permissions and modules, users at roles, all by code.
 */

@Injectable({ providedIn: 'root' })
export class ModuleService extends CodeStore<ModuleRecord> {
  constructor() {
    super(initialModules());
  }

  nameOf(code: string): string {
    return this.find(code)?.name ?? code;
  }
}

@Injectable({ providedIn: 'root' })
export class PermissionService extends CodeStore<PermissionRecord> {
  constructor() {
    super(initialPermissions());
  }

  ofModule(code: string): PermissionRecord[] {
    return this.records.filter((r) => r.module === code);
  }

  /** Name must be unique within its module only. */
  isNameTakenIn(module: string, name: string, exceptCode: string | null): boolean {
    const wanted = name.trim().toLowerCase();
    return this.records.some((r) => r.module === module && r.name.toLowerCase() === wanted && r.code !== exceptCode);
  }

  recodeModule(from: string, to: string): void {
    if (from === to) return;
    this.records = this.records.map((r) => (r.module === from ? { ...r, module: to } : r));
  }
}

@Injectable({ providedIn: 'root' })
export class RoleService extends CodeStore<RoleRecord> {
  constructor() {
    super(initialRoles());
  }

  nameOf(code: string): string {
    return this.find(code)?.name ?? code;
  }

  withPermission(code: string): RoleRecord[] {
    return this.records.filter((r) => r.permissions.includes(code));
  }

  withModule(code: string): RoleRecord[] {
    return this.records.filter((r) => r.modules.includes(code));
  }

  /** Carries a renamed permission or module code into every role. */
  recode(field: 'permissions' | 'modules', from: string, to: string): void {
    if (from === to) return;
    this.records = this.records.map((r) => (r[field].includes(from) ? { ...r, [field]: r[field].map((c) => (c === from ? to : c)) } : r));
  }
}

@Injectable({ providedIn: 'root' })
export class UserService extends CodeStore<UserRecord> {
  // DEMO MODE: the signed-in user shown in the top bar
  readonly currentUser = 'USR-001';

  constructor() {
    super(initialUsers());
  }

  /** Users holding the role, as primary or additional role. */
  withRole(code: string): UserRecord[] {
    return this.records.filter((u) => u.primaryRole === code || u.additionalRoles.includes(code));
  }

  isUsernameTaken(username: string, exceptCode: string | null): boolean {
    const wanted = username.trim().toLowerCase();
    return this.records.some((u) => u.username.toLowerCase() === wanted && u.code !== exceptCode);
  }

  isEmailTaken(email: string, exceptCode: string | null): boolean {
    const wanted = email.trim().toLowerCase();
    return this.records.some((u) => u.email.toLowerCase() === wanted && u.code !== exceptCode);
  }

  nextCode(): string {
    const max = this.records.reduce((m, r) => Math.max(m, Number(r.code.replace(/\D/g, '')) || 0), 0);
    return `USR-${String(max + 1).padStart(3, '0')}`;
  }

  recodeRole(from: string, to: string): void {
    if (from === to) return;
    this.records = this.records.map((u) =>
      u.primaryRole === from || u.additionalRoles.includes(from)
        ? { ...u, primaryRole: u.primaryRole === from ? to : u.primaryRole, additionalRoles: u.additionalRoles.map((c) => (c === from ? to : c)) }
        : u
    );
  }
}
