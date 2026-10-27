import { Component } from '@angular/core';
import { MasterTable } from '../../shared/master-table/master-table';
import { MasterTableConfig } from '../../shared/master-table/master-table.model';

@Component({
  selector: 'app-mdm-people-team',
  imports: [MasterTable],
  template: '<app-master-table [config]="config" />'
})
export class TeamPage {
  readonly config: MasterTableConfig = {
    key: 'people/team',
    title: 'Team',
    singular: 'Team',
    codePrefix: 'TEAM',
    fields: [
      { key: 'name', label: 'Name', required: true },
      { key: 'lead', label: 'Team Lead' },
      { key: 'site', label: 'Site', type: 'select', options: ['Chennai HQ', 'Bengaluru Plant', 'Pune Warehouse'] }
    ],
    rows: [
      { code: 'TEAM-0001', name: 'Alpha', lead: 'Arun Kumar', site: 'Chennai HQ', status: 'Active' },
      { code: 'TEAM-0002', name: 'Bravo', lead: 'Divya Raman', site: 'Bengaluru Plant', status: 'Active' },
      { code: 'TEAM-0003', name: 'Night Crew', lead: 'Vinod P', site: 'Chennai HQ', status: 'Active' }
    ]
  };
}
