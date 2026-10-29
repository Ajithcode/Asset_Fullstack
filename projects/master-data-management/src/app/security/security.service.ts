import { Injectable, inject } from '@angular/core';
import { ApiCodeStore, MasterDto } from '../shared/api-code-store';
import { DepartmentService } from '../organization/department/department.service';
import { blankModule, blankPermission, blankRole, blankUser } from './security.data';
import { ModuleRecord, PermissionRecord, PermissionType, RoleRecord, RoleType, UserRecord } from './security.model';

/*
 * Security masters from {projectApiUrl}api/master-data/... Permissions point at modules, roles at
 * permissions and modules, users at roles; records keep codes and the API links by id. Fields the
 * API doesn't store yet are kept for the session only.
 */

interface ReferenceDto {
  id: string | null;
  name: string;
}

const ref = (r: { id: string; name: string } | undefined) => (r ? { id: r.id, name: r.name } : null);

interface ModuleDto extends MasterDto {
  moduleName: string;
  category: string;
  description: string | null;
  icon: string | null;
}

@Injectable({ providedIn: 'root' })
export class ModuleService extends ApiCodeStore<ModuleRecord, ModuleDto> {
  constructor() {
    super('modules');
    this.load();
  }

  nameOf(code: string): string {
    return this.find(code)?.name ?? code;
  }

  protected toRecord(dto: ModuleDto, cached: ModuleRecord | undefined): ModuleRecord {
    const base = cached ?? blankModule();
    return { ...base, name: dto.moduleName, category: dto.category, description: dto.description ?? '', icon: dto.icon ?? base.icon };
  }

  protected toRequest(r: ModuleRecord) {
    return { moduleName: r.name, code: r.code, category: r.category, description: r.description, icon: r.icon };
  }
}

interface PermissionDto extends MasterDto {
  permissionName: string;
  module: ReferenceDto | null;
  type: string;
  description: string | null;
}

@Injectable({ providedIn: 'root' })
export class PermissionService extends ApiCodeStore<PermissionRecord, PermissionDto> {
  private readonly modules = inject(ModuleService);

  constructor() {
    super('permissions');
    this.load();
  }

  protected override waitFor() {
    return [this.modules.settled$];
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

  protected toRecord(dto: PermissionDto, cached: PermissionRecord | undefined): PermissionRecord {
    return {
      ...(cached ?? blankPermission()),
      name: dto.permissionName,
      module: this.modules.all().find((m) => m.id === dto.module?.id)?.code ?? '',
      type: dto.type as PermissionType,
      description: dto.description ?? ''
    };
  }

  protected toRequest(r: PermissionRecord) {
    return { permissionName: r.name, code: r.code, module: ref(this.modules.find(r.module)), type: r.type, description: r.description };
  }
}

interface RoleDto extends MasterDto {
  roleName: string;
  type: string;
  description: string | null;
  permissions: ReferenceDto[];
  moduleAccess: ReferenceDto[];
}

@Injectable({ providedIn: 'root' })
export class RoleService extends ApiCodeStore<RoleRecord, RoleDto> {
  private readonly modules = inject(ModuleService);
  private readonly permissionService = inject(PermissionService);

  constructor() {
    super('roles');
    this.load();
  }

  protected override waitFor() {
    return [this.modules.settled$, this.permissionService.settled$];
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

  protected toRecord(dto: RoleDto, cached: RoleRecord | undefined): RoleRecord {
    const codes = (refs: ReferenceDto[], list: { id: string; code: string }[]) =>
      refs.map((x) => list.find((r) => r.id === x.id)?.code).filter((c): c is string => !!c);
    return {
      ...(cached ?? blankRole()),
      name: dto.roleName,
      type: dto.type as RoleType,
      description: dto.description ?? '',
      permissions: codes(dto.permissions, this.permissionService.all()),
      modules: codes(dto.moduleAccess, this.modules.all())
    };
  }

  protected toRequest(r: RoleRecord) {
    return {
      roleName: r.name,
      code: r.code,
      type: r.type,
      description: r.description,
      permissions: r.permissions.map((c) => ref(this.permissionService.find(c))).filter((x) => x !== null),
      moduleAccess: r.modules.map((c) => ref(this.modules.find(c))).filter((x) => x !== null)
    };
  }
}

interface UserDto extends MasterDto {
  fullName: string;
  username: string;
  email: string;
  department: ReferenceDto | null;
  primaryRole: ReferenceDto | null;
  phone: string | null;
  photoUrl: string | null;
  // Role codes held besides the primary role
  additionalRoles: string[];
}

/** System users; the department is shown by name and the roles by code. Access and preferences are UI-only for now. */
@Injectable({ providedIn: 'root' })
export class UserService extends ApiCodeStore<UserRecord, UserDto> {
  private readonly roles = inject(RoleService);
  private readonly departments = inject(DepartmentService);
  // DEMO MODE: the signed-in user shown in the top bar
  readonly currentUser = 'USR-001';

  constructor() {
    super('users');
    this.load();
  }

  protected override waitFor() {
    return [this.roles.settled$];
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

  protected toRecord(dto: UserDto, cached: UserRecord | undefined): UserRecord {
    return {
      ...(cached ?? blankUser()),
      name: dto.fullName,
      username: dto.username,
      email: dto.email,
      department: dto.department?.name ?? '',
      primaryRole: this.roles.all().find((r) => r.id === dto.primaryRole?.id)?.code ?? '',
      additionalRoles: dto.additionalRoles,
      phone: dto.phone ?? '',
      photo: dto.photoUrl
    };
  }

  protected toRequest(r: UserRecord) {
    const department = this.departments.all().find((d) => d.name === r.department);
    return {
      fullName: r.name,
      code: r.code,
      username: r.username,
      email: r.email,
      department: department ? { id: department.id, name: department.name } : null,
      primaryRole: ref(this.roles.find(r.primaryRole)),
      phone: r.phone,
      // TODO: upload through the Media service and send its URL; this sends the picked photo as a data URL
      photoUrl: r.photo,
      additionalRoles: r.additionalRoles
    };
  }
}
