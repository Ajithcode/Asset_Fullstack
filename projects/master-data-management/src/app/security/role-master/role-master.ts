import { Component } from '@angular/core';
import { MasterTable } from '../../shared/master-table/master-table';
import { MasterTableConfig } from '../../shared/master-table/master-table.model';

@Component({
  selector: 'app-mdm-security-role-master',
  imports: [MasterTable],
  template: '<app-master-table [config]="config" />'
})
export class RoleMasterPage {
  readonly config: MasterTableConfig = {
    key: 'security/role-master',
    title: 'Role Master',
    singular: 'Role',
    codePrefix: 'ROLE',
    fields: [
      { key: 'name', label: 'Name', required: true },
      { key: 'description', label: 'Description' },
      { key: 'systemRole', label: 'System Role', type: 'boolean' }
    ],
    rows: [
      { code: 'ROLE-0001', name: 'Administrator', description: 'Full access', systemRole: true, status: 'Active' },
      { code: 'ROLE-0002', name: 'Maintenance Manager', description: 'Plans and approves work', systemRole: false, status: 'Active' },
      { code: 'ROLE-0003', name: 'Technician', description: 'Executes work orders', systemRole: false, status: 'Active' },
      { code: 'ROLE-0004', name: 'Viewer', description: 'Read-only', systemRole: false, status: 'Active' }
    ]
  };
}
