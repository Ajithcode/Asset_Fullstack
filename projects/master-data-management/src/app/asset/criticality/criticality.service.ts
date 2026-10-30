import { Injectable } from '@angular/core';
import { MdmAudit } from '../../shared/mdm-api';
import { AssetStore, activeStatus, localStamp } from '../asset-store';
import { LevelIcon } from '../level-icons';
import { blankCriticality } from './criticality.data';
import { BusinessImpact, CriticalityFilters, CriticalityRecord, MaintenancePriority, MonitoringNeed, Scale } from './criticality.model';

/** Criticality as `/api/master-data/criticalities` returns it. */
interface CriticalityDto extends MdmAudit {
  criticalityName: string;
  code: string;
  riskLevel: string | null;
  maintenancePriority: string | null;
  color: string | null;
  defaultCriticality: boolean;
  description: string | null;
  icon: string | null;
  image: string | null;
  likelihood: string | null;
  impact: string | null;
  businessImpact: { [K in keyof BusinessImpact]: string | null } | null;
  recommendedStrategy: string | null;
  inspectionFrequency: string | null;
  conditionMonitoring: string | null;
  sparePartsPriority: string | null;
  responseTimeHours: number | null;
  escalateOnFailure: boolean;
  rootCauseRequired: boolean;
  approvalForDisposal: boolean;
  showInReports: boolean;
}

/** Criticality levels from the master data API. Only one level is the default; the server clears the others. */
@Injectable({ providedIn: 'root' })
export class CriticalityService extends AssetStore<CriticalityRecord, CriticalityDto> {
  protected readonly resource = 'criticalities';

  constructor() {
    super();
  }

  filter(f: CriticalityFilters): CriticalityRecord[] {
    const term = f.search.trim().toLowerCase();
    return this.records.filter(
      (r) => (!f.status || r.status === f.status) && (!term || [r.name, r.code, r.description].some((v) => v.toLowerCase().includes(term)))
    );
  }

  /** Only one level can be the default, so storing a default clears the others. */
  override save(record: CriticalityRecord, originalCode: string | null): void {
    if (record.isDefault) this.records = this.records.map((r) => (r.isDefault ? { ...r, isDefault: false } : r));
    super.save(record, originalCode);
  }

  protected toRecord(dto: CriticalityDto): CriticalityRecord {
    const blank = blankCriticality();
    const impact = dto.businessImpact;
    return {
      ...blank,
      code: dto.code,
      name: dto.criticalityName,
      description: dto.description ?? '',
      color: dto.color ?? blank.color,
      icon: (dto.icon as LevelIcon) ?? blank.icon,
      image: dto.image,
      riskLevel: (dto.riskLevel as Scale) ?? blank.riskLevel,
      maintenancePriority: (dto.maintenancePriority as MaintenancePriority) ?? blank.maintenancePriority,
      isDefault: dto.defaultCriticality,
      status: activeStatus(dto.status),
      likelihood: (dto.likelihood as Scale) ?? blank.likelihood,
      impact: (dto.impact as Scale) ?? blank.impact,
      businessImpact: {
        safety: impact?.safety ?? '',
        operational: impact?.operational ?? '',
        financial: impact?.financial ?? '',
        regulatory: impact?.regulatory ?? '',
        reputation: impact?.reputation ?? '',
        environmental: impact?.environmental ?? ''
      },
      recommendedStrategy: dto.recommendedStrategy ?? blank.recommendedStrategy,
      inspectionFrequency: dto.inspectionFrequency ?? blank.inspectionFrequency,
      conditionMonitoring: (dto.conditionMonitoring as MonitoringNeed) ?? blank.conditionMonitoring,
      sparePartsPriority: (dto.sparePartsPriority as MaintenancePriority) ?? blank.sparePartsPriority,
      responseTimeHours: dto.responseTimeHours ?? blank.responseTimeHours,
      escalateOnFailure: dto.escalateOnFailure,
      rootCauseRequired: dto.rootCauseRequired,
      approvalForDisposal: dto.approvalForDisposal,
      showInReports: dto.showInReports,
      createdOn: localStamp(dto.createdAt),
      createdBy: dto.createdBy ?? '',
      updatedOn: localStamp(dto.updatedAt ?? dto.createdAt)
    };
  }

  protected toRequest(r: CriticalityRecord): object {
    return {
      criticalityName: r.name,
      code: r.code,
      riskLevel: r.riskLevel,
      maintenancePriority: r.maintenancePriority,
      color: r.color,
      defaultCriticality: r.isDefault,
      description: r.description,
      icon: r.icon,
      image: r.image,
      likelihood: r.likelihood,
      impact: r.impact,
      businessImpact: r.businessImpact,
      recommendedStrategy: r.recommendedStrategy,
      inspectionFrequency: r.inspectionFrequency,
      conditionMonitoring: r.conditionMonitoring,
      sparePartsPriority: r.sparePartsPriority,
      responseTimeHours: r.responseTimeHours,
      escalateOnFailure: r.escalateOnFailure,
      rootCauseRequired: r.rootCauseRequired,
      approvalForDisposal: r.approvalForDisposal,
      showInReports: r.showInReports,
      status: r.status
    };
  }
}
