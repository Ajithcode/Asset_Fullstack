import { Injectable } from '@angular/core';
import { Observable, map, switchMap } from 'rxjs';
import { MdmAudit } from '../../shared/mdm-api';
import { AssetStore, activeStatus, localStamp } from '../asset-store';
import { LevelIcon } from '../level-icons';
import { blankCondition } from './condition.data';
import { ConditionCharacteristics, ConditionFilters, ConditionRecord, ConditionType } from './condition.model';

/** Condition as `/api/master-data/conditions` returns it. */
interface ConditionDto extends MdmAudit {
  conditionName: string;
  code: string;
  type: string | null;
  color: string | null;
  defaultCondition: boolean;
  description: string | null;
  healthScore: number | null;
  icon: string | null;
  image: string | null;
  displayOrder: number | null;
  showInMobile: boolean;
  showInReports: boolean;
  characteristics: { [K in keyof ConditionCharacteristics]: string | null } | null;
  canMoveFrom: string[] | null;
  canMoveTo: string[] | null;
  triggerCriteria: string | null;
  autoUpdate: boolean;
  photoRequired: boolean;
  commentRequired: boolean;
  notifyOwner: boolean;
  blocksAssignment: boolean;
}

/**
 * Condition values from the master data API. Only one condition is the default (the server clears
 * the others). "Can move to / from" rules are kept mirrored on both conditions, so saving or deleting
 * one condition also saves the other conditions whose rules changed.
 */
@Injectable({ providedIn: 'root' })
export class ConditionService extends AssetStore<ConditionRecord, ConditionDto> {
  protected readonly resource = 'conditions';

  constructor() {
    super();
  }

  nameOf(code: string): string {
    return this.find(code)?.name ?? code;
  }

  filter(f: ConditionFilters): ConditionRecord[] {
    const term = f.search.trim().toLowerCase();
    return this.records.filter(
      (r) =>
        (!f.status || r.status === f.status) &&
        (!f.type || r.type === f.type) &&
        (!term || [r.name, r.code, r.description].some((v) => v.toLowerCase().includes(term)))
    );
  }

  /** Only one condition can be the default, so storing a default clears the others. */
  override save(record: ConditionRecord, originalCode: string | null): void {
    if (record.isDefault) this.records = this.records.map((r) => (r.isDefault && r.code !== originalCode ? { ...r, isDefault: false } : r));
    super.save(record, originalCode);
  }

  /** Saves the condition, then the other conditions whose rules follow from it (code change, mirrored moves). */
  saveWithRules(record: ConditionRecord, originalCode: string | null): Observable<ConditionRecord> {
    return this.persist(record, originalCode).pipe(
      switchMap((saved) => this.persistAll(this.mirroredOthers(saved, originalCode ?? saved.code)).pipe(map(() => saved)))
    );
  }

  /** Deletes the conditions, then drops them from the other conditions' rules. */
  deleteWithRules(codes: string[]): Observable<unknown> {
    return this.delete(codes).pipe(
      switchMap(() => {
        const changed = this.records
          .filter((r) => r.canMoveFrom.some((c) => codes.includes(c)) || r.canMoveTo.some((c) => codes.includes(c)))
          .map((r) => ({ ...r, canMoveFrom: r.canMoveFrom.filter((c) => !codes.includes(c)), canMoveTo: r.canMoveTo.filter((c) => !codes.includes(c)) }));
        return this.persistAll(changed);
      })
    );
  }

  /** Other conditions with `record`'s code change carried in and its rules mirrored; only the ones that differ. */
  private mirroredOthers(record: ConditionRecord, oldCode: string): ConditionRecord[] {
    const same = (a: string[], b: string[]) => a.length === b.length && a.every((c) => b.includes(c));
    return this.records
      .filter((r) => r.code !== record.code)
      .map((r) => {
        const rename = (codes: string[]) => codes.map((c) => (c === oldCode ? record.code : c)).filter((c) => c !== record.code);
        const from = rename(r.canMoveFrom);
        const to = rename(r.canMoveTo);
        if (record.canMoveTo.includes(r.code)) from.push(record.code);
        if (record.canMoveFrom.includes(r.code)) to.push(record.code);
        return { before: r, after: { ...r, canMoveFrom: from, canMoveTo: to } };
      })
      .filter(({ before, after }) => !same(before.canMoveFrom, after.canMoveFrom) || !same(before.canMoveTo, after.canMoveTo))
      .map(({ after }) => after);
  }

  protected toRecord(dto: ConditionDto): ConditionRecord {
    const blank = blankCondition();
    const c = dto.characteristics;
    return {
      ...blank,
      code: dto.code,
      name: dto.conditionName,
      description: dto.description ?? '',
      type: (dto.type as ConditionType) ?? blank.type,
      color: dto.color ?? blank.color,
      icon: (dto.icon as LevelIcon) ?? blank.icon,
      image: dto.image,
      healthScore: dto.healthScore ?? 0,
      isDefault: dto.defaultCondition,
      status: activeStatus(dto.status),
      displayOrder: dto.displayOrder ?? 0,
      showInMobile: dto.showInMobile,
      showInReports: dto.showInReports,
      characteristics: {
        physicalState: c?.physicalState ?? '',
        functionality: c?.functionality ?? '',
        appearance: c?.appearance ?? '',
        maintenanceNeed: c?.maintenanceNeed ?? '',
        expectedRemainingLife: c?.expectedRemainingLife ?? ''
      },
      canMoveFrom: dto.canMoveFrom ?? [],
      canMoveTo: dto.canMoveTo ?? [],
      triggerCriteria: dto.triggerCriteria ?? '',
      autoUpdate: dto.autoUpdate,
      photoRequired: dto.photoRequired,
      commentRequired: dto.commentRequired,
      notifyOwner: dto.notifyOwner,
      blocksAssignment: dto.blocksAssignment,
      createdOn: localStamp(dto.createdAt),
      createdBy: dto.createdBy ?? '',
      updatedOn: localStamp(dto.updatedAt ?? dto.createdAt)
    };
  }

  protected toRequest(r: ConditionRecord): object {
    return {
      conditionName: r.name,
      code: r.code,
      type: r.type,
      color: r.color,
      defaultCondition: r.isDefault,
      description: r.description,
      healthScore: r.healthScore,
      icon: r.icon,
      image: r.image,
      displayOrder: r.displayOrder || null,
      showInMobile: r.showInMobile,
      showInReports: r.showInReports,
      characteristics: r.characteristics,
      canMoveFrom: r.canMoveFrom,
      canMoveTo: r.canMoveTo,
      triggerCriteria: r.triggerCriteria,
      autoUpdate: r.autoUpdate,
      photoRequired: r.photoRequired,
      commentRequired: r.commentRequired,
      notifyOwner: r.notifyOwner,
      blocksAssignment: r.blocksAssignment,
      status: r.status
    };
  }
}
