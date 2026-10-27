import { Component } from '@angular/core';
import { MasterTable } from '../../shared/master-table/master-table';
import { MasterTableConfig } from '../../shared/master-table/master-table.model';

@Component({
  selector: 'app-mdm-security-permission-master',
  imports: [MasterTable],
  template: '<app-master-table [config]="config" />'
})
export class PermissionMasterPage {
  readonly config: MasterTableConfig = {
    key: 'security/permission-master',
    title: 'Permission Master',
    singular: 'Permission',
    codePrefix: 'PRM',
    fields: [
      { key: 'name', label: 'Name', required: true },
      { key: 'action', label: 'Action', type: 'select', options: ['View', 'Create', 'Edit', 'Delete', 'Approve', 'Export'] },
      { key: 'description', label: 'Description' }
    ],
    rows: [
      { code: 'PRM-0001', name: 'View records', action: 'View', description: 'Read-only access', status: 'Active' },
      { code: 'PRM-0002', name: 'Create records', action: 'Create', description: '', status: 'Active' },
      { code: 'PRM-0003', name: 'Edit records', action: 'Edit', description: '', status: 'Active' },
      { code: 'PRM-0004', name: 'Delete records', action: 'Delete', description: '', status: 'Active' },
      { code: 'PRM-0005', name: 'Approve work', action: 'Approve', description: '', status: 'Active' }
    ]
  };
}
