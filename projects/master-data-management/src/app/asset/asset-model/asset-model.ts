import { Component } from '@angular/core';
import { MasterTable } from '../../shared/master-table/master-table';
import { MasterTableConfig } from '../../shared/master-table/master-table.model';

@Component({
  selector: 'app-mdm-asset-asset-model',
  imports: [MasterTable],
  template: '<app-master-table [config]="config" />'
})
export class AssetModelPage {
  readonly config: MasterTableConfig = {
    key: 'asset/asset-model',
    title: 'Asset Model',
    singular: 'Asset Model',
    codePrefix: 'MDL',
    fields: [
      { key: 'name', label: 'Model Name', required: true },
      { key: 'manufacturer', label: 'Manufacturer', type: 'select', options: ['Cummins', 'Carrier', 'Schneider Electric'] },
      { key: 'assetType', label: 'Asset Type', type: 'select', options: ['Diesel Generator', 'Air Handling Unit', 'UPS'] },
      { key: 'lifeYears', label: 'Expected Life (years)', type: 'number' }
    ],
    rows: [
      { code: 'MDL-0001', name: 'C500 D5', manufacturer: 'Cummins', assetType: 'Diesel Generator', lifeYears: 15, status: 'Active' },
      { code: 'MDL-0002', name: '39G AHU', manufacturer: 'Carrier', assetType: 'Air Handling Unit', lifeYears: 12, status: 'Active' },
      { code: 'MDL-0003', name: 'Galaxy VS 40kVA', manufacturer: 'Schneider Electric', assetType: 'UPS', lifeYears: 10, status: 'Active' }
    ]
  };
}
