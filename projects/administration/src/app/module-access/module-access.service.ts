import { Injectable } from '@angular/core';
import { CURRENT_USER } from '../shared/admin-common';
import { PolicyRecord, seedPolicies } from './module-access.data';

// DEMO MODE: access policies live in memory for the session; swap for the access-policy API when it exists
@Injectable({ providedIn: 'root' })
export class ModuleAccessService {
  private policies: PolicyRecord[] = seedPolicies();
  private nextId = this.policies.length + 1;

  all(): PolicyRecord[] {
    return this.policies;
  }

  byId(id: string): PolicyRecord | undefined {
    return this.policies.find((p) => p.id === id);
  }

  codeTaken(code: string, exceptId: string): boolean {
    const c = code.trim().toUpperCase();
    return this.policies.some((p) => p.id !== exceptId && p.code.toUpperCase() === c);
  }

  /** A free code from the base, e.g. OPS-MGR-001-COPY, OPS-MGR-001-COPY-2 */
  uniqueCode(base: string): string {
    let code = base;
    for (let n = 2; this.codeTaken(code, ''); n++) code = `${base}-${n}`;
    return code;
  }

  nameTaken(name: string, exceptId: string): boolean {
    const n = name.trim().toLowerCase();
    return this.policies.some((p) => p.id !== exceptId && p.name.toLowerCase() === n);
  }

  save(policy: PolicyRecord): PolicyRecord {
    const stored = structuredClone(policy);
    stored.updatedAt = new Date().toISOString();
    stored.updatedBy = CURRENT_USER.name;
    if (!stored.id) {
      stored.id = `POL-${String(this.nextId++).padStart(3, '0')}`;
      this.policies = [...this.policies, stored];
    } else {
      this.policies = this.policies.map((p) => (p.id === stored.id ? stored : p));
    }
    return stored;
  }

  remove(id: string): void {
    this.policies = this.policies.filter((p) => p.id !== id);
  }
}
