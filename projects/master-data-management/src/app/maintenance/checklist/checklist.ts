import { Component } from '@angular/core';
import { MasterTable } from '../../shared/master-table/master-table';
import { MasterTableConfig } from '../../shared/master-table/master-table.model';

@Component({
  selector: 'app-mdm-maintenance-checklist',
  imports: [MasterTable],
  template: '<app-master-table [config]="config" />'
})
export class ChecklistPage {
  readonly config: MasterTableConfig = {
    key: 'maintenance/checklist',
    title: 'Checklist',
    singular: 'Checklist',
    codePrefix: 'CL',
    fields: [
      { key: 'name', label: 'Name', required: true },
      { key: 'assetType', label: 'Asset Type', type: 'select', options: ['Diesel Generator', 'Air Handling Unit', 'UPS'] },
      { key: 'itemCount', label: 'No. of Items', type: 'number' },
      { key: 'mandatory', label: 'Mandatory', type: 'boolean' }
    ],
    rows: [
      { code: 'CL-0001', name: 'DG pre-start checklist', assetType: 'Diesel Generator', itemCount: 12, mandatory: true, status: 'Active' },
      { code: 'CL-0002', name: 'AHU quarterly checklist', assetType: 'Air Handling Unit', itemCount: 8, mandatory: false, status: 'Active' }
    ]
  };
}
