import { Component } from '@angular/core';
import { MasterTable } from '../../shared/master-table/master-table';
import { MasterTableConfig } from '../../shared/master-table/master-table.model';

@Component({
  selector: 'app-mdm-security-module-access-master',
  imports: [MasterTable],
  template: '<app-master-table [config]="config" />'
})
export class ModuleAccessMasterPage {
  readonly config: MasterTableConfig = {
    key: 'security/module-access-master',
    title: 'Module Access Master',
    singular: 'Module Access',
    codePrefix: 'MA',
    fields: [
      { key: 'role', label: 'Role', type: 'select', options: ['Administrator', 'Maintenance Manager', 'Technician', 'Viewer'], required: true },
      { key: 'module', label: 'Module', type: 'select', options: ['Dashboard', 'Assets', 'Tracking & IoT', 'Maintenance', 'Inventory', 'Reports', 'Administration', 'Master Data Management'], required: true },
      { key: 'canView', label: 'Can View', type: 'boolean' },
      { key: 'canEdit', label: 'Can Edit', type: 'boolean' }
    ],
    rows: [
      { code: 'MA-0001', role: 'Administrator', module: 'Master Data Management', canView: true, canEdit: true, status: 'Active' },
      { code: 'MA-0002', role: 'Technician', module: 'Maintenance', canView: true, canEdit: true, status: 'Active' },
      { code: 'MA-0003', role: 'Viewer', module: 'Reports', canView: true, canEdit: false, status: 'Active' }
    ]
  };
}
