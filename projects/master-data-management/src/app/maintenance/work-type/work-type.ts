import { Component } from '@angular/core';
import { MasterTable } from '../../shared/master-table/master-table';
import { MasterTableConfig } from '../../shared/master-table/master-table.model';

@Component({
  selector: 'app-mdm-maintenance-work-type',
  imports: [MasterTable],
  template: '<app-master-table [config]="config" />'
})
export class WorkTypePage {
  readonly config: MasterTableConfig = {
    key: 'maintenance/work-type',
    title: 'Work Type',
    singular: 'Work Type',
    codePrefix: 'WT',
    fields: [
      { key: 'name', label: 'Name', required: true },
      { key: 'maintenanceType', label: 'Maintenance Type', type: 'select', options: ['Preventive', 'Corrective', 'Predictive', 'Breakdown'] },
      { key: 'duration', label: 'Default Duration (hrs)', type: 'number' }
    ],
    rows: [
      { code: 'WT-0001', name: 'Inspection', maintenanceType: 'Preventive', duration: 1, status: 'Active' },
      { code: 'WT-0002', name: 'Lubrication', maintenanceType: 'Preventive', duration: 0.5, status: 'Active' },
      { code: 'WT-0003', name: 'Repair', maintenanceType: 'Corrective', duration: 4, status: 'Active' },
      { code: 'WT-0004', name: 'Replacement', maintenanceType: 'Corrective', duration: 6, status: 'Active' }
    ]
  };
}
