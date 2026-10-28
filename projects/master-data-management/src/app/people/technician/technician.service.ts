import { Injectable } from '@angular/core';
import { nextId } from '../people.data';
import { initialTechnicians } from './technician.data';
import { TechnicianRecord } from './technician.model';

/** DEMO MODE: in-memory technicians; replace these methods with API calls when the backend is ready. */
@Injectable({ providedIn: 'root' })
export class TechnicianService {
  private records: TechnicianRecord[] = initialTechnicians();

  all(): TechnicianRecord[] {
    return this.records;
  }

  find(id: string | null): TechnicianRecord | undefined {
    return id ? this.records.find((r) => r.id === id) : undefined;
  }

  nextId(): string {
    return nextId('TECH', this.records.map((r) => r.id));
  }

  isIdTaken(id: string, exceptId: string | null): boolean {
    const wanted = id.trim().toLowerCase();
    return this.records.some((r) => r.id.toLowerCase() === wanted && r.id !== exceptId);
  }

  isEmailTaken(email: string, exceptId: string | null): boolean {
    const wanted = email.trim().toLowerCase();
    return this.records.some((r) => r.email.toLowerCase() === wanted && r.id !== exceptId);
  }

  usesSkill(name: string): number {
    return this.records.filter((r) => r.primarySkill === name || r.secondarySkills.includes(name)).length;
  }

  renameSkill(from: string, to: string): void {
    if (from === to) return;
    this.records = this.records.map((r) =>
      r.primarySkill === from || r.secondarySkills.includes(from)
        ? { ...r, primarySkill: r.primarySkill === from ? to : r.primarySkill, secondarySkills: r.secondarySkills.map((s) => (s === from ? to : s)) }
        : r
    );
  }

  save(record: TechnicianRecord, originalId: string | null): void {
    const index = originalId ? this.records.findIndex((r) => r.id === originalId) : -1;
    if (index >= 0) {
      this.records[index] = record;
      this.records = [...this.records];
    } else {
      this.records = [record, ...this.records];
    }
  }

  remove(ids: string[]): void {
    this.records = this.records.filter((r) => !ids.includes(r.id));
  }
}
