import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { ApiCodeStore } from '../../shared/code-store';
import { MdmAudit } from '../../shared/mdm-api';
import { LoadOnce } from '../load-once';
import { MdmRef, apiDate, apiText, stampOf } from '../people.data';
import { WEEK_DAYS } from './shift.data';
import { ShiftBreak, ShiftRecord, ShiftStatus, ShiftType } from './shift.model';

interface ShiftDto extends MdmAudit {
  shiftName: string;
  code: string;
  shiftType: string | null;
  startTime: string;
  endTime: string;
  workingDays: string[];
  color: string | null;
  description: string | null;
  effectiveFrom: string | null;
  graceMinutes: number;
  rotationPattern: string | null;
  breaks: { name: string; start: string | null; minutes: number }[];
  overtimeAllowed: boolean;
  nightAllowance: boolean;
  attendanceRequired: boolean;
}

/** Shifts from /api/master-data/shifts. Teams point at their shift. */
@Injectable({ providedIn: 'root' })
export class ShiftService extends ApiCodeStore<ShiftRecord, ShiftDto> {
  protected readonly resource = 'shifts';
  private readonly loader = new LoadOnce(() => this.load());

  constructor() {
    super();
  }

  /** Loads the list once, for pages and pickers that only read it. */
  ensureLoaded(): void {
    this.loader.ensure();
  }

  /** Completes once the list has been loaded (at most one request). */
  loadOnce(): Observable<unknown> {
    return this.loader.once();
  }

  /** Reloads the list from the API. */
  refresh(): Observable<ShiftRecord[]> {
    return this.loader.refresh();
  }

  /** `{ id, name }` link the API takes for the shift, or null. */
  ref(code: string | null | undefined): MdmRef | null {
    const shift = this.find(code);
    const id = code ? this.idOf(code) : undefined;
    return shift && id ? { id, name: shift.name } : null;
  }

  /** Code of the shift with this record id. */
  codeOfId(id: string | null | undefined): string {
    return id ? (this.records.find((r) => this.idOf(r.code) === id)?.code ?? '') : '';
  }

  setStatus(record: ShiftRecord, status: ShiftStatus): Observable<ShiftRecord> {
    return this.api.setStatus<ShiftDto>(this.resource, this.idOf(record.code)!, status).pipe(
      map((dto) => {
        const saved = this.toRecord(dto);
        this.save(saved, saved.code);
        return saved;
      })
    );
  }

  protected toRecord(dto: ShiftDto): ShiftRecord {
    return {
      code: dto.code,
      name: dto.shiftName,
      shiftType: (dto.shiftType as ShiftType) || 'Regular',
      startTime: dto.startTime ?? '',
      endTime: dto.endTime ?? '',
      color: (dto.color ?? '').toLowerCase(),
      // The API keeps full day names; the page uses Mon, Tue...
      workingDays: WEEK_DAYS.filter((d) => (dto.workingDays ?? []).some((w) => w.slice(0, 3) === d)),
      description: dto.description ?? '',
      status: (dto.status as ShiftStatus) || 'Active',
      effectiveFrom: dto.effectiveFrom ?? '',
      graceMinutes: dto.graceMinutes ?? 0,
      rotationPattern: dto.rotationPattern ?? '',
      breaks: (dto.breaks ?? []).map((b): ShiftBreak => ({ name: b.name, start: b.start ?? '', minutes: b.minutes })),
      overtimeAllowed: dto.overtimeAllowed,
      nightAllowance: dto.nightAllowance,
      attendanceRequired: dto.attendanceRequired,
      createdOn: stampOf(dto.createdAt),
      updatedOn: stampOf(dto.updatedAt)
    };
  }

  protected toRequest(r: ShiftRecord): object {
    return {
      shiftName: r.name,
      code: r.code,
      shiftType: r.shiftType,
      startTime: apiText(r.startTime),
      endTime: apiText(r.endTime),
      workingDays: r.workingDays,
      color: apiText(r.color),
      description: apiText(r.description),
      effectiveFrom: apiDate(r.effectiveFrom),
      graceMinutes: r.graceMinutes,
      rotationPattern: apiText(r.rotationPattern),
      breaks: r.breaks.map((b) => ({ name: b.name, start: apiText(b.start), minutes: b.minutes })),
      overtimeAllowed: r.overtimeAllowed,
      nightAllowance: r.nightAllowance,
      attendanceRequired: r.attendanceRequired,
      status: r.status
    };
  }
}
