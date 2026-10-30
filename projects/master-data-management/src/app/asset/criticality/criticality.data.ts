import { BusinessImpact, CriticalityRecord, MaintenancePriority, MonitoringNeed, Scale } from './criticality.model';

export const SCALE: Scale[] = ['Very Low', 'Low', 'Medium', 'High', 'Very High'];
export const PRIORITIES: MaintenancePriority[] = ['Lowest', 'Low', 'Medium', 'High', 'Highest'];
export const MONITORING: MonitoringNeed[] = ['Required', 'Recommended', 'Optional', 'Not Required'];
export const STRATEGIES = ['Predictive + Preventive', 'Preventive', 'Condition Based', 'Corrective', 'Run to Failure'];
export const INSPECTION_FREQUENCIES = ['Weekly', 'Monthly', 'Quarterly', 'Half-yearly', 'Yearly', 'None'];

function impacts([safety, operational, financial, regulatory, reputation, environmental]: string[]): BusinessImpact {
  return { safety, operational, financial, regulatory, reputation, environmental };
}

export function blankCriticality(): CriticalityRecord {
  return {
    code: '',
    name: '',
    description: '',
    color: '#EF4444',
    icon: 'shield',
    image: null,
    riskLevel: 'Medium',
    maintenancePriority: 'Medium',
    isDefault: false,
    status: 'Active',
    likelihood: 'Medium',
    impact: 'Medium',
    businessImpact: impacts(['', '', '', '', '', '']),
    recommendedStrategy: 'Preventive',
    inspectionFrequency: 'Quarterly',
    conditionMonitoring: 'Optional',
    sparePartsPriority: 'Medium',
    responseTimeHours: 24,
    escalateOnFailure: false,
    rootCauseRequired: false,
    approvalForDisposal: false,
    showInReports: true,
    assets: { total: 0, underMaintenance: 0, retired: 0 },
    documents: 0,
    createdOn: '',
    createdBy: '',
    updatedOn: ''
  };
}
