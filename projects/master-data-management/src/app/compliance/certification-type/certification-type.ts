import { Component } from '@angular/core';
import { MasterTable } from '../../shared/master-table/master-table';
import { MasterTableConfig } from '../../shared/master-table/master-table.model';

@Component({
  selector: 'app-mdm-compliance-certification-type',
  imports: [MasterTable],
  template: '<app-master-table [config]="config" />'
})
export class CertificationTypePage {
  readonly config: MasterTableConfig = {
    key: 'compliance/certification-type',
    title: 'Certification Type',
    singular: 'Certification Type',
    codePrefix: 'CERT',
    fields: [
      { key: 'name', label: 'Name', required: true },
      { key: 'issuer', label: 'Issuing Body' },
      { key: 'validityMonths', label: 'Validity (months)', type: 'number' },
      { key: 'renewal', label: 'Renewal Required', type: 'boolean' }
    ],
    rows: [
      { code: 'CERT-0001', name: 'Fire Safety NOC', issuer: 'Fire & Rescue Dept', validityMonths: 12, renewal: true, status: 'Active' },
      { code: 'CERT-0002', name: 'Electrical Safety', issuer: 'CEIG', validityMonths: 12, renewal: true, status: 'Active' },
      { code: 'CERT-0003', name: 'ISO 9001', issuer: 'BSI', validityMonths: 36, renewal: true, status: 'Active' }
    ]
  };
}
