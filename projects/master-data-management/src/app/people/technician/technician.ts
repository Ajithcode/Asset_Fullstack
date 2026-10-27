import { Component } from '@angular/core';
import { MasterTable } from '../../shared/master-table/master-table';
import { MasterTableConfig } from '../../shared/master-table/master-table.model';

@Component({
  selector: 'app-mdm-people-technician',
  imports: [MasterTable],
  template: '<app-master-table [config]="config" />'
})
export class TechnicianPage {
  readonly config: MasterTableConfig = {
    key: 'people/technician',
    title: 'Technician',
    singular: 'Technician',
    codePrefix: 'TEC',
    fields: [
      { key: 'name', label: 'Full Name', required: true },
      { key: 'phone', label: 'Phone' },
      { key: 'skill', label: 'Primary Skill', type: 'select', options: ['Electrical', 'Mechanical', 'HVAC', 'Plumbing'] },
      { key: 'team', label: 'Team', type: 'select', options: ['Alpha', 'Bravo', 'Night Crew'] },
      { key: 'external', label: 'External', type: 'boolean' }
    ],
    rows: [
      { code: 'TEC-0001', name: 'Murugan S', phone: '98400 11111', skill: 'Electrical', team: 'Alpha', external: false, status: 'Active' },
      { code: 'TEC-0002', name: 'Karthik R', phone: '98400 22222', skill: 'HVAC', team: 'Bravo', external: false, status: 'Active' },
      { code: 'TEC-0003', name: 'Vinod P', phone: '98400 33333', skill: 'Mechanical', team: 'Night Crew', external: true, status: 'Active' }
    ]
  };
}
