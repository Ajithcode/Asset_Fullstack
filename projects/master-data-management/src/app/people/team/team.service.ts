import { Injectable } from '@angular/core';
import { initialTeams } from './team.data';
import { TeamRecord } from './team.model';

/**
 * DEMO MODE: in-memory teams; replace these methods with API calls when the backend is ready.
 * Teams own membership: a technician's team is the first team that lists them.
 */
@Injectable({ providedIn: 'root' })
export class TeamService {
  private records: TeamRecord[] = initialTeams();

  all(): TeamRecord[] {
    return this.records;
  }

  find(code: string | null): TeamRecord | undefined {
    return code ? this.records.find((r) => r.code === code) : undefined;
  }

  /** The person's main team: the first team (in list order) they belong to. */
  primaryTeamOf(personId: string): TeamRecord | undefined {
    return this.records.find((r) => r.members.includes(personId));
  }

  /** Moves a person from their current main team to `teamCode` (or out of it when null). */
  setPrimaryTeam(personId: string, teamCode: string | null): void {
    const current = this.primaryTeamOf(personId);
    if (current?.code === teamCode) return;
    this.records = this.records.map((r) => {
      if (r === current && r.leaderId !== personId) return { ...r, members: r.members.filter((m) => m !== personId) };
      if (r.code === teamCode && !r.members.includes(personId)) return { ...r, members: [personId, ...r.members] };
      return r;
    });
  }

  teamsLedBy(personId: string): TeamRecord[] {
    return this.records.filter((r) => r.leaderId === personId);
  }

  teamsOnShift(shiftCode: string): TeamRecord[] {
    return this.records.filter((r) => r.shiftCode === shiftCode);
  }

  /** Puts exactly `teamCodes` on the shift; other teams on it lose it. */
  assignShift(shiftCode: string, teamCodes: string[], previousCode = shiftCode): void {
    this.records = this.records.map((r) => {
      if (teamCodes.includes(r.code)) return { ...r, shiftCode };
      if (r.shiftCode === previousCode || r.shiftCode === shiftCode) return { ...r, shiftCode: '' };
      return r;
    });
  }

  /** Drops people from every team (used when they are deleted). */
  removeMembers(ids: string[]): void {
    this.records = this.records.map((r) => (r.members.some((m) => ids.includes(m)) ? { ...r, members: r.members.filter((m) => !ids.includes(m)) } : r));
  }

  renameMember(from: string, to: string): void {
    if (from === to) return;
    this.records = this.records.map((r) =>
      r.members.includes(from) || r.leaderId === from
        ? { ...r, members: r.members.map((m) => (m === from ? to : m)), leaderId: r.leaderId === from ? to : r.leaderId }
        : r
    );
  }

  isCodeTaken(code: string, exceptCode: string | null): boolean {
    const wanted = code.trim().toLowerCase();
    return this.records.some((r) => r.code.toLowerCase() === wanted && r.code !== exceptCode);
  }

  isNameTaken(name: string, exceptCode: string | null): boolean {
    const wanted = name.trim().toLowerCase();
    return this.records.some((r) => r.name.toLowerCase() === wanted && r.code !== exceptCode);
  }

  save(record: TeamRecord, originalCode: string | null): void {
    const index = originalCode ? this.records.findIndex((r) => r.code === originalCode) : -1;
    if (index >= 0) {
      this.records[index] = record;
      this.records = [...this.records];
    } else {
      this.records = [...this.records, record];
    }
  }

  remove(codes: string[]): void {
    this.records = this.records.filter((r) => !codes.includes(r.code));
  }
}
