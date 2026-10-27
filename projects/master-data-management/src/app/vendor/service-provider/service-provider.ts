import { Component } from '@angular/core';
import { MasterTable } from '../../shared/master-table/master-table';
import { MasterTableConfig } from '../../shared/master-table/master-table.model';

@Component({
  selector: 'app-mdm-vendor-service-provider',
  imports: [MasterTable],
  template: '<app-master-table [config]="config" />'
})
export class ServiceProviderPage {
  readonly config: MasterTableConfig = {
    key: 'vendor/service-provider',
    title: 'Service Provider',
    singular: 'Service Provider',
    codePrefix: 'SP',
    fields: [
      { key: 'name', label: 'Name', required: true },
      { key: 'serviceType', label: 'Service Type', type: 'select', options: ['Electrical', 'HVAC', 'Housekeeping', 'Security', 'Civil'] },
      { key: 'contact', label: 'Contact Person' },
      { key: 'phone', label: 'Phone' }
    ],
    rows: [
      { code: 'SP-0001', name: 'Prime Facility Services', serviceType: 'Housekeeping', contact: 'Latha', phone: '98410 44444', status: 'Active' },
      { code: 'SP-0002', name: 'VoltCare', serviceType: 'Electrical', contact: 'Suresh', phone: '98410 55555', status: 'Active' }
    ]
  };
}
