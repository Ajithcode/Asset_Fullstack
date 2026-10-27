import { Component } from '@angular/core';
import { MasterTable } from '../../shared/master-table/master-table';
import { MasterTableConfig } from '../../shared/master-table/master-table.model';

@Component({
  selector: 'app-mdm-compliance-permit-type',
  imports: [MasterTable],
  template: '<app-master-table [config]="config" />'
})
export class PermitTypePage {
  readonly config: MasterTableConfig = {
    key: 'compliance/permit-type',
    title: 'Permit Type',
    singular: 'Permit Type',
    codePrefix: 'PT',
    fields: [
      { key: 'name', label: 'Name', required: true },
      { key: 'validityHours', label: 'Validity (hrs)', type: 'number' },
      { key: 'approval', label: 'Approval Required', type: 'boolean' },
      { key: 'precautions', label: 'Precautions', type: 'textarea', hideInTable: true }
    ],
    rows: [
      { code: 'PT-0001', name: 'Hot Work', validityHours: 8, approval: true, precautions: '', status: 'Active' },
      { code: 'PT-0002', name: 'Work at Height', validityHours: 12, approval: true, precautions: '', status: 'Active' },
      { code: 'PT-0003', name: 'Electrical Isolation (LOTO)', validityHours: 8, approval: true, precautions: '', status: 'Active' },
      { code: 'PT-0004', name: 'Confined Space', validityHours: 4, approval: true, precautions: '', status: 'Active' }
    ]
  };
}
