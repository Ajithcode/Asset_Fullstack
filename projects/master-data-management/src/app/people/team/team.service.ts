import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, concat, forkJoin, map, of, switchMap, take, tap, toArray } from 'rxjs';
import { AuthService, environment } from 'shared-ui';
import { DepartmentService } from '../../organization/department/department.service';
import { apiErrorMessage } from '../../shared/api-error';
import { ShiftService } from '../shift/shift.service';
import { TechnicianService } from '../technician/technician.service';
import { blankTeam } from './team.data';
import { TeamRecord } from './team.model';

interface ReferenceDto {
  id: string | null;
  name: string;
}

/** Raw shape returned by GET/POST/PUT {projectApiUrl}api/master-data/teams */
interface TeamDto {
  id: string;
  teamName: string;
  code: string;
  department: ReferenceDto | null;
  teamLeader: ReferenceDto | null;
  shift: ReferenceDto | null;
  description: string | null;
  // Technicians whose assigned team is this one
  members: ReferenceDto[];
  memberCount: number;
  createdBy: string | null;
  createdAt: string | null;
  updatedBy: string | null;
  updatedAt: string | null;
  status: string | null;
}

/**
 * Teams from the Master Data API. Rows are cached after load() so the People pages can keep
 * reading them synchronously; writes go to the API and update the cache when they succeed.
 * Members and the leader are technicians, and each technician is in one team (the API stores it on
 * the technician). Records keep technician numbers (TECH…) and shift codes; the API links by id.
 * Responsibilities and settings are UI-only for now.
 */
@Injectable({ providedIn: 'root' })
export class TeamService {
  private readonly http = inject(HttpClient);
  private readonly auth = inject(AuthService);
  private readonly technicians = inject(TechnicianService);
  private readonly shifts = inject(ShiftService);
  private readonly departments = inject(DepartmentService);
  private readonly apiUrl = `${environment.projectApiUrl}api/master-data/teams`;

  private records: TeamRecord[] = [];
  loading = false;
  loadError = '';

  constructor() {
    this.load();
  }

  /** GET /api/master-data/teams once technicians and shifts are loaded, so their ids can be mapped. */
  load(): void {
    if (this.loading) return;
    this.loading = true;
    this.loadError = '';
    forkJoin([this.technicians.settled$.pipe(take(1)), this.shifts.settled$.pipe(take(1))])
      .pipe(switchMap(() => this.http.get<TeamDto[]>(this.apiUrl, { params: { clientId: environment.clientId } })))
      .subscribe({
        next: (list) => {
          this.records = list.map((d) => this.toRecord(d));
          this.loading = false;
        },
        error: (err) => {
          this.loadError = apiErrorMessage(err);
          this.loading = false;
        }
      });
  }

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

  /** Puts exactly `teamCodes` on the shift; other teams on it lose it. Saves each changed team. */
  assignShift(shiftCode: string, teamCodes: string[], previousCode = shiftCode): Observable<unknown> {
    const changes = this.records
      .map((r) => {
        if (teamCodes.includes(r.code)) return r.shiftCode === shiftCode ? null : { ...r, shiftCode };
        if (r.shiftCode === previousCode || r.shiftCode === shiftCode) return { ...r, shiftCode: '' };
        return null;
      })
      .filter((r): r is TeamRecord => r !== null);
    return changes.length ? concat(...changes.map((r) => this.save(r, r.code))).pipe(toArray()) : of([]);
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

  /** POST for a new record, PUT /{id} for an existing one. Members move here from any other team. */
  save(record: TeamRecord, originalCode: string | null): Observable<TeamRecord> {
    const user = this.auth.getUserEmail() || 'unknown';
    const technicianRef = (number: string) => {
      const t = this.technicians.find(number);
      return t ? { id: t.recordId, name: t.name } : null;
    };
    const department = this.departments.all().find((d) => d.name === record.department);
    const shift = this.shifts.find(record.shiftCode);
    const body = {
      teamName: record.name,
      code: record.code,
      department: department ? { id: department.id, name: department.name } : null,
      teamLeader: record.leaderId ? technicianRef(record.leaderId) : null,
      shift: shift ? { id: shift.id, name: shift.name } : null,
      description: record.description,
      members: record.members.map(technicianRef).filter((m) => m !== null),
      status: record.status
    };
    const request$ = record.id
      ? this.http.put<TeamDto>(`${this.apiUrl}/${record.id}`, { ...body, updatedBy: user })
      : this.http.post<TeamDto>(this.apiUrl, { ...body, createdBy: user, clientId: environment.clientId, tenantId: environment.tenantId });

    return request$.pipe(
      // Keep the fields the API doesn't store yet
      map((dto) => ({ ...record, ...this.toRecord(dto) })),
      tap((saved) => {
        const index = this.records.findIndex((r) => r.id === saved.id);
        // A technician is in one team, so they leave any other team they were in
        const others = this.records.map((r) => (r.id === saved.id ? r : { ...r, members: r.members.filter((m) => !saved.members.includes(m)) }));
        this.records = index >= 0 ? others.map((r, i) => (i === index ? saved : r)) : [...others, saved];
      })
    );
  }

  /** DELETE /api/master-data/teams/{id}; the API refuses a team that still has technicians. */
  remove(record: TeamRecord): Observable<void> {
    return this.http
      .delete<void>(`${this.apiUrl}/${record.id}`)
      .pipe(tap(() => (this.records = this.records.filter((r) => r.id !== record.id))));
  }

  private toRecord(dto: TeamDto): TeamRecord {
    const numberOf = (ref: ReferenceDto) => this.technicians.all().find((t) => t.recordId === ref.id)?.id ?? null;
    // DEMO MODE: the fields the API doesn't store yet keep what this session has
    const cached = this.records.find((r) => r.id === dto.id) ?? blankTeam();
    return {
      ...cached,
      id: dto.id,
      code: dto.code,
      name: dto.teamName,
      department: dto.department?.name ?? '',
      leaderId: dto.teamLeader ? (numberOf(dto.teamLeader) ?? '') : '',
      members: dto.members.map(numberOf).filter((m): m is string => m !== null),
      shiftCode: this.shifts.all().find((s) => s.id === dto.shift?.id)?.code ?? '',
      description: dto.description ?? '',
      status: (['Active', 'On Leave', 'Inactive'].includes(dto.status ?? '') ? dto.status : 'Active') as TeamRecord['status'],
      createdOn: dto.createdAt?.slice(0, 16) ?? '',
      updatedOn: (dto.updatedAt ?? dto.createdAt)?.slice(0, 16) ?? ''
    };
  }
}
