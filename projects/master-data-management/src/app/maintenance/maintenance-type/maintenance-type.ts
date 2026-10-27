import { Component } from '@angular/core';
import { MasterTable } from '../../shared/master-table/master-table';
import { MasterTableConfig } from '../../shared/master-table/master-table.model';

@Component({
  selector: 'app-mdm-maintenance-maintenance-type',
  imports: [MasterTable],
  template: '<app-master-table [config]="config" />'
})
export class MaintenanceTypePage {
  readonly config: MasterTableConfig = {
    key: 'maintenance/maintenance-type',
    title: 'Maintenance Type',
    singular: 'Maintenance Type',
    codePrefix: 'MT',
    fields: [
      { key: 'name', label: 'Name', required: true },
      { key: 'planned', label: 'Planned', type: 'boolean' },
      { key: 'description', label: 'Description' }
    ],
    rows: [
      { code: 'MT-0001', name: 'Preventive', planned: true, description: 'Scheduled by time or usage', status: 'Active' },
      { code: 'MT-0002', name: 'Corrective', planned: false, description: 'Fix after a failure', status: 'Active' },
      { code: 'MT-0003', name: 'Predictive', planned: true, description: 'Triggered by condition data', status: 'Active' },
      { code: 'MT-0004', name: 'Breakdown', planned: false, description: 'Emergency repair', status: 'Active' }
    ]
  };
}
