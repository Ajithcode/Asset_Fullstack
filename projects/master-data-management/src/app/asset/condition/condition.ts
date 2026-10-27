import { Component } from '@angular/core';
import { MasterTable } from '../../shared/master-table/master-table';
import { MasterTableConfig } from '../../shared/master-table/master-table.model';

@Component({
  selector: 'app-mdm-asset-condition',
  imports: [MasterTable],
  template: '<app-master-table [config]="config" />'
})
export class ConditionPage {
  readonly config: MasterTableConfig = {
    key: 'asset/condition',
    title: 'Condition',
    singular: 'Condition',
    codePrefix: 'CND',
    fields: [
      { key: 'name', label: 'Name', required: true },
      { key: 'score', label: 'Score (1-5)', type: 'number' },
      { key: 'description', label: 'Description' }
    ],
    rows: [
      { code: 'CND-0001', name: 'Excellent', score: 5, description: 'Like new, no defects', status: 'Active' },
      { code: 'CND-0002', name: 'Good', score: 4, description: 'Minor wear', status: 'Active' },
      { code: 'CND-0003', name: 'Fair', score: 3, description: 'Needs attention soon', status: 'Active' },
      { code: 'CND-0004', name: 'Poor', score: 2, description: 'Frequent faults', status: 'Active' },
      { code: 'CND-0005', name: 'Failed', score: 1, description: 'Not working', status: 'Active' }
    ]
  };
}
