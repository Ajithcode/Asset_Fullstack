import { Component } from '@angular/core';
import { MasterTable } from '../../shared/master-table/master-table';
import { MasterTableConfig } from '../../shared/master-table/master-table.model';

@Component({
  selector: 'app-mdm-security-user-management',
  imports: [MasterTable],
  template: '<app-master-table [config]="config" />'
})
export class UserManagementPage {
  readonly config: MasterTableConfig = {
    key: 'security/user-management',
    title: 'User Management',
    singular: 'User',
    codePrefix: 'USR',
    fields: [
      { key: 'name', label: 'Full Name', required: true },
      { key: 'email', label: 'Email', type: 'email', required: true },
      { key: 'role', label: 'Role', type: 'select', options: ['Administrator', 'Maintenance Manager', 'Technician', 'Viewer'] },
      { key: 'site', label: 'Site', type: 'select', options: ['Chennai HQ', 'Bengaluru Plant', 'Pune Warehouse'] },
      { key: 'lastLogin', label: 'Last Login', type: 'date' }
    ],
    rows: [
      { code: 'USR-0001', name: 'Arun Kumar', email: 'arun.kumar@example.com', role: 'Maintenance Manager', site: 'Chennai HQ', lastLogin: '2026-10-26', status: 'Active' },
      { code: 'USR-0002', name: 'Divya Raman', email: 'divya.raman@example.com', role: 'Viewer', site: 'Bengaluru Plant', lastLogin: '2026-10-21', status: 'Active' },
      { code: 'USR-0003', name: 'Murugan S', email: 'murugan.s@example.com', role: 'Technician', site: 'Chennai HQ', lastLogin: '2026-09-30', status: 'Inactive' }
    ]
  };
}
