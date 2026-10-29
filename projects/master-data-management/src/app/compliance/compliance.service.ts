import { Injectable } from '@angular/core';
import { ApiCodeStore, MasterDto } from '../shared/api-code-store';
import { blankCertificationType, blankPermitType, blankRegulation, blankStandard } from './compliance.data';
import { CertificationTypeRecord, PermitTypeRecord, RegulationRecord, StandardRecord } from './compliance.model';

/*
 * Compliance masters from {projectApiUrl}api/master-data/... Certification types and regulations
 * refer to standards by code. Fields the API doesn't store yet are kept for the session only.
 */

interface StandardDto extends MasterDto {
  standardName: string;
  // The standards body, e.g. ISO
  organization: string;
  category: string;
  version: string | null;
  description: string | null;
}

/** Standards; title, dates, link and the mandatory flag are UI-only for now. */
@Injectable({ providedIn: 'root' })
export class StandardService extends ApiCodeStore<StandardRecord, StandardDto> {
  constructor() {
    super('standards');
    this.load();
  }

  protected toRecord(dto: StandardDto, cached: StandardRecord | undefined): StandardRecord {
    return {
      ...(cached ?? blankStandard()),
      name: dto.standardName,
      organization: dto.organization,
      category: dto.category,
      version: dto.version ?? '',
      description: dto.description ?? ''
    };
  }

  protected toRequest(r: StandardRecord) {
    return { standardName: r.name, code: r.code, organization: r.organization, category: r.category, version: r.version, description: r.description };
  }
}

interface CertificationTypeDto extends MasterDto {
  certificationTypeName: string;
  category: string;
  applicableTo: string[];
  validityMonths: number | null;
  renewalReminderDays: number | null;
  description: string | null;
  standardCode: string | null;
}

/** Certification types; issuing body, required documents and settings are UI-only for now. */
@Injectable({ providedIn: 'root' })
export class CertificationTypeService extends ApiCodeStore<CertificationTypeRecord, CertificationTypeDto> {
  constructor() {
    super('certification-types');
    this.load();
  }

  usingStandard(code: string): CertificationTypeRecord[] {
    return this.records.filter((r) => r.standardCode === code);
  }

  // Cache only; the next save of each certification type stores the new code
  recodeStandard(from: string, to: string): void {
    if (from === to) return;
    this.records = this.records.map((r) => (r.standardCode === from ? { ...r, standardCode: to } : r));
  }

  protected toRecord(dto: CertificationTypeDto, cached: CertificationTypeRecord | undefined): CertificationTypeRecord {
    const base = cached ?? blankCertificationType();
    return {
      ...base,
      name: dto.certificationTypeName,
      category: dto.category,
      applicableTo: dto.applicableTo,
      validityMonths: dto.validityMonths ?? base.validityMonths,
      renewalReminderDays: dto.renewalReminderDays ?? base.renewalReminderDays,
      description: dto.description ?? '',
      standardCode: dto.standardCode ?? ''
    };
  }

  protected toRequest(r: CertificationTypeRecord) {
    return {
      certificationTypeName: r.name,
      code: r.code,
      category: r.category,
      applicableTo: r.applicableTo,
      validityMonths: r.validityMonths,
      renewalReminderDays: r.renewalReminderDays,
      description: r.description,
      standardCode: r.standardCode || null
    };
  }
}

interface PermitTypeDto extends MasterDto {
  permitTypeName: string;
  category: string;
  validTillDays: number | null;
  description: string | null;
}

/** Permit types; the approval workflow and safety checks are UI-only for now. */
@Injectable({ providedIn: 'root' })
export class PermitTypeService extends ApiCodeStore<PermitTypeRecord, PermitTypeDto> {
  constructor() {
    super('permit-types');
    this.load();
  }

  protected toRecord(dto: PermitTypeDto, cached: PermitTypeRecord | undefined): PermitTypeRecord {
    const base = cached ?? blankPermitType();
    return { ...base, name: dto.permitTypeName, category: dto.category, validDays: dto.validTillDays ?? base.validDays, description: dto.description ?? '' };
  }

  protected toRequest(r: PermitTypeRecord) {
    return { permitTypeName: r.name, code: r.code, category: r.category, validTillDays: r.validDays, description: r.description };
  }
}

interface RegulationDto extends MasterDto {
  regulationName: string;
  country: string;
  category: string;
  issuingAuthority: string | null;
  description: string | null;
  standardCodes: string[];
}

/** Regulations; asset categories, effective date, review period and penalties are UI-only for now. */
@Injectable({ providedIn: 'root' })
export class RegulationService extends ApiCodeStore<RegulationRecord, RegulationDto> {
  constructor() {
    super('regulations');
    this.load();
  }

  usingStandard(code: string): RegulationRecord[] {
    return this.records.filter((r) => r.standardCodes.includes(code));
  }

  // Cache only; the next save of each regulation stores the new code
  recodeStandard(from: string, to: string): void {
    if (from === to) return;
    this.records = this.records.map((r) => (r.standardCodes.includes(from) ? { ...r, standardCodes: r.standardCodes.map((c) => (c === from ? to : c)) } : r));
  }

  protected toRecord(dto: RegulationDto, cached: RegulationRecord | undefined): RegulationRecord {
    return {
      ...(cached ?? blankRegulation()),
      name: dto.regulationName,
      country: dto.country,
      category: dto.category,
      issuingAuthority: dto.issuingAuthority ?? '',
      description: dto.description ?? '',
      standardCodes: dto.standardCodes
    };
  }

  protected toRequest(r: RegulationRecord) {
    return {
      regulationName: r.name,
      code: r.code,
      country: r.country,
      category: r.category,
      issuingAuthority: r.issuingAuthority,
      description: r.description,
      standardCodes: r.standardCodes
    };
  }
}
