import { Injectable } from '@angular/core';
import { Observable, map } from 'rxjs';
import { ApiCodeStore } from '../../shared/code-store';
import { MdmAudit } from '../../shared/mdm-api';
import { AssetModelIcon } from '../../asset/asset-model/asset-model.icons';
import { LoadOnce } from '../load-once';
import { apiText, stampOf } from '../people.data';
import { SkillRecord, SkillStatus, SkillType } from './skill.model';

interface SkillDto extends MdmAudit {
  skillName: string;
  code: string;
  category: string | null;
  type: string | null;
  description: string | null;
  icon: string | null;
  levels: string[];
  relatedRoles: string[];
  certificationRequired: boolean;
  certificationValidityMonths: number | null;
  assessmentRequired: boolean;
}

/**
 * Skills from /api/master-data/skills. Employees, technicians and service providers refer to skills by
 * name; the API renames them there when a skill is renamed.
 */
@Injectable({ providedIn: 'root' })
export class SkillService extends ApiCodeStore<SkillRecord, SkillDto> {
  protected readonly resource = 'skills';
  private readonly loader = new LoadOnce(() => this.load());

  constructor() {
    super();
  }

  /** All skills; the first read starts loading them. */
  override all(): SkillRecord[] {
    this.loadOnFirstRead();
    return this.records;
  }

  private autoLoad = true;

  // Deferred: reads happen while templates render, where the loading signal can't be written
  private loadOnFirstRead(): void {
    if (!this.autoLoad) return;
    this.autoLoad = false;
    queueMicrotask(() => this.ensureLoaded());
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
  refresh(): Observable<SkillRecord[]> {
    return this.loader.refresh();
  }

  /** Active skill names A-Z, plus `keep` so existing choices stay selectable. */
  activeNames(keep: string[] = []): string[] {
    this.loadOnFirstRead();
    return this.records
      .filter((r) => r.status === 'Active' || keep.includes(r.name))
      .map((r) => r.name)
      .sort((a, b) => a.localeCompare(b));
  }

  setStatus(record: SkillRecord, status: SkillStatus): Observable<SkillRecord> {
    return this.api.setStatus<SkillDto>(this.resource, this.idOf(record.code)!, status).pipe(map((dto) => this.replace(dto)));
  }

  private replace(dto: SkillDto): SkillRecord {
    const record = this.toRecord(dto);
    this.save(record, record.code);
    return record;
  }

  protected toRecord(dto: SkillDto): SkillRecord {
    return {
      code: dto.code,
      name: dto.skillName,
      category: dto.category ?? '',
      type: (dto.type as SkillType) || 'Hard Skill',
      description: dto.description ?? '',
      icon: (dto.icon as AssetModelIcon) || 'wrench',
      status: (dto.status as SkillStatus) || 'Active',
      levels: dto.levels ?? [],
      relatedRoles: dto.relatedRoles ?? [],
      certificationRequired: dto.certificationRequired,
      certificationValidityMonths: dto.certificationValidityMonths ?? 0,
      assessmentRequired: dto.assessmentRequired,
      createdOn: stampOf(dto.createdAt),
      updatedOn: stampOf(dto.updatedAt)
    };
  }

  protected toRequest(r: SkillRecord): object {
    return {
      skillName: r.name,
      code: r.code,
      category: apiText(r.category),
      type: r.type,
      description: apiText(r.description),
      icon: r.icon,
      levels: r.levels,
      relatedRoles: r.relatedRoles,
      certificationRequired: r.certificationRequired,
      certificationValidityMonths: r.certificationRequired ? r.certificationValidityMonths : null,
      assessmentRequired: r.assessmentRequired,
      status: r.status
    };
  }
}
