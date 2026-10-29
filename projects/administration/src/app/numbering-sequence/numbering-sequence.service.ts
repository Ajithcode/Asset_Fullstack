import { Injectable, signal } from '@angular/core';
import { CURRENT_USER, todayIso } from '../shared/admin-common';
import { SEQUENCES } from './numbering-sequence.data';
import { NumberSequence, bumpVersion, cloneSequence } from './numbering-sequence.model';

// DEMO MODE: sequences live in memory until the numbering API exists
@Injectable({ providedIn: 'root' })
export class NumberingSequenceService {
  private readonly store = signal<NumberSequence[]>(SEQUENCES.map(cloneSequence));
  readonly sequences = this.store.asReadonly();
  private seq = SEQUENCES.length;

  get(id: string): NumberSequence | undefined {
    return this.store().find((s) => s.id === id);
  }

  codeTaken(code: string, exceptId: string | null): boolean {
    const c = code.trim().toUpperCase();
    return this.store().some((s) => s.id !== exceptId && s.code.toUpperCase() === c);
  }

  /** Insert or update; logs the change and optionally bumps the version (publish). */
  save(draft: NumberSequence, change: string, publish = false): NumberSequence {
    const copy = cloneSequence(draft);
    copy.code = copy.code.trim().toUpperCase();
    copy.name = copy.name.trim();
    copy.updatedOn = todayIso();
    const isNew = !copy.id;
    if (isNew) {
      this.seq += 1;
      copy.id = `SEQ-${String(this.seq).padStart(3, '0')}`;
      copy.createdOn = todayIso();
      copy.version = 'v1.0';
      copy.changes = [];
    } else if (publish) {
      copy.version = bumpVersion(copy.version);
    }
    copy.changes = [{ date: todayIso(), version: copy.version, user: CURRENT_USER.name, change }, ...copy.changes];
    this.store.update((list) => (isNew ? [copy, ...list] : list.map((s) => (s.id === copy.id ? copy : s))));
    return copy;
  }

  remove(id: string): void {
    this.store.update((list) => list.filter((s) => s.id !== id));
  }

  /** Copy with a unique "-COPY" code; returned unsaved so the user can review it first. */
  cloneOf(source: NumberSequence): NumberSequence {
    const copy = cloneSequence(source);
    copy.id = '';
    copy.name = `${source.name} (Copy)`;
    let code = `${source.code}-COPY`.slice(0, 20);
    let n = 2;
    while (this.codeTaken(code, null)) code = `${source.code}-C${n++}`.slice(0, 20);
    copy.code = code;
    copy.currentNumber = copy.startNumber;
    copy.usage = [];
    copy.changes = [];
    return copy;
  }

  /** Adds imported sequences, skipping codes that already exist. Returns how many were added. */
  importMany(rows: NumberSequence[]): number {
    let added = 0;
    for (const row of rows) {
      if (!row.code || this.codeTaken(row.code, null)) continue;
      this.save({ ...row, id: '' }, 'Imported sequence');
      added++;
    }
    return added;
  }
}
