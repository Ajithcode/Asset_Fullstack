import { Component } from '@angular/core';
import { MasterTable } from '../../shared/master-table/master-table';
import { MasterTableConfig } from '../../shared/master-table/master-table.model';

@Component({
  selector: 'app-mdm-people-employee',
  imports: [MasterTable],
  template: '<app-master-table [config]="config" />'
})
export class EmployeePage {
  readonly config: MasterTableConfig = {
    key: 'people/employee',
    title: 'Employee',
    singular: 'Employee',
    codePrefix: 'EMP',
    fields: [
      { key: 'name', label: 'Full Name', required: true },
      { key: 'email', label: 'Email', type: 'email' },
      { key: 'department', label: 'Department', type: 'select', options: ['Production', 'Maintenance', 'Quality'] },
      { key: 'designation', label: 'Designation' },
      { key: 'joiningDate', label: 'Joining Date', type: 'date' }
    ],
    rows: [
      { code: 'EMP-0001', name: 'Arun Kumar', email: 'arun.kumar@example.com', department: 'Maintenance', designation: 'Supervisor', joiningDate: '2021-04-12', status: 'Active' },
      { code: 'EMP-0002', name: 'Divya Raman', email: 'divya.raman@example.com', department: 'Quality', designation: 'Engineer', joiningDate: '2022-08-01', status: 'Active' }
    ]
  };
}
