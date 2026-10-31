import { Injectable } from '@angular/core';
import { MdmAudit } from '../shared/mdm-api';
import { MasterApiStore, dateOrNull, stampOf } from '../maintenance/master-api-store';
import { CertificationTypeRecord, ComplianceStatus, PermitTypeRecord, RegulationRecord, StandardRecord } from './compliance.model';

/*
 * Compliance masters, backed by /api/master-data/{standards, certification-types, permit-types, regulations}.
 * Certification types and regulations refer to standards by code.
 */

const statusOf = (status: string | null): ComplianceStatus => (status === 'Inactive' ? 'Inactive' : 'Active');

interface StandardDto extends MdmAudit {
  standardName: string;
  code: string;
  organization: string;
  category: string;
  version: string | null;
  description: string | null;
  title: string | null;
  publishedOn: string | null;
  nextReview: string | null;
  url: string | null;
  mandatory: boolean;
}

@Injectable({ providedIn: 'root' })
export class StandardService extends MasterApiStore<StandardRecord, StandardDto> {
  protected readonly resource = 'standards';

  constructor() {
    super();
  }

  protected toRecord(dto: StandardDto): StandardRecord {
    return {
      code: dto.code,
      name: dto.standardName,
      organization: dto.organization,
      category: dto.category,
      version: dto.version ?? '',
      description: dto.description ?? '',
      status: statusOf(dto.status),
      title: dto.title ?? '',
      publishedOn: dateOrNull(dto.publishedOn) ?? '',
      nextReview: dateOrNull(dto.nextReview) ?? '',
      url: dto.url ?? '',
      mandatory: dto.mandatory,
      createdOn: stampOf(dto.createdAt),
      updatedOn: stampOf(dto.updatedAt ?? dto.createdAt)
    };
  }

  protected toRequest(r: StandardRecord): object {
    return {
      standardName: r.name,
      code: r.code,
      organization: r.organization,
      category: r.category,
      version: r.version,
      description: r.description,
      title: r.title,
      publishedOn: dateOrNull(r.publishedOn),
      nextReview: dateOrNull(r.nextReview),
      url: r.url,
      mandatory: r.mandatory,
      status: r.status
    };
  }
}

interface CertificationTypeDto extends MdmAudit {
  certificationTypeName: string;
  code: string;
  category: string;
  applicableTo: string[];
  validityMonths: number | null;
  renewalReminderDays: number | null;
  description: string | null;
  issuingBody: string | null;
  standardCode: string | null;
  requiredDocuments: string[];
  inspectionRequired: boolean;
  blockWhenExpired: boolean;
  notifyOwner: boolean;
}

@Injectable({ providedIn: 'root' })
export class CertificationTypeService extends MasterApiStore<CertificationTypeRecord, CertificationTypeDto> {
  protected readonly resource = 'certification-types';

  constructor() {
    super();
  }

  usingStandard(code: string): CertificationTypeRecord[] {
    return this.records.filter((r) => r.standardCode === code);
  }

  /** Follows a standard whose code changed; the API updates the stored codes itself. */
  recodeStandard(from: string, to: string): void {
    if (from === to) return;
    this.records = this.records.map((r) => (r.standardCode === from ? { ...r, standardCode: to } : r));
  }

  protected toRecord(dto: CertificationTypeDto): CertificationTypeRecord {
    return {
      code: dto.code,
      name: dto.certificationTypeName,
      category: dto.category,
      applicableTo: dto.applicableTo ?? [],
      validityMonths: dto.validityMonths ?? 0,
      renewalReminderDays: dto.renewalReminderDays ?? 0,
      description: dto.description ?? '',
      status: statusOf(dto.status),
      issuingBody: dto.issuingBody ?? '',
      standardCode: dto.standardCode ?? '',
      requiredDocuments: dto.requiredDocuments ?? [],
      inspectionRequired: dto.inspectionRequired,
      blockWhenExpired: dto.blockWhenExpired,
      notifyOwner: dto.notifyOwner,
      createdOn: stampOf(dto.createdAt),
      updatedOn: stampOf(dto.updatedAt ?? dto.createdAt)
    };
  }

  protected toRequest(r: CertificationTypeRecord): object {
    return {
      certificationTypeName: r.name,
      code: r.code,
      category: r.category,
      applicableTo: r.applicableTo,
      validityMonths: r.validityMonths,
      renewalReminderDays: r.renewalReminderDays,
      description: r.description,
      issuingBody: r.issuingBody,
      standardCode: r.standardCode || null,
      requiredDocuments: r.requiredDocuments,
      inspectionRequired: r.inspectionRequired,
      blockWhenExpired: r.blockWhenExpired,
      notifyOwner: r.notifyOwner,
      status: r.status
    };
  }
}

interface PermitTypeDto extends MdmAudit {
  permitTypeName: string;
  code: string;
  category: string;
  validTillDays: number | null;
  description: string | null;
  approvalLevels: number | null;
  approverRoles: string[];
  maxExtensionDays: number | null;
  isolationRequired: boolean;
  gasTestRequired: boolean;
  closeOutInspection: boolean;
}

@Injectable({ providedIn: 'root' })
export class PermitTypeService extends MasterApiStore<PermitTypeRecord, PermitTypeDto> {
  protected readonly resource = 'permit-types';

  constructor() {
    super();
  }

  protected toRecord(dto: PermitTypeDto): PermitTypeRecord {
    return {
      code: dto.code,
      name: dto.permitTypeName,
      category: dto.category,
      validDays: dto.validTillDays ?? 1,
      description: dto.description ?? '',
      status: statusOf(dto.status),
      approvalLevels: dto.approvalLevels ?? 1,
      approverRoles: dto.approverRoles ?? [],
      maxExtensionDays: dto.maxExtensionDays ?? 0,
      isolationRequired: dto.isolationRequired,
      gasTestRequired: dto.gasTestRequired,
      closeOutInspection: dto.closeOutInspection,
      createdOn: stampOf(dto.createdAt),
      updatedOn: stampOf(dto.updatedAt ?? dto.createdAt)
    };
  }

  protected toRequest(r: PermitTypeRecord): object {
    return {
      permitTypeName: r.name,
      code: r.code,
      category: r.category,
      validTillDays: r.validDays,
      description: r.description,
      approvalLevels: r.approvalLevels,
      approverRoles: r.approverRoles,
      maxExtensionDays: r.maxExtensionDays,
      isolationRequired: r.isolationRequired,
      gasTestRequired: r.gasTestRequired,
      closeOutInspection: r.closeOutInspection,
      status: r.status
    };
  }
}

interface RegulationDto extends MdmAudit {
  regulationName: string;
  code: string;
  country: string;
  category: string;
  issuingAuthority: string | null;
  description: string | null;
  assetCategories: string[];
  standardCodes: string[];
  effectiveDate: string | null;
  reviewMonths: number | null;
  penalties: string | null;
}

@Injectable({ providedIn: 'root' })
export class RegulationService extends MasterApiStore<RegulationRecord, RegulationDto> {
  protected readonly resource = 'regulations';

  constructor() {
    super();
  }

  usingStandard(code: string): RegulationRecord[] {
    return this.records.filter((r) => r.standardCodes.includes(code));
  }

  /** Follows a standard whose code changed; the API updates the stored codes itself. */
  recodeStandard(from: string, to: string): void {
    if (from === to) return;
    this.records = this.records.map((r) => (r.standardCodes.includes(from) ? { ...r, standardCodes: r.standardCodes.map((c) => (c === from ? to : c)) } : r));
  }

  protected toRecord(dto: RegulationDto): RegulationRecord {
    return {
      code: dto.code,
      name: dto.regulationName,
      country: dto.country,
      category: dto.category,
      issuingAuthority: dto.issuingAuthority ?? '',
      description: dto.description ?? '',
      status: statusOf(dto.status),
      assetCategories: dto.assetCategories ?? [],
      standardCodes: dto.standardCodes ?? [],
      effectiveDate: dateOrNull(dto.effectiveDate) ?? '',
      reviewMonths: dto.reviewMonths ?? 12,
      penalties: dto.penalties ?? '',
      createdOn: stampOf(dto.createdAt),
      updatedOn: stampOf(dto.updatedAt ?? dto.createdAt)
    };
  }

  protected toRequest(r: RegulationRecord): object {
    return {
      regulationName: r.name,
      code: r.code,
      country: r.country,
      category: r.category,
      issuingAuthority: r.issuingAuthority,
      description: r.description,
      assetCategories: r.assetCategories,
      standardCodes: r.standardCodes,
      effectiveDate: dateOrNull(r.effectiveDate),
      reviewMonths: r.reviewMonths,
      penalties: r.penalties,
      status: r.status
    };
  }
}
