import { Component } from '@angular/core';
import { MasterTable } from '../../shared/master-table/master-table';
import { MasterTableConfig } from '../../shared/master-table/master-table.model';

@Component({
  selector: 'app-mdm-people-skill',
  imports: [MasterTable],
  template: '<app-master-table [config]="config" />'
})
export class SkillPage {
  readonly config: MasterTableConfig = {
    key: 'people/skill',
    title: 'Skill',
    singular: 'Skill',
    codePrefix: 'SKL',
    fields: [
      { key: 'name', label: 'Name', required: true },
      { key: 'certRequired', label: 'Certification Required', type: 'boolean' },
      { key: 'description', label: 'Description' }
    ],
    rows: [
      { code: 'SKL-0001', name: 'Electrical', certRequired: true, description: 'LT/HT panel work', status: 'Active' },
      { code: 'SKL-0002', name: 'Mechanical', certRequired: false, description: '', status: 'Active' },
      { code: 'SKL-0003', name: 'HVAC', certRequired: true, description: 'Chiller and AHU service', status: 'Active' },
      { code: 'SKL-0004', name: 'Plumbing', certRequired: false, description: '', status: 'Active' }
    ]
  };
}
