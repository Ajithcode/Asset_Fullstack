import { Component } from '@angular/core';
import { MasterTable } from '../../shared/master-table/master-table';
import { MasterTableConfig } from '../../shared/master-table/master-table.model';

@Component({
  selector: 'app-mdm-system-configuration-api-sync-status',
  imports: [MasterTable],
  template: '<app-master-table [config]="config" />'
})
export class ApiSyncStatusPage {
  readonly config: MasterTableConfig = {
    key: 'system-configuration/api-sync-status',
    title: 'API Sync Status',
    singular: 'API Sync Status',
    codePrefix: 'SYNC',
    fields: [
      { key: 'name', label: 'Integration', required: true },
      { key: 'endpoint', label: 'Endpoint' },
      { key: 'lastSync', label: 'Last Sync', type: 'date' },
      { key: 'result', label: 'Result', type: 'select', options: ['Success', 'Partial', 'Failed', 'Pending'] }
    ],
    rows: [
      { code: 'SYNC-0001', name: 'SAP ERP', endpoint: '/api/erp/assets', lastSync: '2026-10-26', result: 'Success', status: 'Active' },
      { code: 'SYNC-0002', name: 'BMS', endpoint: '/api/bms/points', lastSync: '2026-10-26', result: 'Partial', status: 'Active' },
      { code: 'SYNC-0003', name: 'HRMS', endpoint: '/api/hr/employees', lastSync: '2026-10-20', result: 'Failed', status: 'Inactive' }
    ]
  };
}
