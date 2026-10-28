import { Injectable } from '@angular/core';
import { initialSkills } from './skill.data';
import { SkillRecord } from './skill.model';

/**
 * DEMO MODE: in-memory skills; replace these methods with API calls when the backend is ready.
 * Employees and technicians refer to skills by name.
 */
@Injectable({ providedIn: 'root' })
export class SkillService {
  private records: SkillRecord[] = initialSkills();

  all(): SkillRecord[] {
    return this.records;
  }

  find(code: string | null): SkillRecord | undefined {
    return code ? this.records.find((r) => r.code === code) : undefined;
  }

  /** Active skill names A-Z, plus `keep` so existing choices stay selectable. */
  activeNames(keep: string[] = []): string[] {
    return this.records
      .filter((r) => r.status === 'Active' || keep.includes(r.name))
      .map((r) => r.name)
      .sort((a, b) => a.localeCompare(b));
  }

  isCodeTaken(code: string, exceptCode: string | null): boolean {
    const wanted = code.trim().toLowerCase();
    return this.records.some((r) => r.code.toLowerCase() === wanted && r.code !== exceptCode);
  }

  isNameTaken(name: string, exceptCode: string | null): boolean {
    const wanted = name.trim().toLowerCase();
    return this.records.some((r) => r.name.toLowerCase() === wanted && r.code !== exceptCode);
  }

  save(record: SkillRecord, originalCode: string | null): void {
    const index = originalCode ? this.records.findIndex((r) => r.code === originalCode) : -1;
    if (index >= 0) {
      this.records[index] = record;
      this.records = [...this.records];
    } else {
      this.records = [record, ...this.records];
    }
  }

  remove(codes: string[]): void {
    this.records = this.records.filter((r) => !codes.includes(r.code));
  }
}
