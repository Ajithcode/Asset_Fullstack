import { Injectable } from '@angular/core';
import { nextId } from '../people.data';
import { initialEmployees } from './employee.data';
import { EmployeeRecord } from './employee.model';

/** DEMO MODE: in-memory employees; replace these methods with API calls when the backend is ready. */
@Injectable({ providedIn: 'root' })
export class EmployeeService {
  private records: EmployeeRecord[] = initialEmployees();

  all(): EmployeeRecord[] {
    return this.records;
  }

  find(id: string | null): EmployeeRecord | undefined {
    return id ? this.records.find((r) => r.id === id) : undefined;
  }

  nextId(): string {
    return nextId('EMP', this.records.map((r) => r.id));
  }

  isIdTaken(id: string, exceptId: string | null): boolean {
    const wanted = id.trim().toLowerCase();
    return this.records.some((r) => r.id.toLowerCase() === wanted && r.id !== exceptId);
  }

  isEmailTaken(email: string, exceptId: string | null): boolean {
    const wanted = email.trim().toLowerCase();
    return this.records.some((r) => r.email.toLowerCase() === wanted && r.id !== exceptId);
  }

  /** Employees who report to `id`. */
  reportsOf(id: string): EmployeeRecord[] {
    return this.records.filter((r) => r.reportingManager === id);
  }

  usesSkill(name: string): number {
    return this.records.filter((r) => r.skills.includes(name)).length;
  }

  renameSkill(from: string, to: string): void {
    if (from === to) return;
    this.records = this.records.map((r) => (r.skills.includes(from) ? { ...r, skills: r.skills.map((s) => (s === from ? to : s)) } : r));
  }

  save(record: EmployeeRecord, originalId: string | null): void {
    const index = originalId ? this.records.findIndex((r) => r.id === originalId) : -1;
    if (index >= 0) {
      const list = [...this.records];
      list[index] = record;
      // Reports follow a changed manager ID
      this.records = originalId !== record.id ? list.map((r) => (r.reportingManager === originalId ? { ...r, reportingManager: record.id } : r)) : list;
    } else {
      this.records = [record, ...this.records];
    }
  }

  remove(ids: string[]): void {
    this.records = this.records.filter((r) => !ids.includes(r.id));
  }
}
