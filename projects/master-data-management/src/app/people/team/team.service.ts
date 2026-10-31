import { Injectable, inject } from '@angular/core';
import { Observable, map, switchMap } from 'rxjs';
import { ApiCodeStore } from '../../shared/code-store';
import { MdmAudit, inParallel, inSequence } from '../../shared/mdm-api';
import { EmployeeService } from '../employee/employee.service';
import { LoadOnce } from '../load-once';
import { MasterLookups } from '../name-lookup';
import { MdmRef, apiText, stampOf } from '../people.data';
import { ShiftService } from '../shift/shift.service';
import { TechnicianService } from '../technician/technician.service';
import { TeamRecord, TeamStatus } from './team.model';

interface TeamDto extends MdmAudit {
  teamName: string;
  code: string;
  department: MdmRef | null;
  teamLeader: MdmRef | null;
  description: string | null;
  members: MdmRef[];
  employeeMembers: MdmRef[];
  shift: MdmRef | null;
  responsibilities: string[];
  notifyLeader: boolean;
  autoAssign: boolean;
}

/**
 * Teams from /api/master-data/teams. Members are employee and technician IDs (EMP..., TECH...); a technician
 * is in at most one team (the API keeps it on the technician), employees can be in several.
 */
@Injectable({ providedIn: 'root' })
export class TeamService extends ApiCodeStore<TeamRecord, TeamDto> {
  protected readonly resource = 'teams';
  private readonly employees = inject(EmployeeService);
  private readonly technicians = inject(TechnicianService);
  private readonly shifts = inject(ShiftService);
  private readonly lookups = inject(MasterLookups);
  // People and shifts first, so record ids resolve to their IDs and codes
  private readonly loader = new LoadOnce(() =>
    inParallel([this.employees.loadOnce(), this.technicians.loadOnce(), this.shifts.loadOnce()]).pipe(switchMap(() => this.load()))
  );

  constructor() {
    super();
  }

  refresh(): Observable<TeamRecord[]> {
    this.lookups.departments.ensureLoaded();
    return this.loader.refresh();
  }

  loadOnce(): Observable<unknown> {
    return this.loader.once();
  }

  ensureLoaded(): void {
    this.lookups.departments.ensureLoaded();
    this.loader.ensure();
  }

  /** `{ id, name }` link the API takes for the team, or null. */
  ref(code: string | null | undefined): MdmRef | null {
    const team = this.find(code);
    const id = code ? this.idOf(code) : undefined;
    return team && id ? { id, name: team.name } : null;
  }

  /** The person's main team: the first team (in list order) they belong to. */
  primaryTeamOf(personId: string): TeamRecord | undefined {
    return this.records.find((r) => r.members.includes(personId));
  }

  /** Moves a person from their current main team to `teamCode` (or out of it) after the API saved it. */
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

  /** Technicians in the team; the API won't delete a team that still has them. */
  technicianMembers(r: TeamRecord): string[] {
    return r.members.filter((m) => this.technicians.find(m));
  }

  /** Saves the team, then drops its technicians from other teams (a technician is in one team only). */
  persistTeam(record: TeamRecord, originalCode: string | null): Observable<TeamRecord> {
    return this.persist(record, originalCode).pipe(
      map((saved) => {
        const moved = this.technicianMembers(saved);
        this.records = this.records.map((r) =>
          r.code !== saved.code && r.members.some((m) => moved.includes(m)) ? { ...r, members: r.members.filter((m) => !moved.includes(m)) } : r
        );
        return saved;
      })
    );
  }

  setStatus(record: TeamRecord, status: TeamStatus): Observable<TeamRecord> {
    return this.api.setStatus<TeamDto>(this.resource, this.idOf(record.code)!, status).pipe(
      map((dto) => {
        const saved = this.toRecord(dto);
        this.save(saved, saved.code);
        return saved;
      })
    );
  }

  /** Puts exactly `teamCodes` on the shift (other teams on it lose it), saving each changed team. */
  assignShift(shiftCode: string, teamCodes: string[], previousCode: string = shiftCode): Observable<unknown> {
    // Teams link to the shift by id, so a changed shift code only needs changing here
    if (previousCode !== shiftCode) {
      this.records = this.records.map((r) => (r.shiftCode === previousCode ? { ...r, shiftCode } : r));
    }
    const changed = this.records
      .map((r): TeamRecord | null => {
        if (teamCodes.includes(r.code)) return r.shiftCode === shiftCode ? null : { ...r, shiftCode };
        if (r.shiftCode === shiftCode) return { ...r, shiftCode: '' };
        return null;
      })
      .filter((r): r is TeamRecord => r !== null);
    return inSequence(changed.map((r) => this.persist(r, r.code)));
  }

  /** Drops people from every team locally (the API already did when they were deleted). */
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

  /** Employee or technician ID of a person's record id. */
  private personOf(recordId: string | null | undefined): string {
    return this.technicians.codeOfId(recordId) || this.employees.codeOfId(recordId);
  }

  protected toRecord(dto: TeamDto): TeamRecord {
    const technicians = (dto.members ?? []).map((m) => this.technicians.codeOfId(m.id)).filter(Boolean);
    const employees = (dto.employeeMembers ?? []).map((m) => this.employees.codeOfId(m.id)).filter(Boolean);
    return {
      code: dto.code,
      name: dto.teamName,
      department: dto.department?.name ?? '',
      leaderId: this.personOf(dto.teamLeader?.id),
      description: dto.description ?? '',
      status: (dto.status as TeamStatus) || 'Active',
      members: [...technicians, ...employees],
      shiftCode: this.shifts.codeOfId(dto.shift?.id),
      responsibilities: dto.responsibilities ?? [],
      notifyLeader: dto.notifyLeader,
      autoAssign: dto.autoAssign,
      createdOn: stampOf(dto.createdAt),
      updatedOn: stampOf(dto.updatedAt)
    };
  }

  protected toRequest(r: TeamRecord): object {
    const technicianIds = this.technicianMembers(r);
    return {
      teamName: r.name,
      code: r.code,
      department: this.lookups.departments.ref(r.department),
      teamLeader: this.technicians.ref(r.leaderId) ?? this.employees.ref(r.leaderId),
      description: apiText(r.description),
      members: technicianIds.map((m) => this.technicians.ref(m)).filter(Boolean),
      employeeMembers: r.members
        .filter((m) => !technicianIds.includes(m))
        .map((m) => this.employees.ref(m))
        .filter(Boolean),
      shift: this.shifts.ref(r.shiftCode),
      responsibilities: r.responsibilities,
      notifyLeader: r.notifyLeader,
      autoAssign: r.autoAssign,
      status: r.status
    };
  }
}
