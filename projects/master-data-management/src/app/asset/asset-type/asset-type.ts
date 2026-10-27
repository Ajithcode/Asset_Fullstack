import { Component } from '@angular/core';
import { MasterTable } from '../../shared/master-table/master-table';
import { MasterTableConfig } from '../../shared/master-table/master-table.model';

@Component({
  selector: 'app-mdm-asset-asset-type',
  imports: [MasterTable],
  template: '<app-master-table [config]="config" />'
})
export class AssetTypePage {
  readonly config: MasterTableConfig = {
    key: 'asset/asset-type',
    title: 'Asset Type',
    singular: 'Asset Type',
    codePrefix: 'AT',
    fields: [
      { key: 'name', label: 'Name', required: true },
      { key: 'category', label: 'Category', type: 'select', options: ['Electrical', 'Generators', 'HVAC', 'Chillers'] },
      { key: 'trackable', label: 'Trackable', type: 'boolean' }
    ],
    rows: [
      { code: 'AT-0001', name: 'Diesel Generator', category: 'Generators', trackable: true, status: 'Active' },
      { code: 'AT-0002', name: 'Air Handling Unit', category: 'HVAC', trackable: true, status: 'Active' },
      { code: 'AT-0003', name: 'UPS', category: 'Electrical', trackable: false, status: 'Active' }
    ]
  };
}
