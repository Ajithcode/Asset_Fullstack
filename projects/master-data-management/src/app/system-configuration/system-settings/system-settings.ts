import { Component } from '@angular/core';
import { MasterTable } from '../../shared/master-table/master-table';
import { MasterTableConfig } from '../../shared/master-table/master-table.model';

@Component({
  selector: 'app-mdm-system-configuration-system-settings',
  imports: [MasterTable],
  template: '<app-master-table [config]="config" />'
})
export class SystemSettingsPage {
  readonly config: MasterTableConfig = {
    key: 'system-configuration/system-settings',
    title: 'System Settings',
    singular: 'Setting',
    codePrefix: 'SET',
    fields: [
      { key: 'name', label: 'Setting', required: true },
      { key: 'value', label: 'Value', required: true },
      { key: 'group', label: 'Group', type: 'select', options: ['General', 'Localization', 'Notifications', 'Security'] }
    ],
    rows: [
      { code: 'SET-0001', name: 'Date format', value: 'DD-MM-YYYY', group: 'Localization', status: 'Active' },
      { code: 'SET-0002', name: 'Currency', value: 'INR', group: 'Localization', status: 'Active' },
      { code: 'SET-0003', name: 'Session timeout (min)', value: '30', group: 'Security', status: 'Active' },
      { code: 'SET-0004', name: 'Alert email sender', value: 'alerts@purpleiq.ai', group: 'Notifications', status: 'Active' }
    ]
  };
}
