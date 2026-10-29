import { Injectable } from '@angular/core';
import { CURRENT_USER, todayIso } from '../shared/admin-common';
import { RoleRecord, seedRoles } from './role-access.data';

// DEMO MODE: roles live in memory for the session; swap for the roles API when it exists
@Injectable({ providedIn: 'root' })
export class RoleAccessService {
  private roles: RoleRecord[] = seedRoles();
  private nextId = this.roles.length + 1;

  all(): RoleRecord[] {
    return this.roles;
  }

  byId(id: string): RoleRecord | undefined {
    return this.roles.find((r) => r.id === id);
  }

  /** True when another role already uses the code (case-insensitive). */
  codeTaken(code: string, exceptId: string): boolean {
    const c = code.trim().toUpperCase();
    return this.roles.some((r) => r.id !== exceptId && r.code.toUpperCase() === c);
  }

  /** A free code built from the base, e.g. OPS_MANAGER_COPY, OPS_MANAGER_COPY_2 */
  uniqueCode(base: string): string {
    let code = base;
    for (let n = 2; this.codeTaken(code, ''); n++) code = `${base}_${n}`;
    return code;
  }

  /** Inserts or replaces the role; returns the stored copy. */
  save(role: RoleRecord): RoleRecord {
    const stored = structuredClone(role);
    stored.updatedOn = todayIso();
    if (!stored.id) {
      stored.id = `ROLE-${String(this.nextId++).padStart(3, '0')}`;
      stored.createdOn = todayIso();
      stored.createdBy = CURRENT_USER.name;
      this.roles = [...this.roles, stored];
    } else {
      this.roles = this.roles.map((r) => (r.id === stored.id ? stored : r));
    }
    return stored;
  }

  remove(id: string): void {
    this.roles = this.roles.filter((r) => r.id !== id);
  }
}
