import { Component } from '@angular/core';
import { MasterTable } from '../../shared/master-table/master-table';
import { MasterTableConfig } from '../../shared/master-table/master-table.model';

@Component({
  selector: 'app-mdm-vendor-supplier',
  imports: [MasterTable],
  template: '<app-master-table [config]="config" />'
})
export class SupplierPage {
  readonly config: MasterTableConfig = {
    key: 'vendor/supplier',
    title: 'Supplier',
    singular: 'Supplier',
    codePrefix: 'SUP',
    fields: [
      { key: 'name', label: 'Name', required: true },
      { key: 'contact', label: 'Contact Person' },
      { key: 'email', label: 'Email', type: 'email' },
      { key: 'phone', label: 'Phone' },
      { key: 'gstin', label: 'GSTIN' }
    ],
    rows: [
      { code: 'SUP-0001', name: 'Sri Balaji Electricals', contact: 'Ramesh', email: 'sales@balaji.example.com', phone: '044 2345 6789', gstin: '33ABCDE1234F1Z5', status: 'Active' },
      { code: 'SUP-0002', name: 'Coolair Spares', contact: 'Nisha', email: 'orders@coolair.example.com', phone: '080 4567 1234', gstin: '29PQRSX5678K1Z2', status: 'Active' }
    ]
  };
}
