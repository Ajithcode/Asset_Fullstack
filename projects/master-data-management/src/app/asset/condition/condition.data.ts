import { ConditionRecord, ConditionType } from './condition.model';

export const CONDITION_TYPES: ConditionType[] = ['Operational', 'Non Operational'];

export function blankCondition(): ConditionRecord {
  return {
    code: '',
    name: '',
    description: '',
    type: 'Operational',
    color: '#4CAF50',
    icon: 'gear',
    image: null,
    healthScore: 50,
    isDefault: false,
    status: 'Active',
    displayOrder: 0,
    showInMobile: true,
    showInReports: true,
    characteristics: { physicalState: '', functionality: '', appearance: '', maintenanceNeed: '', expectedRemainingLife: '' },
    canMoveFrom: [],
    canMoveTo: [],
    triggerCriteria: '',
    autoUpdate: false,
    photoRequired: false,
    commentRequired: false,
    notifyOwner: false,
    blocksAssignment: false,
    usage: { assets: 0, openWorkOrders: 0, slaBreaches: 0, underWarranty: 0 },
    documents: 0,
    createdOn: '',
    createdBy: '',
    updatedOn: ''
  };
}
