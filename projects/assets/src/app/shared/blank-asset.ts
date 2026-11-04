import { AssetRecord } from 'shared-ui';

/** An empty register row, shown until the Asset API has returned the asset being viewed. */
export function blankAssetRecord(tag = ''): AssetRecord {
  return {
    id: 0,
    name: '',
    tag,
    category: '',
    type: '',
    site: '',
    area: '',
    custodian: '',
    department: '',
    status: 'Active',
    criticality: 'Low',
    manufacturer: '',
    model: '',
    nextMaintenance: null,
    icon: 'generator',
    image: '',
  };
}
