import { HttpClient } from '@angular/common/http';
import { Injectable, inject, signal } from '@angular/core';
import { Observable, finalize, map, switchMap, tap } from 'rxjs';
import { AuthService, environment } from 'shared-ui';
import { RoleAccessService, adminApiError } from '../role-access-control/role-access.service';
import { PolicyRecord, PolicyStatus, PolicyType, blankPolicy } from './module-access.data';

/** Request body for POST {administrationApiUrl}api/access-policies */
interface CreateAccessPolicyRequest {
  policyName: string;
  policyCode: string;
  description: string | null;
  policyType: PolicyType;
  status: PolicyStatus;
  /** Role ids (roleId from api/roles) */
  assignedRoles: string[];
  createdBy: string | null;
  tenantId: string;
  clientId: string;
}

/** Request body for PUT {administrationApiUrl}api/access-policies/{policyId} */
type UpdateAccessPolicyRequest = Pick<
  CreateAccessPolicyRequest,
  'policyName' | 'policyCode' | 'description' | 'policyType' | 'status' | 'assignedRoles'
>;

interface AssignedRoleDto {
  roleId: string;
  roleCode: string;
  roleName: string;
}

/** Raw shape returned by the access-policies API. */
interface AccessPolicyDto {
  policyId: string;
  policyName: string;
  policyCode: string;
  description: string | null;
  policyType: string;
  status: string | null;
  assignedRoles: AssignedRoleDto[];
  createdBy: string | null;
  createdAt: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
  tenantId: string | null;
  clientId: string | null;
}

/**
 * Maps an AccessPolicyDto onto the page model. The API stores the policy header and its roles only; module grants,
 * features, exceptions, landing module, session / MFA settings and the user count have no backend fields yet, so they
 * come from `extras` (the client-side copy) or the blank-policy defaults.
 */
function toAppPolicy(dto: AccessPolicyDto, extras?: PolicyRecord): PolicyRecord {
  const base = extras ? structuredClone(extras) : blankPolicy();
  const type: PolicyType = dto.policyType === 'Team' ? 'Team' : dto.policyType === 'User' ? 'User' : 'Role';
  return {
    ...base,
    id: dto.policyId,
    name: dto.policyName,
    code: dto.policyCode,
    description: dto.description ?? '',
    type,
    status: dto.status === 'Inactive' ? 'Inactive' : 'Active',
    icon: extras?.icon ?? (type === 'User' ? 'user' : 'users'),
    roles: dto.assignedRoles.map((r) => r.roleName),
    updatedAt: dto.updatedAt ?? dto.createdAt ?? '',
    updatedBy: dto.updatedBy ?? dto.createdBy ?? ''
  };
}

/** Access policies from the Administration API (GET / POST / PUT / DELETE api/access-policies). */
@Injectable({ providedIn: 'root' })
export class ModuleAccessService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly roles = inject(RoleAccessService);
  private readonly apiUrl = `${environment.administrationApiUrl}api/access-policies`;

  private readonly policies = signal<PolicyRecord[]>([]);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  all(): PolicyRecord[] {
    return this.policies();
  }

  byId(id: string): PolicyRecord | undefined {
    return this.policies().find((p) => p.id === id);
  }

  /** GET /api/access-policies — replaces the list; client-only fields of policies already loaded are kept. */
  load(): Observable<PolicyRecord[]> {
    this.loading.set(true);
    this.error.set(null);
    return this.http.get<AccessPolicyDto[]>(this.apiUrl).pipe(
      map((list) => list.map((dto) => toAppPolicy(dto, this.byId(dto.policyId)))),
      tap({
        next: (policies) => this.policies.set(policies),
        error: (err) => this.error.set(adminApiError(err, 'access policy'))
      }),
      finalize(() => this.loading.set(false))
    );
  }

  codeTaken(code: string, exceptId: string): boolean {
    const c = code.trim().toUpperCase();
    return this.policies().some((p) => p.id !== exceptId && p.code.toUpperCase() === c);
  }

  /** A free code from the base, e.g. OPS-MGR-001-COPY, OPS-MGR-001-COPY-2 */
  uniqueCode(base: string): string {
    let code = base;
    for (let n = 2; this.codeTaken(code, ''); n++) code = `${base}-${n}`;
    return code;
  }

  nameTaken(name: string, exceptId: string): boolean {
    const n = name.trim().toLowerCase();
    return this.policies().some((p) => p.id !== exceptId && p.name.toLowerCase() === n);
  }

  /** New policies: POST /api/access-policies; existing ones: PUT /api/access-policies/{policyId}. Emits the stored copy. */
  save(policy: PolicyRecord): Observable<PolicyRecord> {
    // The UI picks roles by name; the API wants role ids, so resolve them against the roles list
    return this.roles.ensureLoaded().pipe(
      switchMap(() => {
        const roles = this.roles.all();
        const update: UpdateAccessPolicyRequest = {
          policyName: policy.name.trim(),
          policyCode: policy.code.trim().toUpperCase(),
          description: policy.description.trim() || null,
          policyType: policy.type,
          status: policy.status,
          assignedRoles: policy.roles.map((name) => roles.find((r) => r.name === name)?.id).filter((id): id is string => !!id)
        };
        if (policy.id) {
          return this.http.put<AccessPolicyDto>(`${this.apiUrl}/${encodeURIComponent(policy.id)}`, update);
        }
        const request: CreateAccessPolicyRequest = {
          ...update,
          createdBy: this.auth.getUserEmail(),
          tenantId: environment.tenantId,
          clientId: environment.clientId
        };
        return this.http.post<AccessPolicyDto>(this.apiUrl, request);
      }),
      map((dto) => toAppPolicy(dto, policy)),
      tap((stored) =>
        this.policies.update((list) =>
          list.some((p) => p.id === stored.id) ? list.map((p) => (p.id === stored.id ? stored : p)) : [...list, stored]
        )
      )
    );
  }

  /** DELETE /api/access-policies/{policyId} (soft delete). */
  remove(id: string): Observable<void> {
    return this.http
      .delete<void>(`${this.apiUrl}/${encodeURIComponent(id)}`)
      .pipe(tap(() => this.policies.update((list) => list.filter((p) => p.id !== id))));
  }
}
