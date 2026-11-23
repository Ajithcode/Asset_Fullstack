import { AssetSubTypeRecord } from './asset-sub-type.model';

export function blankAssetSubType(): AssetSubTypeRecord {
  return {
    code: '',
    name: '',
    assetType: '',
    category: '',
    subcategory: '',
    icon: 'conveyor',
    image: null,
    description: '',
    status: 'Active',
    taggingFormat: '',
    specifications: [],
    lifecycleYears: 5,
    maintenanceType: 'Preventive',
    maintenanceFrequencyDays: 90,
    warrantyMonths: 12,
    defaultCondition: 'New',
    trackingMethod: 'RFID',
    serialRequired: true,
    inspectionRequired: false,
    iotTracking: false,
    customFields: [],
    assetCount: 0,
    createdOn: '',
    createdBy: '',
    updatedOn: ''
  };
}
