import { Injectable } from '@angular/core';
import { CURRENT_USER } from '../shared/admin-common';
import { AdminUser, UserStatus } from './user-management.model';
import { seedUsers } from './user-management.data';

// DEMO MODE: users are kept in memory; invitations and reset links are sent by the identity service once connected
@Injectable({ providedIn: 'root' })
export class UserAdminService {
  private users: AdminUser[] = seedUsers();
  private seq = 1000 + this.users.length;

  readonly currentUserId = CURRENT_USER.id;

  all(): AdminUser[] {
    return this.users;
  }

  find(id: string | null): AdminUser | undefined {
    return id ? this.users.find((u) => u.id === id) : undefined;
  }

  count(status?: UserStatus): number {
    return status ? this.users.filter((u) => u.status === status).length : this.users.length;
  }

  emailTaken(email: string, exceptId: string | null): boolean {
    const e = email.trim().toLowerCase();
    return this.users.some((u) => u.email.toLowerCase() === e && u.id !== exceptId);
  }

  employeeIdTaken(employeeId: string, exceptId: string | null): boolean {
    const e = employeeId.trim().toUpperCase();
    return this.users.some((u) => u.employeeId.toUpperCase() === e && u.id !== exceptId);
  }

  nextId(): string {
    this.seq++;
    return `USR-${String(this.seq).padStart(6, '0')}`;
  }

  /** Next free EMP-#### number. */
  nextEmployeeId(): string {
    const max = this.users.reduce((m, u) => Math.max(m, Number(u.employeeId.replace(/\D/g, '')) || 0), 0);
    return `EMP-${String(max + 1).padStart(4, '0')}`;
  }

  save(user: AdminUser): void {
    const exists = this.users.some((u) => u.id === user.id);
    this.users = exists ? this.users.map((u) => (u.id === user.id ? user : u)) : [user, ...this.users];
  }

  remove(ids: string[]): void {
    const set = new Set(ids);
    this.users = this.users.filter((u) => !set.has(u.id));
  }

  /** Why an account can't be suspended or deleted: yourself, or the last active System Administrator. */
  lockoutReason(user: AdminUser, removing: string[] = [user.id]): string | null {
    if (user.id === this.currentUserId) return 'You can’t suspend or delete your own account.';
    const isAdmin = (u: AdminUser) => u.primaryRole === 'System Administrator' || u.secondaryRoles.includes('System Administrator');
    if (isAdmin(user) && user.status === 'Active') {
      const others = this.users.filter((u) => isAdmin(u) && u.status === 'Active' && !removing.includes(u.id));
      if (!others.length) return `${user.name} is the last active System Administrator.`;
    }
    return null;
  }
}
