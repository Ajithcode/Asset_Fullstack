import { Component } from '@angular/core';
import { MasterTable } from '../../shared/master-table/master-table';
import { MasterTableConfig } from '../../shared/master-table/master-table.model';

@Component({
  selector: 'app-mdm-maintenance-failure-code',
  imports: [MasterTable],
  template: '<app-master-table [config]="config" />'
})
export class FailureCodePage {
  readonly config: MasterTableConfig = {
    key: 'maintenance/failure-code',
    title: 'Failure Code',
    singular: 'Failure Code',
    codePrefix: 'FC',
    fields: [
      { key: 'problem', label: 'Problem', required: true },
      { key: 'cause', label: 'Cause' },
      { key: 'remedy', label: 'Remedy' },
      { key: 'assetType', label: 'Asset Type', type: 'select', options: ['Diesel Generator', 'Air Handling Unit', 'UPS'] }
    ],
    rows: [
      { code: 'FC-0001', problem: 'Won’t start', cause: 'Battery drained', remedy: 'Replace battery', assetType: 'Diesel Generator', status: 'Active' },
      { code: 'FC-0002', problem: 'Low airflow', cause: 'Clogged filter', remedy: 'Clean / replace filter', assetType: 'Air Handling Unit', status: 'Active' },
      { code: 'FC-0003', problem: 'On battery', cause: 'Input supply failure', remedy: 'Check input breaker', assetType: 'UPS', status: 'Active' }
    ]
  };
}
